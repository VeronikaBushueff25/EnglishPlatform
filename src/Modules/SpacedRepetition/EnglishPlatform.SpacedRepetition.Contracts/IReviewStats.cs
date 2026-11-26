namespace EnglishPlatform.SpacedRepetition.Contracts;

/// <summary>
/// Где слова на пути «узнаю → использую». <c>Learning</c> — ещё заучивается; <c>Passive</c> — уверенно узнаётся;
/// <c>Active</c> — уверенно вспоминается самим (карточка вспоминания выучена с запасом). Слова без ответов сюда не входят.
/// </summary>
public sealed record VocabularyProgress(int Learning, int Passive, int Active);

/// <summary>Ответы за день (по часовому поясу пользователя): сколько, сколько удачных, сколько новых слов, суммарное время ответов.</summary>
public sealed record DailyReviews(DateOnly Day, int Reviews, int Successful, int NewWords, long AnswerMs);

/// <summary>
/// Прогресс слова: <c>Stage</c> — New (ещё не занимались), Learning, Passive, Active (как в статистике);
/// <c>Reviews</c> — сколько раз отвечали, <c>Lapses</c> — сколько раз забывали, <c>NextReview</c> — ближайшее повторение.
/// </summary>
public sealed record WordProgress(Guid UserWordId, string Stage, int Reviews, int Lapses, DateTimeOffset? NextReview);

public interface IReviewStats
{
    /// <summary>Прогресс всех слов пользователя, у которых есть карточки.</summary>
    Task<IReadOnlyList<WordProgress>> GetWordProgressAsync(Guid userId, double activeStabilityDays, CancellationToken cancellationToken);

    /// <param name="activeStabilityDays">С какой стабильности карточки вспоминания слово считается активным.</param>
    Task<VocabularyProgress> GetProgressAsync(Guid userId, double activeStabilityDays, CancellationToken cancellationToken);

    /// <summary>Ответы по дням начиная с <paramref name="from"/>; дни без ответов не возвращаются.</summary>
    Task<IReadOnlyList<DailyReviews>> GetDailyAsync(Guid userId, DateTimeOffset from, string timeZone, CancellationToken cancellationToken);
}
