using EnglishPlatform.BuildingBlocks.Events;

namespace EnglishPlatform.Pets.Contracts;

/// <summary>Пользователь завёл питомца. Вид передаётся строкой, чтобы событие не зависело от домена.</summary>
public sealed record PetAdopted(Guid UserId, string Species, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
}
