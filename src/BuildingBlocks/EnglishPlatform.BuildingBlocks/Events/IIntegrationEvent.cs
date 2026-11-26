namespace EnglishPlatform.BuildingBlocks.Events;

/// <summary>Событие, которым модуль сообщает о факте другим модулям. Сами события объявляются в Contracts.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

/// <summary>Обработчик события из другого модуля.</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>Шина событий внутри процесса. Брокер не нужен, пока модули живут в одном процессе.</summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent;
}
