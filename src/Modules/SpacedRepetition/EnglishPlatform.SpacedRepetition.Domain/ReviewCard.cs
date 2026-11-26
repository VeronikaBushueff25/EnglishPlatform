using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.SpacedRepetition.Domain;

/// <summary>Что проверяет карточка: узнавание (вижу слово — понимаю) или вспоминание (вижу перевод — пишу слово сам).</summary>
public enum CardMode
{
    Recognition,
    Recall,
}

/// <summary>
/// Карточка повторения одного слова личного словаря в одном режиме. У слова до двух карточек;
/// карточка вспоминания появляется, когда узнавание впервые перешло в долгое повторение.
/// </summary>
public sealed class ReviewCard : Entity<Guid>
{
    private ReviewCard()
    {
    }

    private ReviewCard(Guid id, Guid userId, Guid userWordId, CardMode mode, DateTimeOffset now)
        : base(id)
    {
        UserId = userId;
        UserWordId = userWordId;
        Mode = mode;
        State = CardState.New;
        Due = now;
        CreatedAt = now;
    }

    public Guid UserId { get; private set; }

    public Guid UserWordId { get; private set; }

    public CardMode Mode { get; private set; }

    public CardState State { get; private set; }

    /// <summary>Номер шага заучивания или переучивания.</summary>
    public int Step { get; private set; }

    public double Stability { get; private set; }

    public double Difficulty { get; private set; }

    public DateTimeOffset Due { get; private set; }

    public DateTimeOffset? LastReview { get; private set; }

    public int Reps { get; private set; }

    /// <summary>Сколько раз слово забывали после того, как оно было выучено.</summary>
    public int Lapses { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ReviewCard Create(Guid userId, Guid userWordId, CardMode mode, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, userWordId, mode, now);

    public MemoryState Memory => new(State, Step, Stability, Difficulty, Due, LastReview);

    /// <summary>Применяет ответ и возвращает запись журнала (журнал только дописывается).</summary>
    public ReviewLog Review(FsrsScheduler scheduler, Rating rating, DateTimeOffset now, int elapsedMs)
    {
        MemoryState before = Memory;
        MemoryState after = scheduler.Review(before, rating, now);

        if (before.State == CardState.Review && rating == Rating.Again)
        {
            Lapses++;
        }

        Reps++;
        State = after.State;
        Step = after.Step;
        Stability = after.Stability;
        Difficulty = after.Difficulty;
        Due = after.Due;
        LastReview = after.LastReview;

        return ReviewLog.Create(this, before, after, rating, now, elapsedMs);
    }
}

/// <summary>
/// Запись о каждом ответе: оценка, состояние до и после, интервалы. Не правится и не удаляется — это история обучения
/// и сырьё для подбора личных весов FSRS.
/// </summary>
public sealed class ReviewLog : Entity<Guid>
{
    private ReviewLog()
    {
    }

    public Guid CardId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid UserWordId { get; private set; }

    public CardMode Mode { get; private set; }

    public Rating Rating { get; private set; }

    public DateTimeOffset ReviewedAt { get; private set; }

    public int ElapsedMs { get; private set; }

    public CardState StateBefore { get; private set; }

    public CardState StateAfter { get; private set; }

    public double StabilityBefore { get; private set; }

    public double StabilityAfter { get; private set; }

    public double DifficultyAfter { get; private set; }

    /// <summary>Сколько дней прошло с прошлого ответа (0 для нового).</summary>
    public double ElapsedDays { get; private set; }

    /// <summary>Через сколько дней карточка запланирована снова.</summary>
    public double ScheduledDays { get; private set; }

    internal static ReviewLog Create(ReviewCard card, MemoryState before, MemoryState after, Rating rating, DateTimeOffset now, int elapsedMs) => new()
    {
        Id = Guid.CreateVersion7(),
        CardId = card.Id,
        UserId = card.UserId,
        UserWordId = card.UserWordId,
        Mode = card.Mode,
        Rating = rating,
        ReviewedAt = now,
        ElapsedMs = Math.Max(0, elapsedMs),
        StateBefore = before.State,
        StateAfter = after.State,
        StabilityBefore = before.Stability,
        StabilityAfter = after.Stability,
        DifficultyAfter = after.Difficulty,
        ElapsedDays = before.LastReview is { } last ? Math.Max(0, (now - last).TotalDays) : 0,
        ScheduledDays = Math.Max(0, (after.Due - now).TotalDays),
    };
}
