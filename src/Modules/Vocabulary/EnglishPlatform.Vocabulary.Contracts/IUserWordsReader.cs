namespace EnglishPlatform.Vocabulary.Contracts;

/// <summary>
/// Слово личного словаря в том виде, который нужен занятию. Тип, часть речи и уровень — строками
/// (значения перечислений Vocabulary), чтобы контракт не зависел от домена модуля.
/// </summary>
public sealed record LearningWord(
    Guid Id,
    Guid? LexemeId,
    string Text,
    string Type,
    string? PartOfSpeech,
    string? Level,
    string? Transcription,
    IReadOnlyList<string> Translations,
    string? Example,
    string? ExampleTranslation,
    string? AudioUrl);

/// <summary>Чтение личного словаря для других модулей (повторение, занятия).</summary>
public interface IUserWordsReader
{
    /// <summary>Идентификаторы слов пользователя в порядке добавления.</summary>
    Task<IReadOnlyList<Guid>> ListIdsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LearningWord>> GetAsync(Guid userId, IReadOnlyCollection<Guid> wordIds, CancellationToken cancellationToken);

    /// <summary>
    /// Неправильные варианты перевода для задания с выбором: переводы других слов пользователя (сначала той же части речи),
    /// при нехватке — слов общей базы. Не совпадают ни с одним переводом самого слова.
    /// </summary>
    Task<IReadOnlyList<string>> GetDistractorsAsync(Guid userId, Guid wordId, int count, CancellationToken cancellationToken);

    /// <summary>Английские слова для выбора «перевод → слово»: того же типа и части речи, похожей длины.</summary>
    Task<IReadOnlyList<string>> GetEnglishDistractorsAsync(Guid userId, Guid wordId, int count, CancellationToken cancellationToken);
}
