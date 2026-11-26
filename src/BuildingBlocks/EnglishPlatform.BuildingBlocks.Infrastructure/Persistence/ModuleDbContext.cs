using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Базовый DbContext модуля. У каждого модуля своя схема PostgreSQL и своя история миграций,
/// связи с другими модулями хранятся только идентификаторами.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    public abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Всё время в базе в UTC: Npgsql пишет в timestamptz только смещение 0.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
        base.ConfigureConventions(configurationBuilder);
    }

    private sealed class UtcDateTimeOffsetConverter()
        : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v.ToUniversalTime());
}
