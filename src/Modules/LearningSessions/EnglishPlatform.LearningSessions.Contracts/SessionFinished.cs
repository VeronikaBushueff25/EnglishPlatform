using EnglishPlatform.BuildingBlocks.Events;

namespace EnglishPlatform.LearningSessions.Contracts;

/// <summary>
/// Занятие закончено (публикуется один раз, только если в нём были ответы).
/// Pets кормит по нему питомца угощениями.
/// </summary>
public sealed record SessionFinished(
    Guid UserId,
    Guid SessionId,
    string Kind,
    int Answered,
    int Correct,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
}
