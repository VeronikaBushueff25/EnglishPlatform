namespace EnglishPlatform.Vocabulary.Contracts;

/// <summary>Сколько слов добавлено в стартовый набор и сколько подходящих было всего.</summary>
public sealed record StarterWordsResult(int Added, int Available);

/// <summary>Стартовый набор слов по итогам теста уровня.</summary>
public interface IStarterWords
{
    /// <summary>
    /// Добавляет в личный словарь слова общей базы нужных уровней (A1…C2, строками), сначала из предпочтительных
    /// категорий (по идентификатору категории), затем остальные. Слова, которые уже есть в словаре, пропускаются.
    /// </summary>
    Task<StarterWordsResult> AddAsync(
        Guid userId,
        IReadOnlyCollection<string> levels,
        IReadOnlyCollection<string> preferredCategorySlugs,
        int limit,
        CancellationToken cancellationToken);
}
