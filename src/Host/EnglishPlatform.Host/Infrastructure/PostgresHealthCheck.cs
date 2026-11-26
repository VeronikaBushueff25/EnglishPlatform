using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace EnglishPlatform.Host.Infrastructure;

/// <summary>Готовность: база отвечает на SELECT 1.</summary>
internal sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(configuration.GetConnectionString(PersistenceExtensions.ConnectionStringName));
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL недоступен", exception);
        }
    }
}
