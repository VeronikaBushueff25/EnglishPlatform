using EnglishPlatform.BuildingBlocks.Events;

namespace EnglishPlatform.Identity.Contracts;

/// <summary>Пользователь зарегистрировался. Users создаёт по нему профиль с настройками по умолчанию.</summary>
public sealed record UserRegistered(Guid UserId, string Email, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
}
