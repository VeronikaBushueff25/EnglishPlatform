using System.Text.Json;
using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.LearningSessions.Domain;

/// <summary>
/// Элемент очереди занятия: карточка, которую нужно показать. Проблемное слово (<c>IsProblem</c>) сначала получает
/// разминку в более лёгком формате (<c>WarmedUp</c> — разминка пройдена), затем обычное задание.
/// </summary>
public sealed record SessionItem(
    Guid CardId, Guid UserWordId, string Mode, bool IsNew, bool Introduced, int Repeats, bool IsProblem = false, bool WarmedUp = false)
{
    public bool NeedsWarmUp => IsProblem && !WarmedUp && Repeats == 0;
}

public static class SessionKinds
{
    /// <summary>Обычное занятие дня: повторения и новые слова.</summary>
    public const string Daily = "Daily";

    /// <summary>Работа над ошибками: только проблемные слова, независимо от срока повторения.</summary>
    public const string Mistakes = "Mistakes";
}

/// <summary>
/// Учебное занятие: очередь карточек, текущее задание, счётчики. Очередь и задание хранятся как jsonb:
/// они читаются и пишутся только целиком и только этим занятием.
/// </summary>
public sealed class LearningSession : Entity<Guid>
{
    /// <summary>Сколько раз карточка может вернуться в то же занятие (после ошибки или короткого шага заучивания).</summary>
    public const int MaxRepeats = 2;

    private const int MaxRememberedIntros = 100;

    private List<SessionItem> _queue = [];
    private List<Guid> _intros = [];

    private LearningSession()
    {
        Kind = SessionKinds.Daily;
        QueueJson = "[]";
        IntrosJson = "[]";
    }

    private LearningSession(Guid id, Guid userId, string kind, IEnumerable<SessionItem> queue, DateTimeOffset now)
        : base(id)
    {
        UserId = userId;
        Kind = kind;
        StartedAt = now;
        LastActivityAt = now;
        _queue = queue.ToList();
        Total = _queue.Count;
        QueueJson = JsonSerializer.Serialize(_queue);
        IntrosJson = "[]";
    }

    public Guid UserId { get; private set; }

    public string Kind { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Номер текущего элемента очереди.</summary>
    public int Position { get; private set; }

    /// <summary>Размер очереди при старте; повторы внутри занятия добавляются сверху.</summary>
    public int Total { get; private set; }

    /// <summary>Текущее задание (снимок с правильными ответами) или null, если его ещё не выдали.</summary>
    public string? CurrentTaskJson { get; private set; }

    public Guid? CurrentTaskId { get; private set; }

    public int Answered { get; private set; }

    public int Correct { get; private set; }

    public int Almost { get; private set; }

    public int Wrong { get; private set; }

    public int NewWords { get; private set; }

    public int Reviews { get; private set; }

    private string QueueJson { get; set; }

    private string IntrosJson { get; set; }

    public IReadOnlyList<SessionItem> Queue => _queue.Count > 0 || QueueJson == "[]" ? _queue : _queue = JsonSerializer.Deserialize<List<SessionItem>>(QueueJson)!;

    public bool IsFinished => FinishedAt is not null;

    /// <summary>Сколько элементов очереди осталось, включая текущий.</summary>
    public int Remaining => Math.Max(0, Queue.Count - Position);

    public SessionItem? Current => Position < Queue.Count ? Queue[Position] : null;

    public static LearningSession Start(Guid userId, IEnumerable<SessionItem> queue, DateTimeOffset now) =>
        Start(userId, SessionKinds.Daily, queue, now);

    public static LearningSession Start(Guid userId, string kind, IEnumerable<SessionItem> queue, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, kind, queue, now);

    /// <summary>Разминка проблемного слова пройдена: дальше по этой карточке — обычное задание.</summary>
    public void CompleteWarmUp(Guid attemptId, DateTimeOffset now)
    {
        SessionItem item = Current ?? throw new InvalidOperationException("Очередь занятия пуста");
        ReplaceCurrent(item with { WarmedUp = true, Introduced = true });
        RememberAttempt(attemptId);
        ClearTask(now);
    }

    public void SetTask(Guid taskId, string taskJson, DateTimeOffset now)
    {
        CurrentTaskId = taskId;
        CurrentTaskJson = taskJson;
        LastActivityAt = now;
    }

    public bool WasIntroduced(Guid attemptId) => Intros().Contains(attemptId);

    /// <summary>Новое слово показали: дальше по этой же карточке пойдёт настоящее задание.</summary>
    public void CompleteIntroduction(Guid attemptId, DateTimeOffset now)
    {
        SessionItem item = Current ?? throw new InvalidOperationException("Очередь занятия пуста");
        ReplaceCurrent(item with { Introduced = true });
        RememberAttempt(attemptId);
        ClearTask(now);
    }

    private void RememberAttempt(Guid attemptId)
    {
        List<Guid> intros = Intros();
        intros.Add(attemptId);
        IntrosJson = JsonSerializer.Serialize(intros.TakeLast(MaxRememberedIntros));
        _intros = [];
    }

    /// <summary>
    /// Ответ засчитан: счётчики, переход к следующей карточке. <paramref name="repeatSoon"/> — карточку
    /// нужно показать ещё раз в этом же занятии (ошибка или короткий шаг заучивания).
    /// </summary>
    public void CompleteAnswer(string verdict, bool repeatSoon, DateTimeOffset now)
    {
        SessionItem item = Current ?? throw new InvalidOperationException("Очередь занятия пуста");

        Answered++;
        switch (verdict)
        {
            case "Correct":
                Correct++;
                break;
            case "Almost":
                Almost++;
                break;
            default:
                Wrong++;
                break;
        }

        if (item.Repeats == 0)
        {
            if (item.IsNew)
            {
                NewWords++;
            }
            else
            {
                Reviews++;
            }
        }

        var queue = Queue.ToList();
        if (repeatSoon && item.Repeats < MaxRepeats)
        {
            queue.Add(item with { IsNew = false, Introduced = true, Repeats = item.Repeats + 1 });
        }

        SaveQueue(queue);
        Position++;
        ClearTask(now);
    }

    /// <summary>Карточку больше нельзя показать (слово удалили) — пропускаем.</summary>
    public void Skip(DateTimeOffset now)
    {
        Position++;
        ClearTask(now);
    }

    public void Finish(DateTimeOffset now)
    {
        FinishedAt ??= now;
        ClearTask(now);
    }

    private void ReplaceCurrent(SessionItem item)
    {
        var queue = Queue.ToList();
        queue[Position] = item;
        SaveQueue(queue);
    }

    private void SaveQueue(List<SessionItem> queue)
    {
        _queue = queue;
        QueueJson = JsonSerializer.Serialize(queue);
    }

    private List<Guid> Intros() => _intros.Count > 0 ? _intros : _intros = JsonSerializer.Deserialize<List<Guid>>(IntrosJson)!;

    private void ClearTask(DateTimeOffset now)
    {
        CurrentTaskId = null;
        CurrentTaskJson = null;
        LastActivityAt = now;
    }
}
