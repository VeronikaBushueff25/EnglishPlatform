using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;

public static class DatabaseSetupLock
{
    /// <summary>
    /// Выполняет подготовку базы (миграции, начальные данные) под advisory lock PostgreSQL,
    /// чтобы несколько экземпляров приложения, стартующих одновременно, не делали это параллельно.
    /// Блокировка берётся в служебной базе <c>postgres</c>: целевой базы при первом запуске ещё нет.
    /// Ключ зависит от имени целевой базы, поэтому другие базы на сервере не блокируются.
    /// </summary>
    public static async Task RunExclusiveDatabaseSetupAsync(
        this WebApplication app,
        Func<Task> setup,
        CancellationToken cancellationToken = default)
    {
        string connectionString = app.Configuration.GetConnectionString(PersistenceExtensions.ConnectionStringName)
            ?? throw new InvalidOperationException($"Не задана строка подключения ConnectionStrings:{PersistenceExtensions.ConnectionStringName}.");

        var target = new NpgsqlConnectionStringBuilder(connectionString);
        long key = LockKey(target.Database ?? string.Empty);
        var maintenance = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", Pooling = false };

        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSetup");

        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection))
        {
            lockCommand.Parameters.AddWithValue("key", key);
            lockCommand.CommandTimeout = 0;
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        logger.LogInformation("Получена блокировка подготовки базы {Database}", target.Database);
        try
        {
            await setup();
        }
        finally
        {
            await using var unlockCommand = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
            unlockCommand.Parameters.AddWithValue("key", key);
            await unlockCommand.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static long LockKey(string database) =>
        BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes("english-platform-setup:" + database)));
}
