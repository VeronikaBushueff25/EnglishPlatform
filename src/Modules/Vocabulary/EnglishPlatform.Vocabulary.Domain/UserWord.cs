using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Vocabulary.Domain;

/// <summary>
/// Слово в личном словаре пользователя. Хранит собственную копию текста и перевода:
/// пользователь может поправить перевод под себя, а слово остаётся в словаре, даже если его удалят из общей базы.
/// Пока пользователь копию не правил (<see cref="IsEdited"/> = false), она обновляется вместе со словом общей базы.
/// </summary>
public sealed class UserWord : Entity<Guid>
{
    public const int MaxNoteLength = 1000;

    private UserWord()
    {
        Text = string.Empty;
        NormalizedText = string.Empty;
        SearchText = string.Empty;
        Translations = [];
    }

    private UserWord(Guid id, Guid userId, DateTimeOffset now)
        : base(id)
    {
        UserId = userId;
        Text = string.Empty;
        NormalizedText = string.Empty;
        SearchText = string.Empty;
        Translations = [];
        AddedAt = now;
        UpdatedAt = now;
    }

    public Guid UserId { get; private set; }

    /// <summary>Слово общей базы, из которого добавлено. Пусто для своих слов и после удаления слова из базы.</summary>
    public Guid? LexemeId { get; private set; }

    public string Text { get; private set; }

    public string NormalizedText { get; private set; }

    /// <summary>Строка для поиска: текст и все переводы в нижнем регистре.</summary>
    public string SearchText { get; private set; }

    public LexemeType Type { get; private set; }

    public string? Transcription { get; private set; }

    /// <summary>Уровень CEFR. У слова из общей базы — уровень базы, у своего — указанный при добавлении или импорте.</summary>
    public CefrLevel? Level { get; private set; }

    public PartOfSpeech? PartOfSpeech { get; private set; }

    public List<string> Translations { get; private set; }

    public string? Example { get; private set; }

    public string? ExampleTranslation { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Пользователь правил слово, добавленное из общей базы: правки администратора его больше не трогают.</summary>
    public bool IsEdited { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserWord FromLexeme(Guid userId, Lexeme lexeme, string language, DateTimeOffset now)
    {
        var word = new UserWord(Guid.CreateVersion7(), userId, now) { LexemeId = lexeme.Id };
        word.SyncWith(lexeme, language, now);
        return word;
    }

    public static UserWord Custom(Guid userId, UserWordData data, DateTimeOffset now)
    {
        var word = new UserWord(Guid.CreateVersion7(), userId, now);
        word.Apply(data, now);
        return word;
    }

    public void Edit(UserWordData data, DateTimeOffset now)
    {
        string before = ContentKey();
        Apply(data, now);

        // Заметка — личное поле, она не отменяет синхронизацию с общей базой; правка самого слова отменяет.
        if (LexemeId is not null && ContentKey() != before)
        {
            IsEdited = true;
        }
    }

    private string ContentKey() =>
        string.Join('\n', Text, Type, Transcription, Level, PartOfSpeech, string.Join('|', Translations), Example, ExampleTranslation);

    public UserWordData ToData() => new(Text, Type, Transcription, Translations, Example, ExampleTranslation, Note, Level, PartOfSpeech);

    /// <summary>Уровень, проставленный сразу нескольким словам. Для слова из базы это правка копии.</summary>
    public void SetLevel(CefrLevel? level, DateTimeOffset now)
    {
        if (Level == level)
        {
            return;
        }

        Level = level;
        if (LexemeId is not null)
        {
            IsEdited = true;
        }

        UpdatedAt = now;
    }

    /// <summary>
    /// Своё слово становится копией слова общей базы (его перенесли в базу или оно там уже было).
    /// <paramref name="keepOwnContent"/>: перевод и пример остаются пользовательскими, из базы берутся только пустые поля.
    /// </summary>
    public void LinkToLexeme(Lexeme lexeme, string language, bool keepOwnContent, DateTimeOffset now)
    {
        LexemeId = lexeme.Id;
        if (keepOwnContent)
        {
            IsEdited = true;
            Transcription ??= lexeme.Transcription;
            Level ??= lexeme.Level;
            PartOfSpeech ??= lexeme.PartOfSpeech;
            UpdatedAt = now;
            return;
        }

        IsEdited = false;
        SyncWith(lexeme, language, now);
    }

    /// <summary>Подтягивает данные слова общей базы, если пользователь свою копию не правил.</summary>
    public void SyncWith(Lexeme lexeme, string language, DateTimeOffset now)
    {
        if (IsEdited || LexemeId != lexeme.Id)
        {
            return;
        }

        Text = lexeme.Text;
        NormalizedText = lexeme.NormalizedText;
        Type = lexeme.Type;
        Transcription = lexeme.Transcription;
        Level = lexeme.Level;
        PartOfSpeech = lexeme.PartOfSpeech;
        Translations = lexeme.TranslationsFor(language).ToList();
        LexemeExample? example = lexeme.Examples.FirstOrDefault();
        Example = example?.Text;
        ExampleTranslation = example?.Translation;
        SearchText = TextNormalizer.SearchKey(Text, Translations);
        UpdatedAt = now;
    }

    /// <summary>Слово удалено из общей базы: копия остаётся своим словом пользователя.</summary>
    public void DetachFromLexeme(DateTimeOffset now)
    {
        LexemeId = null;
        UpdatedAt = now;
    }

    private void Apply(UserWordData data, DateTimeOffset now)
    {
        Text = TextNormalizer.Clean(data.Text);
        NormalizedText = TextNormalizer.Normalize(data.Text);
        Type = data.Type;
        Transcription = TextNormalizer.CleanTranscription(data.Transcription);
        Level = data.Level;
        PartOfSpeech = data.PartOfSpeech;
        Translations = data.Translations
            .Select(TextNormalizer.Clean)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Lexeme.MaxTranslations)
            .ToList();
        Example = TextNormalizer.NullIfEmpty(data.Example);
        ExampleTranslation = Example is null ? null : TextNormalizer.NullIfEmpty(data.ExampleTranslation);
        Note = TextNormalizer.NullIfEmpty(data.Note);
        SearchText = TextNormalizer.SearchKey(Text, Translations);
        UpdatedAt = now;
    }
}

public sealed record UserWordData(
    string Text,
    LexemeType Type,
    string? Transcription,
    IReadOnlyCollection<string> Translations,
    string? Example,
    string? ExampleTranslation,
    string? Note,
    CefrLevel? Level = null,
    PartOfSpeech? PartOfSpeech = null);
