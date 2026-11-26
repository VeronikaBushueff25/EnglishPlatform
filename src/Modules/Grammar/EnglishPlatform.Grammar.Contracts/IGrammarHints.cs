namespace EnglishPlatform.Grammar.Contracts;

/// <summary>Подсказка к ошибке: название типа ошибки и, если есть, тема с коротким правилом.</summary>
public sealed record GrammarHint(string TagCode, string TagTitle, string? TopicSlug, string? TopicTitle, string? Rule);

public sealed record GrammarTopicRef(string Slug, string Title, string? Level, string Summary);

public interface IGrammarHints
{
    /// <summary>Подсказка по коду тега ошибки (Typo, Article, WordForm, Preposition…). null — тег неизвестен.</summary>
    Task<GrammarHint?> GetForTagAsync(string tagCode, CancellationToken cancellationToken);

    /// <summary>Темы грамматики (идентификатор, название, уровень) — для рекомендаций в учебном плане.</summary>
    Task<IReadOnlyList<GrammarTopicRef>> ListTopicsAsync(CancellationToken cancellationToken);

    /// <summary>Все теги с названиями — для статистики слабых мест.</summary>
    Task<IReadOnlyList<GrammarHint>> ListAsync(CancellationToken cancellationToken);
}
