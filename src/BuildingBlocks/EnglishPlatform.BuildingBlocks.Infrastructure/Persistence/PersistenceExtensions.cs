using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public const string ConnectionStringName = "Database";

    /// <summary>
    /// Регистрирует DbContext модуля: Npgsql, snake_case, таблица истории миграций в схеме модуля.
    /// Обычно контекст наследует <see cref="ModuleDbContext"/>; контекст Identity наследует IdentityDbContext
    /// и задаёт схему сам.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString(ConnectionStringName),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext)));
        return services;
    }

    /// <summary>Применяет миграции всех модулей по очереди.</summary>
    public static async Task MigrateModuleDatabasesAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

        foreach (ModuleDbContextRegistration registration in scope.ServiceProvider.GetServices<ModuleDbContextRegistration>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            logger.LogInformation("Применяю миграции {Context}", registration.ContextType.Name);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}

public sealed record ModuleDbContextRegistration(Type ContextType);
