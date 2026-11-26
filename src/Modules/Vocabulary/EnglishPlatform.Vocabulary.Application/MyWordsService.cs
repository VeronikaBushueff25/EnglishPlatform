using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Vocabulary.Application.Import;
using EnglishPlatform.Vocabulary.Contracts;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Vocabulary.Application;

/// <summary>Личный словарь текущего пользователя.</summary>
public sealed class MyWordsService(IVocabularyDb db, ICurrentUser currentUser, IEventBus eventBus, TimeProvider timeProvider)
{
    public async Task<PagedResult<MyWordDto>> ListAsync(LexemeFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        IQueryable<UserWord> query = db.UserWords.AsNoTracking().Where(w => w.UserId == userId);

        string key = TextNormalizer.Normalize(filter.Search);
        if (key.Length > 0)
        {
            query = query.Where(w => w.SearchText.Contains(key));
        }

        if (filter.Type is { } type)
        {
            query = query.Where(w => w.Type == type);
        }

        if (filter.NoLevel)
        {
            query = query.Where(w => w.Level == null);
        }
        else if (filter.Level is { } level)
        {
            query = query.Where(w => w.Level == level);
        }

        // Категории есть только у слов из общей базы.

        if (filter.CategoryId is { } categoryId)
        {
            IQueryable<Guid> ids = db.Lexemes.Where(l => l.CategoryIds.Contains(categoryId)).Select(l => l.Id);
            query = query.Where(w => w.LexemeId != null && ids.Contains(w.LexemeId.Value));
        }

        long total = await query.LongCountAsync(cancellationToken);
        List<UserWord> words = await query
            .OrderByDescending(w => w.AddedAt)
            .ThenByDescending(w => w.Id)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MyWordDto>(await ToDtosAsync(words, cancellationToken), page.SafePage, page.SafePageSize, total);
    }

    public async Task<Result<MyWordDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        UserWord? word = await FindAsync(id, tracking: false, cancellationToken);
        return word is null ? VocabularyErrors.MyWordNotFound : (await ToDtosAsync([word], cancellationToken))[0];
    }

    public async Task<Result<MyWordDto>> AddAsync(AddMyWordRequest request, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        DateTimeOffset now = timeProvider.GetUtcNow();
        UserWord word;

        if (request.LexemeId is { } lexemeId)
        {
            Lexeme? lexeme = await db.Lexemes.SingleOrDefaultAsync(l => l.Id == lexemeId, cancellationToken);
            if (lexeme is null)
            {
                return VocabularyErrors.LexemeNotFound;
            }

            if (await db.UserWords.AnyAsync(w => w.UserId == userId && w.LexemeId == lexemeId, cancellationToken))
            {
                return VocabularyErrors.AlreadyInMyWords;
            }

            word = UserWord.FromLexeme(userId, lexeme, Lexeme.DefaultLanguage, now);
            if (!string.IsNullOrWhiteSpace(request.Note))
            {
                word.Edit(word.ToData() with { Note = request.Note }, now);
            }
        }
        else
        {
            Classification guess = LexemeClassifier.Classify(request.Text!, null, null, null);
            var data = new UserWordData(
                request.Text!,
                request.Type ?? guess.Type,
                request.Transcription,
                request.Translations ?? [],
                request.Example,
                request.ExampleTranslation,
                request.Note,
                request.Level,
                request.PartOfSpeech ?? guess.PartOfSpeech);

            string normalized = TextNormalizer.Normalize(data.Text);
            if (await db.UserWords.AnyAsync(w => w.UserId == userId && w.LexemeId == null && w.NormalizedText == normalized, cancellationToken))
            {
                return VocabularyErrors.AlreadyInMyWords;
            }

            word = UserWord.Custom(userId, data, now);
        }

        db.UserWords.Add(word);
        await db.SaveChangesAsync(cancellationToken);
        await eventBus.PublishAsync(new UserWordAdded(userId, word.Id, now), cancellationToken);

        return (await ToDtosAsync([word], cancellationToken))[0];
    }

    public async Task<Result<MyWordDto>> UpdateAsync(Guid id, UpdateMyWordRequest request, CancellationToken cancellationToken)
    {
        UserWord? word = await FindAsync(id, tracking: true, cancellationToken);
        if (word is null)
        {
            return VocabularyErrors.MyWordNotFound;
        }

        string normalized = TextNormalizer.Normalize(request.Text);
        Guid userId = currentUser.UserId;
        if (word.LexemeId is null && normalized != word.NormalizedText
            && await db.UserWords.AnyAsync(w => w.UserId == userId && w.LexemeId == null && w.NormalizedText == normalized && w.Id != id, cancellationToken))
        {
            return VocabularyErrors.AlreadyInMyWords;
        }

        word.Edit(request.ToData(), timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([word], cancellationToken))[0];
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        UserWord? word = await FindAsync(id, tracking: true, cancellationToken);
        if (word is null)
        {
            return VocabularyErrors.MyWordNotFound;
        }

        db.UserWords.Remove(word);
        await db.SaveChangesAsync(cancellationToken);
        await eventBus.PublishAsync(new UserWordDeleted(word.UserId, word.Id, timeProvider.GetUtcNow()), cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Импорт своих слов из CSV. Строка с переводом становится своим словом; строка без перевода
    /// берётся из общей базы, если там есть такое слово. Дубли пропускаются.
    /// </summary>
    public async Task<Result<ImportResult>> ImportAsync(ImportCsvRequest request, CancellationToken cancellationToken)
    {
        (IReadOnlyList<CsvRow> rows, string? error) = CsvTable.Parse(request.Content, ImportLimits.MaxRows);
        if (error is not null)
        {
            return ImportErrors.InvalidFile(error);
        }

        Guid userId = currentUser.UserId;
        DateTimeOffset now = timeProvider.GetUtcNow();
        var issues = new ImportIssues();

        List<string> texts = rows.Select(r => TextNormalizer.Normalize(r.Get(CsvTable.Columns.Text))).Where(t => t.Length > 0).Distinct().ToList();
        var existing = (await db.UserWords.AsNoTracking()
                .Where(w => w.UserId == userId && texts.Contains(w.NormalizedText))
                .Select(w => w.NormalizedText)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        // Для строк без перевода — слово общей базы; если у слова несколько частей речи, берём самое раннее (стабильно).
        Dictionary<string, Lexeme> lexemes = (await db.Lexemes
                .Where(l => texts.Contains(l.NormalizedText))
                .OrderBy(l => l.Id)
                .ToListAsync(cancellationToken))
            .GroupBy(l => l.NormalizedText)
            .ToDictionary(g => g.Key, g => g.First());

        var added = new List<UserWord>();
        int skipped = 0;

        foreach (CsvRow row in rows)
        {
            string? text = row.Get(CsvTable.Columns.Text);
            if (text is null)
            {
                issues.Add(row.Line, "Пустое слово");
                continue;
            }

            if (text.Length > Lexeme.MaxTextLength)
            {
                issues.Add(row.Line, $"Слово длиннее {Lexeme.MaxTextLength} символов");
                continue;
            }

            string normalized = TextNormalizer.Normalize(text);
            if (!existing.Add(normalized))
            {
                skipped++;
                continue;
            }

            IReadOnlyList<string> translations = CsvTable.SplitValues(row.Get(CsvTable.Columns.Translation));
            if (translations.Count == 0)
            {
                if (lexemes.TryGetValue(normalized, out Lexeme? lexeme))
                {
                    added.Add(UserWord.FromLexeme(userId, lexeme, Lexeme.DefaultLanguage, now));
                }
                else
                {
                    existing.Remove(normalized);
                    issues.Add(row.Line, $"«{text}»: нет перевода, и в общей базе такого слова нет");
                }

                continue;
            }

            Classification kind = LexemeClassifier.Classify(
                text, row.Get(CsvTable.Columns.Type), row.Get(CsvTable.Columns.PartOfSpeech), row.Get(CsvTable.Columns.Note));
            if (kind.Error is not null)
            {
                existing.Remove(normalized);
                issues.Add(row.Line, $"«{text}»: {kind.Error}");
                continue;
            }

            if (!ImportValueParser.TryParseLevel(row.Get(CsvTable.Columns.Level), out CefrLevel? level))
            {
                existing.Remove(normalized);
                issues.Add(row.Line, $"«{text}»: уровень должен быть A1–C2, а не «{row.Get(CsvTable.Columns.Level)}»");
                continue;
            }

            // Чего нет в файле, берём у такого же слова общей базы: уровень, часть речи, транскрипцию.
            lexemes.TryGetValue(normalized, out Lexeme? known);
            added.Add(UserWord.Custom(
                userId,
                new UserWordData(
                    text,
                    kind.Type,
                    row.Get(CsvTable.Columns.Transcription) ?? known?.Transcription,
                    translations.Select(t => Truncate(t, Lexeme.MaxTranslationLength)!).ToList(),
                    Truncate(row.Get(CsvTable.Columns.Example), Lexeme.MaxExampleLength),
                    Truncate(row.Get(CsvTable.Columns.ExampleTranslation), Lexeme.MaxExampleLength),
                    Truncate(kind.Note, UserWord.MaxNoteLength),
                    level ?? known?.Level,
                    kind.PartOfSpeech ?? known?.PartOfSpeech),
                now));
        }

        db.UserWords.AddRange(added);
        await db.SaveChangesAsync(cancellationToken);
        foreach (UserWord word in added)
        {
            await eventBus.PublishAsync(new UserWordAdded(userId, word.Id, now), cancellationToken);
        }

        return new ImportResult(added.Count, 0, skipped, issues.ToList());
    }

    /// <summary>Проставляет уровень сразу нескольким словам своего словаря.</summary>
    public async Task<BulkResult> BulkAsync(BulkMyWordsRequest request, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        List<Guid> ids = request.Ids.Distinct().ToList();
        List<UserWord> words = await db.UserWords.Where(w => w.UserId == userId && ids.Contains(w.Id)).ToListAsync(cancellationToken);

        DateTimeOffset now = timeProvider.GetUtcNow();
        words.ForEach(w => w.SetLevel(request.Level, now));
        await db.SaveChangesAsync(cancellationToken);
        return new BulkResult(words.Count);
    }

    private Task<UserWord?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        IQueryable<UserWord> query = tracking ? db.UserWords : db.UserWords.AsNoTracking();
        return query.SingleOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);
    }

    private async Task<List<MyWordDto>> ToDtosAsync(IReadOnlyList<UserWord> words, CancellationToken cancellationToken)
    {
        List<Guid> lexemeIds = words.Where(w => w.LexemeId != null).Select(w => w.LexemeId!.Value).Distinct().ToList();
        var lexemes = await db.Lexemes.AsNoTracking()
            .Where(l => lexemeIds.Contains(l.Id))
            .Select(l => new { l.Id, l.AudioUrl })
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        return words.Select(w =>
            {
                var lexeme = w.LexemeId is { } lexemeId && lexemes.TryGetValue(lexemeId, out var found) ? found : null;
                return new MyWordDto(
                    w.Id,
                    w.LexemeId,
                    w.Text,
                    w.Type,
                    w.Transcription,
                    w.Translations,
                    w.Example,
                    w.ExampleTranslation,
                    w.Note,
                    w.Level,
                    w.PartOfSpeech,
                    lexeme?.AudioUrl,
                    w.IsEdited,
                    w.AddedAt);
            })
            .ToList();
    }

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
