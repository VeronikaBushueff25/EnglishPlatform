using System.ComponentModel.DataAnnotations;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.Vocabulary.Domain;

namespace EnglishPlatform.Vocabulary.Application;

public sealed record TranslationDto(
    [property: Required, MaxLength(8)] string Language,
    [property: Required, MaxLength(Lexeme.MaxTranslationLength)] string Text);

public sealed record ExampleDto(
    [property: Required, MaxLength(Lexeme.MaxExampleLength)] string Text,
    [property: MaxLength(Lexeme.MaxExampleLength)] string? Translation);

public sealed record CategoryRefDto(Guid Id, string Name, string Slug);

public sealed record CategoryDto(Guid Id, string Name, string Slug, string? Description, int LexemeCount);

/// <summary>Строка списка общей базы. <c>MyWordId</c> заполнен, если слово уже в личном словаре.</summary>
public sealed record LexemeSummaryDto(
    Guid Id,
    string Text,
    LexemeType Type,
    PartOfSpeech? PartOfSpeech,
    string? Transcription,
    CefrLevel? Level,
    IReadOnlyList<string> Translations,
    Guid? MyWordId);

public sealed record LexemeDto(
    Guid Id,
    string Text,
    LexemeType Type,
    PartOfSpeech? PartOfSpeech,
    string? Transcription,
    CefrLevel? Level,
    string? AudioUrl,
    IReadOnlyList<TranslationDto> Translations,
    IReadOnlyList<ExampleDto> Examples,
    IReadOnlyList<CategoryRefDto> Categories,
    Guid? MyWordId,
    DateTimeOffset UpdatedAt);

/// <summary>Слово личного словаря. Уровень и аудио берутся из общей базы, если слово оттуда.</summary>
public sealed record MyWordDto(
    Guid Id,
    Guid? LexemeId,
    string Text,
    LexemeType Type,
    string? Transcription,
    IReadOnlyList<string> Translations,
    string? Example,
    string? ExampleTranslation,
    string? Note,
    CefrLevel? Level,
    PartOfSpeech? PartOfSpeech,
    string? AudioUrl,
    bool IsEdited,
    DateTimeOffset AddedAt);

/// <summary>Фильтр списка слов. <c>NoLevel</c> — только слова без уровня (чтобы проставить его).</summary>
public sealed record LexemeFilter(string? Search, LexemeType? Type, CefrLevel? Level, Guid? CategoryId, bool NoLevel = false);

/// <summary>
/// Добавление в личный словарь: либо <c>LexemeId</c> слова общей базы, либо своё слово (текст и хотя бы один перевод).
/// </summary>
public sealed record AddMyWordRequest(
    Guid? LexemeId,
    [property: MaxLength(Lexeme.MaxTextLength, ErrorMessage = "Не длиннее 128 символов")] string? Text,
    LexemeType? Type,
    [property: MaxLength(Lexeme.MaxTranscriptionLength)] string? Transcription,
    [property: MaxLength(Lexeme.MaxTranslations, ErrorMessage = "Не больше 10 переводов")] IReadOnlyList<string>? Translations,
    [property: MaxLength(Lexeme.MaxExampleLength)] string? Example,
    [property: MaxLength(Lexeme.MaxExampleLength)] string? ExampleTranslation,
    [property: MaxLength(UserWord.MaxNoteLength)] string? Note,
    CefrLevel? Level = null,
    PartOfSpeech? PartOfSpeech = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (LexemeId is not null)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(Text))
        {
            yield return new ValidationResult("Введите слово или фразу", [nameof(Text)]);
        }

        foreach (ValidationResult result in VocabularyValidation.Translations(Translations, nameof(Translations)))
        {
            yield return result;
        }
    }
}

public sealed record UpdateMyWordRequest(
    [property: Required(ErrorMessage = "Введите слово или фразу"), MaxLength(Lexeme.MaxTextLength, ErrorMessage = "Не длиннее 128 символов")] string Text,
    LexemeType Type,
    [property: MaxLength(Lexeme.MaxTranscriptionLength)] string? Transcription,
    [property: Required, MaxLength(Lexeme.MaxTranslations, ErrorMessage = "Не больше 10 переводов")] IReadOnlyList<string> Translations,
    [property: MaxLength(Lexeme.MaxExampleLength)] string? Example,
    [property: MaxLength(Lexeme.MaxExampleLength)] string? ExampleTranslation,
    [property: MaxLength(UserWord.MaxNoteLength)] string? Note,
    CefrLevel? Level = null,
    PartOfSpeech? PartOfSpeech = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        VocabularyValidation.Translations(Translations, nameof(Translations));

    public UserWordData ToData() => new(Text, Type, Transcription, Translations, Example, ExampleTranslation, Note, Level, PartOfSpeech);
}

/// <summary>
/// Импорт CSV. Файл читает клиент и присылает текстом.
/// <c>UpdateExisting</c> и <c>AddToMyWords</c> действуют только при импорте в общую базу.
/// </summary>
public sealed record ImportCsvRequest(
    [property: Required(ErrorMessage = "Файл пуст"), MaxLength(ImportLimits.MaxContentLength, ErrorMessage = "Файл больше 2 МБ")] string Content,
    bool UpdateExisting = false,
    bool AddToMyWords = false);

public static class ImportLimits
{
    public const int MaxContentLength = 2_000_000;
    public const int MaxRows = 5000;
    public const int MaxIssues = 100;
}

public sealed record ImportIssue(int Line, string Message);

public sealed record ImportResult(int Added, int Updated, int Skipped, IReadOnlyList<ImportIssue> Issues, int AddedToMyWords = 0);

/// <summary>Действие над несколькими словами сразу: проставить уровень и/или добавить категорию.</summary>
public sealed record BulkLexemesRequest(
    [property: Required, MinLength(1, ErrorMessage = "Не выбрано ни одного слова"), MaxLength(500, ErrorMessage = "Не больше 500 слов за раз")] IReadOnlyList<Guid> Ids,
    CefrLevel? Level,
    Guid? AddCategoryId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Level is null && AddCategoryId is null)
        {
            yield return new ValidationResult("Выберите уровень или категорию", [nameof(Level)]);
        }
    }
}

public sealed record BulkMyWordsRequest(
    [property: Required, MinLength(1, ErrorMessage = "Не выбрано ни одного слова"), MaxLength(500, ErrorMessage = "Не больше 500 слов за раз")] IReadOnlyList<Guid> Ids,
    [property: Required(ErrorMessage = "Выберите уровень")] CefrLevel? Level);

public sealed record BulkResult(int Updated);

public sealed record SaveLexemeRequest(
    [property: Required(ErrorMessage = "Введите слово или фразу"), MaxLength(Lexeme.MaxTextLength, ErrorMessage = "Не длиннее 128 символов")] string Text,
    LexemeType Type,
    PartOfSpeech? PartOfSpeech,
    [property: MaxLength(Lexeme.MaxTranscriptionLength)] string? Transcription,
    CefrLevel? Level,
    [property: MaxLength(Lexeme.MaxAudioUrlLength), Url(ErrorMessage = "Нужна ссылка http(s)")] string? AudioUrl,
    [property: Required, MaxLength(Lexeme.MaxTranslations, ErrorMessage = "Не больше 10 переводов")] IReadOnlyList<TranslationDto> Translations,
    [property: Required, MaxLength(Lexeme.MaxExamples, ErrorMessage = "Не больше 10 примеров")] IReadOnlyList<ExampleDto> Examples,
    [property: Required, MaxLength(20)] IReadOnlyList<Guid> CategoryIds) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Translations.Any(t => !string.IsNullOrWhiteSpace(t.Text)))
        {
            yield return new ValidationResult("Нужен хотя бы один перевод", [nameof(Translations)]);
        }

        foreach (ValidationResult result in VocabularyValidation.Nested(Translations, nameof(Translations))
                     .Concat(VocabularyValidation.Nested(Examples, nameof(Examples))))
        {
            yield return result;
        }
    }

    public LexemeData ToData() => new(
        Text,
        Type,
        PartOfSpeech,
        Transcription,
        Level,
        AudioUrl,
        Translations.Select(t => new LexemeTranslation(t.Language, t.Text)).ToList(),
        Examples.Select(e => new LexemeExample(e.Text, e.Translation)).ToList(),
        CategoryIds);
}

public sealed record SaveCategoryRequest(
    [property: Required(ErrorMessage = "Введите название"), MaxLength(Category.MaxNameLength)] string Name,
    [property: Required(ErrorMessage = "Введите идентификатор"), MaxLength(Category.MaxSlugLength),
               RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Латинские буквы, цифры и дефис, например food или daily-life")] string Slug,
    [property: MaxLength(Category.MaxDescriptionLength)] string? Description);

internal static class VocabularyValidation
{
    public static IEnumerable<ValidationResult> Translations(IReadOnlyList<string>? translations, string member)
    {
        if (translations is null || !translations.Any(t => !string.IsNullOrWhiteSpace(t)))
        {
            yield return new ValidationResult("Нужен хотя бы один перевод", [member]);
            yield break;
        }

        if (translations.Any(t => t?.Length > Lexeme.MaxTranslationLength))
        {
            yield return new ValidationResult("Перевод не длиннее 256 символов", [member]);
        }
    }

    /// <summary>DataAnnotations не проверяет вложенные объекты сами, проверяем элементы списка вручную.</summary>
    public static IEnumerable<ValidationResult> Nested<T>(IEnumerable<T>? items, string member)
        where T : notnull
    {
        foreach (T item in items ?? [])
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(item, new ValidationContext(item), results, validateAllProperties: true))
            {
                foreach (ValidationResult result in results)
                {
                    yield return new ValidationResult(result.ErrorMessage, [member]);
                }
            }
        }
    }
}

public static class VocabularyErrors
{
    public static readonly Error LexemeNotFound = Error.NotFound("vocabulary.lexeme_not_found", "Слово не найдено в общей базе");

    public static readonly Error MyWordNotFound = Error.NotFound("vocabulary.my_word_not_found", "Слова нет в вашем словаре");

    public static readonly Error CategoryNotFound = Error.NotFound("vocabulary.category_not_found", "Категория не найдена");

    public static readonly Error AlreadyInMyWords = Error.Conflict("vocabulary.already_in_my_words", "Это слово уже есть в вашем словаре");

    public static readonly Error DuplicateLexeme = Error.Conflict("vocabulary.duplicate_lexeme", "Такое слово с этой частью речи уже есть в общей базе");

    public static readonly Error SlugTaken = Error.Conflict("vocabulary.slug_taken", "Категория с таким идентификатором уже есть");

    public static Error UnknownCategories(IEnumerable<Guid> ids) =>
        Error.Validation("vocabulary.unknown_categories", "Неизвестные категории", new Dictionary<string, string[]>
        {
            ["categoryIds"] = [$"Нет категорий: {string.Join(", ", ids)}"],
        });
}
