using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Exercises.Domain;

/// <summary>
/// Попытка ответа на задание. Только дописывается: это история обучения. Идентификатор присылает клиент,
/// поэтому повторная отправка того же ответа (обрыв связи) находит уже записанную попытку.
/// </summary>
public sealed class ExerciseAttempt : Entity<Guid>
{
    private ExerciseAttempt()
    {
        Kind = string.Empty;
        TaskSnapshot = string.Empty;
        Answer = string.Empty;
        Verdict = string.Empty;
        ErrorTag = string.Empty;
        CorrectAnswer = string.Empty;
        Explanation = string.Empty;
    }

    public Guid UserId { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid CardId { get; private set; }

    public Guid UserWordId { get; private set; }

    public string Kind { get; private set; }

    /// <summary>Снимок задания (jsonb): что именно видел пользователь и какие ответы принимались.</summary>
    public string TaskSnapshot { get; private set; }

    public string Answer { get; private set; }

    public bool UsedHint { get; private set; }

    public int ElapsedMs { get; private set; }

    public string Verdict { get; private set; }

    public string ErrorTag { get; private set; }

    public string CorrectAnswer { get; private set; }

    public string Explanation { get; private set; }

    /// <summary>Оценка FSRS, выведенная из ответа (1–4).</summary>
    public int Rating { get; private set; }

    public DateTimeOffset AnsweredAt { get; private set; }

    public static ExerciseAttempt Create(
        Guid attemptId,
        Guid userId,
        Guid sessionId,
        Guid cardId,
        Guid userWordId,
        string kind,
        string taskSnapshot,
        string answer,
        bool usedHint,
        int elapsedMs,
        string verdict,
        string errorTag,
        string correctAnswer,
        string explanation,
        int rating,
        DateTimeOffset answeredAt) => new()
    {
        Id = attemptId,
        UserId = userId,
        SessionId = sessionId,
        CardId = cardId,
        UserWordId = userWordId,
        Kind = kind,
        TaskSnapshot = taskSnapshot,
        Answer = answer.Length > 512 ? answer[..512] : answer,
        UsedHint = usedHint,
        ElapsedMs = Math.Max(0, elapsedMs),
        Verdict = verdict,
        ErrorTag = errorTag,
        CorrectAnswer = correctAnswer,
        Explanation = explanation,
        Rating = rating,
        AnsweredAt = answeredAt,
    };
}
