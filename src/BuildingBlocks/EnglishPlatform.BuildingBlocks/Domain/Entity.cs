namespace EnglishPlatform.BuildingBlocks.Domain;

/// <summary>Базовый класс сущности с идентификатором.</summary>
public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity(TId id)
    {
        Id = id;
    }

    protected Entity()
    {
        Id = default!;
    }

    public TId Id { get; protected set; }
}
