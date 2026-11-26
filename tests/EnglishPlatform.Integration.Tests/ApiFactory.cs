using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace EnglishPlatform.Integration.Tests;

/// <summary>
/// Поднимает API на настоящем PostgreSQL. Источник базы:
/// 1) переменная TEST_DATABASE_CONNECTION (локальный сервер, база будет пересоздана миграциями);
/// 2) иначе контейнер Testcontainers. Если Docker недоступен, тесты помечаются пропущенными.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@integration.tests";
    public const string AdminPassword = "admin12345";

    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public string? SkipReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION");
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        try
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }
        catch (Exception exception)
        {
            SkipReason = $"Нет PostgreSQL для интеграционных тестов (Docker недоступен и TEST_DATABASE_CONNECTION не задана): {exception.Message}";
        }
    }

    public void SkipIfUnavailable() => Assert.SkipWhen(SkipReason is not null, SkipReason ?? string.Empty);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Identity:AdminEmail", AdminEmail);
        builder.UseSetting("Identity:AdminPassword", AdminPassword);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
