using EnglishPlatform.BuildingBlocks.Events;

namespace EnglishPlatform.Vocabulary.Contracts;

/// <summary>Слово добавлено в личный словарь. SpacedRepetition создаёт по нему карточку узнавания.</summary>
public sealed record UserWordAdded(Guid UserId, Guid UserWordId, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
}

/// <summary>Слово удалено из личного словаря. SpacedRepetition убирает его карточки из расписания.</summary>
public sealed record UserWordDeleted(Guid UserId, Guid UserWordId, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
}
