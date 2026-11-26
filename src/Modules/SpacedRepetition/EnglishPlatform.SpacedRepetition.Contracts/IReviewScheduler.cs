namespace EnglishPlatform.SpacedRepetition.Contracts;

public enum ReviewMode
{
    Recognition,
    Recall,
}

/// <summary>Оценка ответа (шкала FSRS). Её выводит занятие из результата проверки.</summary>
public enum ReviewRating
{
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4,
}

/// <summary>
/// Карточка в очереди. <c>IsNew</c> — слово ещё ни разу не показывали. <c>IsProblem</c> — слово часто забывают
/// или в нём часто ошибаются: оно идёт первым и сначала в более лёгком формате.
/// </summary>
public sealed record QueuedCard(Guid CardId, Guid UserWordId, ReviewMode Mode, bool IsNew, bool IsProblem = false);

/// <summary>Очередь на сегодня и счётчики дня (день считается по часовому поясу пользователя).</summary>
public sealed record DailyQueue(
    IReadOnlyList<QueuedCard> Reviews,
    IReadOnlyList<QueuedCard> NewCards,
    int ReviewsDoneToday,
    int NewDoneToday,
    int ReviewsDue,
    int NewAvailable);

public sealed record ReviewOutcome(
    Guid CardId,
    DateTimeOffset NextDue,
    string State,
    double Stability,
    bool Graduated,
    bool RecallUnlocked);

public interface IReviewScheduler
{
    /// <summary>
    /// Собирает очередь: просроченные карточки (самые «забываемые» первыми) и новые слова в пределах лимитов дня.
    /// Перед этим создаёт карточки узнавания для слов, у которых их ещё нет, и убирает карточки удалённых слов.
    /// </summary>
    /// <param name="problemWordIds">Слова, в которых пользователь часто ошибается (из Analytics); они идут первыми.</param>
    Task<DailyQueue> BuildQueueAsync(
        Guid userId,
        int newLimit,
        int reviewLimit,
        DateTimeOffset dayStart,
        DateTimeOffset now,
        IReadOnlyCollection<Guid> problemWordIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Очередь «работы над ошибками»: проблемные слова независимо от срока повторения, по одной карточке на слово
    /// (вспоминание, если оно уже открыто, — это сложнее и полезнее). Плюс слова, которые забывали несколько раз.
    /// </summary>
    Task<IReadOnlyList<QueuedCard>> BuildMistakesQueueAsync(
        Guid userId,
        IReadOnlyCollection<Guid> problemWordIds,
        int limit,
        CancellationToken cancellationToken);

    Task<QueuedCard?> GetCardAsync(Guid userId, Guid cardId, CancellationToken cancellationToken);

    /// <summary>Применяет оценку к карточке и пишет журнал. Карточка вспоминания открывается, когда узнавание впервые выучено.</summary>
    Task<ReviewOutcome> ReviewAsync(Guid userId, Guid cardId, ReviewRating rating, DateTimeOffset at, int elapsedMs, CancellationToken cancellationToken);
}
