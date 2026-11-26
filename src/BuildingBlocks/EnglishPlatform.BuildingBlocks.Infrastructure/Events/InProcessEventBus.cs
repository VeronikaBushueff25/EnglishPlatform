using EnglishPlatform.BuildingBlocks.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Публикует событие всем обработчикам в текущем scope, по очереди.
/// Ошибка обработчика пробрасывается вызывающему: в MVP так проще отлаживать, чем терять события молча.
/// </summary>
internal sealed class InProcessEventBus(IServiceProvider serviceProvider) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        foreach (IIntegrationEventHandler<TEvent> handler in serviceProvider.GetServices<IIntegrationEventHandler<TEvent>>())
        {
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}
