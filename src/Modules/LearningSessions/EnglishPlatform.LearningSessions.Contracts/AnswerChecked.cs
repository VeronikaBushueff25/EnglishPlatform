using EnglishPlatform.BuildingBlocks.Events;

namespace EnglishPlatform.LearningSessions.Contracts;

/// <summary>
/// Ответ проверен. Analytics считает по нему ошибки по типам, форматам и словам.
/// Перечисления передаются строками, чтобы событие не зависело от других модулей.
/// </summary>
public sealed record AnswerChecked(
    Guid UserId,
    Guid UserWordId,
    Guid CardId,
    string Mode,
    string ExerciseKind,
    string Verdict,
    string ErrorTag,
    bool IsWarmUp,
    int ElapsedMs,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
}
