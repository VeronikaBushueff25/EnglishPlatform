using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Vocabulary.Domain;

public enum LexemeType
{
    Word,
    Phrase,
    Idiom,
    PhrasalVerb,
}

public enum PartOfSpeech
{
    Noun,
    Verb,
    Adjective,
    Adverb,
    Pronoun,
    Preposition,
    Conjunction,
    Determiner,
    Interjection,
    Numeral,
}

/// <summary>Откуда слово в общей базе: вручную администратором, стартовый набор, импорт файла, внешний словарь.</summary>
public enum LexemeSource
{
    Manual,
    Seed,
    Import,
    Dictionary,
}

public enum CefrLevel
{
    A1,
    A2,
    B1,
    B2,
    C1,
    C2,
}

/// <summary>Перевод единицы лексики на язык пользователя (в MVP русский). Класс, а не record: EF хранит его как owned-тип.</summary>
public sealed class LexemeTranslation(string language, string text)
{
    public string Language { get; private set; } = language;

    public string Text { get; private set; } = text;
}

/// <summary>Пример употребления и его перевод.</summary>
public sealed class LexemeExample(string text, string? translation)
{
    public string Text { get; private set; } = text;

    public string? Translation { get; private set; } = translation;
}

/// <summary>Единица общей базы лексики: слово, фраза, идиома или фразовый глагол.</summary>
public sealed class Lexeme : Entity<Guid>
{
    public const int MaxTextLength = 128;
    public const int MaxTranscriptionLength = 128;
    public const int MaxTranslationLength = 256;
    public const int MaxExampleLength = 512;
    public const int MaxAudioUrlLength = 512;
    public const int MaxTranslations = 10;
    public const int MaxExamples = 10;
    public const string DefaultLanguage = "ru";

    private Lexeme()
    {
        Text = string.Empty;
        NormalizedText = string.Empty;
        SearchText = string.Empty;
        Translations = [];
        Examples = [];
        CategoryIds = [];
    }

    private Lexeme(Guid id, DateTimeOffset now)
        : base(id)
    {
        Text = string.Empty;
        NormalizedText = string.Empty;
        SearchText = string.Empty;
        Translations = [];
        Examples = [];
        CategoryIds = [];
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Text { get; private set; }

    /// <summary>Текст для поиска и проверки дублей: нижний регистр, схлопнутые пробелы.</summary>
    public string NormalizedText { get; private set; }

    /// <summary>Строка для поиска: текст и все переводы в нижнем регистре.</summary>
    public string SearchText { get; private set; }

    public LexemeType Type { get; private set; }

    public PartOfSpeech? PartOfSpeech { get; private set; }

    /// <summary>Транскрипция IPA без косых скобок, например <c>ˈwɔːtə</c>.</summary>
    public string? Transcription { get; private set; }

    public CefrLevel? Level { get; private set; }

    /// <summary>Ссылка на запись произношения. Если пусто, клиент озвучивает текст синтезатором браузера.</summary>
    public string? AudioUrl { get; private set; }

    public List<LexemeTranslation> Translations { get; private set; }

    public List<LexemeExample> Examples { get; private set; }

    /// <summary>Категории (тематические наборы). Хранятся массивом uuid[] с GIN-индексом.</summary>
    public List<Guid> CategoryIds { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public LexemeSource Source { get; private set; }

    /// <summary>Добавлено автоматически (из внешнего словаря) и ещё не проверено администратором.</summary>
    public bool NeedsReview { get; private set; }

    public static Lexeme Create(LexemeData data, DateTimeOffset now)
    {
        var lexeme = new Lexeme(Guid.CreateVersion7(), now);
        lexeme.Apply(data, now);
        return lexeme;
    }

    /// <summary>Слово из внешнего словаря: попадает в базу сразу, но помечается для проверки.</summary>
    public static Lexeme FromDictionary(LexemeData data, DateTimeOffset now)
    {
        Lexeme lexeme = Create(data, now);
        lexeme.Source = LexemeSource.Dictionary;
        lexeme.NeedsReview = true;
        return lexeme;
    }

    /// <summary>Правка администратором — это и есть проверка.</summary>
    public void Update(LexemeData data, DateTimeOffset now)
    {
        Apply(data, now);
        NeedsReview = false;
    }

    /// <summary>
    /// Дозаполнение из внешнего словаря: только пустые поля (транскрипция, часть речи, примеры), заполненное не трогаем.
    /// Возвращает, изменилось ли что-нибудь.
    /// </summary>
    public bool FillGaps(
        string? transcription,
        PartOfSpeech? partOfSpeech,
        IEnumerable<string> examples,
        DateTimeOffset now,
        CefrLevel? level = null,
        LexemeExample? translatedExample = null)
    {
        bool changed = false;
        if (Level is null && level is not null)
        {
            Level = level;
            changed = true;
        }

        // Пример с переводом — первым: его видит ученик в карточке и в заданиях с пропуском.
        if (translatedExample is { Translation.Length: > 0 } && !Examples.Any(e => e.Translation is not null))
        {
            Examples = [translatedExample, .. Examples.Where(e => e.Text != translatedExample.Text).Take(MaxExamples - 1).Select(e => new LexemeExample(e.Text, e.Translation))];
            changed = true;
        }

        if (Transcription is null && TextNormalizer.CleanTranscription(transcription) is { } ipa)
        {
            Transcription = ipa;
            changed = true;
        }

        if (PartOfSpeech is null && partOfSpeech is not null)
        {
            PartOfSpeech = partOfSpeech;
            changed = true;
        }

        if (Examples.Count == 0)
        {
            List<LexemeExample> added = examples.Select(TextNormalizer.Clean).Where(e => e.Length > 0).Take(MaxExamples)
                .Select(e => new LexemeExample(e, null)).ToList();
            if (added.Count > 0)
            {
                Examples = added;
                changed = true;
            }
        }

        if (changed)
        {
            UpdatedAt = now;
        }

        return changed;
    }

    public void MarkReviewed(DateTimeOffset now)
    {
        NeedsReview = false;
        UpdatedAt = now;
    }

    /// <summary>Перевод на язык пользователя; если его нет — на язык по умолчанию.</summary>
    public IReadOnlyList<string> TranslationsFor(string language)
    {
        List<string> own = Translations.Where(t => t.Language == language).Select(t => t.Text).ToList();
        return own.Count > 0 ? own : Translations.Where(t => t.Language == DefaultLanguage).Select(t => t.Text).ToList();
    }

    private void Apply(LexemeData data, DateTimeOffset now)
    {
        Text = TextNormalizer.Clean(data.Text);
        NormalizedText = TextNormalizer.Normalize(data.Text);
        Type = data.Type;
        PartOfSpeech = data.PartOfSpeech;
        Transcription = TextNormalizer.CleanTranscription(data.Transcription);
        Level = data.Level;
        AudioUrl = TextNormalizer.NullIfEmpty(data.AudioUrl);
        Translations = data.Translations
            .Select(t => new LexemeTranslation(t.Language.Trim().ToLowerInvariant(), TextNormalizer.Clean(t.Text)))
            .Where(t => t.Text.Length > 0)
            .DistinctBy(t => (t.Language, t.Text.ToLowerInvariant()))
            .Take(MaxTranslations)
            .ToList();
        Examples = data.Examples
            .Select(e => new LexemeExample(TextNormalizer.Clean(e.Text), TextNormalizer.NullIfEmpty(e.Translation)))
            .Where(e => e.Text.Length > 0)
            .Take(MaxExamples)
            .ToList();
        CategoryIds = data.CategoryIds.Distinct().ToList();
        SearchText = TextNormalizer.SearchKey(Text, Translations.Select(t => t.Text));
        UpdatedAt = now;
    }

    public void SetLevel(CefrLevel level, DateTimeOffset now)
    {
        Level = level;
        UpdatedAt = now;
    }

    public void AddCategory(Guid categoryId, DateTimeOffset now)
    {
        if (!CategoryIds.Contains(categoryId))
        {
            CategoryIds = [.. CategoryIds, categoryId];
            UpdatedAt = now;
        }
    }

    public void RemoveCategory(Guid categoryId, DateTimeOffset now)
    {
        if (CategoryIds.Remove(categoryId))
        {
            UpdatedAt = now;
        }
    }
}

/// <summary>Поля единицы лексики при создании и правке.</summary>
/// <summary>Данные слова общей базы (создание, правка, импорт).</summary>
public sealed record LexemeData(
    string Text,
    LexemeType Type,
    PartOfSpeech? PartOfSpeech,
    string? Transcription,
    CefrLevel? Level,
    string? AudioUrl,
    IReadOnlyCollection<LexemeTranslation> Translations,
    IReadOnlyCollection<LexemeExample> Examples,
    IReadOnlyCollection<Guid> CategoryIds);
