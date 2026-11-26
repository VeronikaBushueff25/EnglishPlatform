using System.ComponentModel.DataAnnotations;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Vocabulary.Application.Dictionary;

/// <summary>
/// Что можно улучшить в слове. Поля Suggested* заполнены только там, где есть что предложить:
/// уровень по спискам CEFR, начальная форма (buried → bury) с её переводами, тип (фразовый глагол, идиома),
/// пустые транскрипция, часть речи, пример.
/// </summary>
public sealed record WordSuggestionDto(
    Guid UserWordId,
    string Text,
    LexemeType Type,
    CefrLevel? Level,
    PartOfSpeech? PartOfSpeech,
    string? Transcription,
    IReadOnlyList<string> Translations,
    string? SuggestedText,
    LexemeType? SuggestedType,
    CefrLevel? SuggestedLevel,
    PartOfSpeech? SuggestedPartOfSpeech,
    string? SuggestedTranscription,
    string? SuggestedExample,
    IReadOnlyList<string>? SuggestedTranslations,
    string? SuggestedExampleTranslation = null);

/// <summary>Порция проверки: предложения по словам с <c>Offset</c> по <c>Offset + Checked</c> из <c>Total</c>.</summary>
public sealed record SuggestionsPageDto(IReadOnlyList<WordSuggestionDto> Items, int Offset, int Checked, int Total);

/// <summary>Принятые предложения по слову: null — поле не менять.</summary>
public sealed record ApplySuggestion(
    [property: Required] Guid UserWordId,
    [property: MaxLength(Lexeme.MaxTextLength)] string? Text,
    LexemeType? Type,
    CefrLevel? Level,
    PartOfSpeech? PartOfSpeech,
    [property: MaxLength(Lexeme.MaxTranscriptionLength)] string? Transcription,
    [property: MaxLength(Lexeme.MaxExampleLength)] string? Example,
    [property: MaxLength(Lexeme.MaxTranslations)] IReadOnlyList<string>? Translations,
    [property: MaxLength(Lexeme.MaxExampleLength)] string? ExampleTranslation = null);

public sealed record ApplySuggestionsRequest([property: Required, MaxLength(500)] IReadOnlyList<ApplySuggestion> Items);

/// <summary><c>Skipped</c> — начальная форма уже есть в словаре отдельным словом, её не переименовывали.</summary>
public sealed record ApplySuggestionsResult(int Updated, int Skipped);

/// <summary>
/// «Улучшить мои слова»: проверяет личный словарь по общей базе, внешнему словарю и спискам CEFR и предлагает правки.
/// Идёт порциями: внешний словарь отвечает не мгновенно, а слов бывают сотни. Ничего не меняет без подтверждения.
/// </summary>
public sealed class WordSuggestions(IVocabularyDb db, DictionaryLookup lookup, ICefrLevels levels, ICurrentUser currentUser, TimeProvider time)
{
    public const int MaxPage = 25;

    public async Task<SuggestionsPageDto> SuggestAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        offset = Math.Max(0, offset);
        limit = Math.Clamp(limit, 1, MaxPage);

        IQueryable<UserWord> mine = db.UserWords.AsNoTracking().Where(w => w.UserId == userId);
        int total = await mine.CountAsync(cancellationToken);
        List<UserWord> words = await mine.OrderBy(w => w.AddedAt).ThenBy(w => w.Id).Skip(offset).Take(limit).ToListAsync(cancellationToken);

        var items = new List<WordSuggestionDto>();
        foreach (UserWord word in words)
        {
            if (await SuggestAsync(word, cancellationToken) is { } suggestion)
            {
                items.Add(suggestion);
            }
        }

        return new SuggestionsPageDto(items, offset, words.Count, total);
    }

    public async Task<ApplySuggestionsResult> ApplyAsync(ApplySuggestionsRequest request, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        DateTimeOffset now = time.GetUtcNow();
        List<Guid> ids = request.Items.Select(i => i.UserWordId).Distinct().ToList();
        Dictionary<Guid, UserWord> words = await db.UserWords
            .Where(w => w.UserId == userId && ids.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, cancellationToken);

        int updated = 0, skipped = 0;
        foreach (ApplySuggestion item in request.Items)
        {
            if (!words.TryGetValue(item.UserWordId, out UserWord? word))
            {
                continue;
            }

            UserWordData data = word.ToData();
            if (item.Text is { Length: > 0 } text && TextNormalizer.Normalize(text) != word.NormalizedText)
            {
                string normalized = TextNormalizer.Normalize(text);
                if (await db.UserWords.AnyAsync(w => w.UserId == userId && w.Id != word.Id && w.NormalizedText == normalized, cancellationToken))
                {
                    skipped++;
                }
                else
                {
                    data = data with { Text = text, Translations = item.Translations is { Count: > 0 } t ? t : data.Translations };
                }
            }
            else if (item.Translations is { Count: > 0 } translations && data.Translations.Count == 0)
            {
                data = data with { Translations = translations };
            }

            data = data with
            {
                Type = item.Type ?? data.Type,
                Level = item.Level ?? data.Level,
                PartOfSpeech = item.PartOfSpeech ?? data.PartOfSpeech,
                Transcription = item.Transcription ?? data.Transcription,
                Example = data.Example ?? item.Example,
                ExampleTranslation = data.Example is null && item.Example is not null ? item.ExampleTranslation : data.ExampleTranslation,
            };

            word.Edit(data, now);
            updated++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ApplySuggestionsResult(updated, skipped);
    }

    private async Task<WordSuggestionDto?> SuggestAsync(UserWord word, CancellationToken cancellationToken)
    {
        Lexeme? lexeme;
        string? lemma = null;
        if (word.LexemeId is { } linked)
        {
            lexeme = await db.Lexemes.AsNoTracking().SingleOrDefaultAsync(l => l.Id == linked, cancellationToken);
        }
        else
        {
            (List<Lexeme> found, lemma) = await lookup.FindOrImportAsync(word.Text, cancellationToken);
            lexeme = found.FirstOrDefault(l => word.PartOfSpeech is not null && l.PartOfSpeech == word.PartOfSpeech) ?? found.FirstOrDefault();
        }

        if (lexeme is not null)
        {
            // Слово базы без транскрипции/части речи/примера — дозаполняем из словаря (и для всех, кто его учит).
            Lexeme? tracked = await db.Lexemes.SingleOrDefaultAsync(l => l.Id == lexeme.Id, cancellationToken);
            if (tracked is not null && await lookup.CompleteAsync(tracked, cancellationToken))
            {
                lexeme = tracked;
            }
        }

        bool single = !word.Text.Trim().Contains(' ');
        string? suggestedText = single && lemma is not null && !string.Equals(lemma, word.Text, StringComparison.OrdinalIgnoreCase) ? lemma : null;

        // Тип: из словаря, а если слова там нет — по составу (get over, put up with — фразовые глаголы).
        LexemeType type = lexeme?.Type ?? WiktionaryParser.Classify(word.Text, single ? string.Empty : "verb", []);
        LexemeType? suggestedType = type != word.Type ? type : null;
        LexemeType finalType = suggestedType ?? word.Type;

        PartOfSpeech? pos = word.PartOfSpeech ?? lexeme?.PartOfSpeech;
        // Уровень фразы по её словам — грубая оценка: для обычных фраз меняем уровень, только если его не было.
        CefrLevel? level = levels.Find(suggestedText ?? word.Text, pos, finalType)
            ?? (word.Level is null ? lexeme?.Level ?? lookup.LevelFor(word.Text, pos, finalType) : null);
        bool estimatedPhrase = finalType == LexemeType.Phrase && !single && word.Level is not null;
        CefrLevel? suggestedLevel = level is not null && level != word.Level && !estimatedPhrase ? level : null;

        PartOfSpeech? suggestedPos = word.PartOfSpeech is null ? lexeme?.PartOfSpeech : null;
        string? suggestedTranscription = word.Transcription is null ? lexeme?.Transcription : null;
        // Пример — лучше с переводом (из Tatoeba), иначе любой.
        LexemeExample? example = word.Example is null
            ? lexeme?.Examples.FirstOrDefault(e => e.Translation is not null) ?? lexeme?.Examples.FirstOrDefault()
            : null;
        string? suggestedExample = example?.Text;
        IReadOnlyList<string>? lexemeTranslations = lexeme?.TranslationsFor(Lexeme.DefaultLanguage);
        IReadOnlyList<string>? suggestedTranslations = (suggestedText is not null || word.Translations.Count == 0) && lexemeTranslations is { Count: > 0 }
            ? lexemeTranslations
            : null;

        if (suggestedText is null && suggestedType is null && suggestedLevel is null && suggestedPos is null
            && suggestedTranscription is null && suggestedExample is null && suggestedTranslations is null)
        {
            return null;
        }

        return new WordSuggestionDto(
            word.Id,
            word.Text,
            word.Type,
            word.Level,
            word.PartOfSpeech,
            word.Transcription,
            word.Translations,
            suggestedText,
            suggestedType,
            suggestedLevel,
            suggestedPos,
            suggestedTranscription,
            suggestedExample,
            suggestedTranslations,
            example?.Translation);
    }
}
