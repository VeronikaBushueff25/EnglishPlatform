using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

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
        string connectionString = ConnectionString(configuration);
        services.AddDbContext<TContext>(options => options
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext)));
        return services;
    }

    /// <summary>
    /// Строка подключения с ограниченным пулом. Все модули ходят в одну базу по одной строке, значит делят один пул;
    /// по умолчанию Npgsql разрешает 100 соединений — ровно столько, сколько Postgres принимает всего (max_connections).
    /// Под нагрузкой это кончалось ошибкой 53300 «слишком много подключений». С меньшим пулом запросы на пике ждут
    /// свободное соединение в очереди. Размер — Database:MaxPoolSize (по умолчанию 30); явно заданный в строке не трогаем.
    /// </summary>
    public static string ConnectionString(IConfiguration configuration)
    {
        string raw = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Не задана строка подключения ConnectionStrings:{ConnectionStringName}.");
        var builder = new NpgsqlConnectionStringBuilder(raw);
        bool explicitPool = raw.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("MaxPoolSize", StringComparison.OrdinalIgnoreCase);
        if (!explicitPool)
        {
            builder.MaxPoolSize = configuration.GetValue("Database:MaxPoolSize", 30);
        }

        return builder.ConnectionString;
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
