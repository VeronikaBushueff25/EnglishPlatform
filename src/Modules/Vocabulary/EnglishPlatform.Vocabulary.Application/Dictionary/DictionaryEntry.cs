using EnglishPlatform.Vocabulary.Domain;

namespace EnglishPlatform.Vocabulary.Application.Dictionary;

/// <summary>
/// Статья внешнего словаря, приведённая к модели нашей базы: одна часть речи одного слова.
/// <c>LemmaOf</c> — если это форма другого слова (buried → bury, ovaries → ovary).
/// </summary>
public sealed record DictionaryEntry(
    string Text,
    LexemeType Type,
    PartOfSpeech? PartOfSpeech,
    string? Transcription,
    IReadOnlyList<string> Translations,
    IReadOnlyList<string> Examples,
    string? LemmaOf);

/// <summary>Внешний словарь не ответил (сеть, таймаут) — это не «слова нет», пользователю стоит попробовать позже.</summary>
public sealed class DictionaryUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Внешний словарь (Викисловарь через kaikki.org). Пустой список — слова нет;
/// <see cref="DictionaryUnavailableException"/> — источник не ответил.
/// </summary>
public interface IDictionarySource
{
    Task<IReadOnlyList<DictionaryEntry>> LookupAsync(string text, CancellationToken cancellationToken);
}

/// <summary>Уровень CEFR по открытым спискам (CEFR-J A1–B2, Octanove C1–C2). Null — слова в списках нет.</summary>
public interface ICefrLevels
{
    /// <summary>
    /// Уровень слова; для выражений из нескольких слов — оценка: фразовый глагол — по глаголу, но не ниже B1;
    /// идиома — не ниже B2; фраза — по самому трудному слову.
    /// </summary>
    CefrLevel? Find(string text, PartOfSpeech? partOfSpeech, LexemeType type = LexemeType.Word);

    /// <summary>Слова списка для пополнения базы: уровень, слово, часть речи — в порядке уровней.</summary>
    IReadOnlyList<(CefrLevel Level, string Text, PartOfSpeech? PartOfSpeech)> Words { get; }
}
