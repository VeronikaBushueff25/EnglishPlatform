using EnglishPlatform.Analytics.Application;
using EnglishPlatform.Analytics.Domain;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishPlatform.Analytics.Infrastructure.Persistence;

public sealed class AnalyticsDb(DbContextOptions<AnalyticsDb> options) : ModuleDbContext(options), IAnalyticsDb
{
    public const string SchemaName = "analytics";

    public override string Schema => SchemaName;

    public DbSet<ErrorStat> ErrorStats => Set<ErrorStat>();

    public async Task IncrementErrorStatsAsync(Guid userId, IReadOnlyList<ErrorStatIncrement> increments, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (increments.Count == 0)
        {
            return;
        }

        Guid[] ids = increments.Select(_ => Guid.CreateVersion7()).ToArray();
        string[] dimensions = increments.Select(i => i.Dimension.ToString()).ToArray();
        string[] keys = increments.Select(i => i.Key).ToArray();
        int[] errors = increments.Select(i => i.Errors).ToArray();
        int[] almost = increments.Select(i => i.Almost).ToArray();
        DateTime atUtc = at.UtcDateTime;

        // Порядок строк фиксированный (как в increments), поэтому два одновременных запроса не заблокируют друг друга крест-накрест.
        await Database.ExecuteSqlAsync(
            $"""
            INSERT INTO analytics.error_stats (id, user_id, dimension, key, attempts, errors, almost, last_answered_at, last_error_at)
            SELECT r.id, {userId}, r.dimension, r.key, 1, r.errors, r.almost, {atUtc},
                   CASE WHEN r.errors + r.almost > 0 THEN {atUtc} END
            FROM unnest({ids}, {dimensions}, {keys}, {errors}, {almost}) AS r(id, dimension, key, errors, almost)
            ON CONFLICT (user_id, dimension, key) DO UPDATE SET
                attempts = error_stats.attempts + 1,
                errors = error_stats.errors + excluded.errors,
                almost = error_stats.almost + excluded.almost,
                last_answered_at = GREATEST(error_stats.last_answered_at, excluded.last_answered_at),
                last_error_at = GREATEST(error_stats.last_error_at, excluded.last_error_at)
            """,
            cancellationToken);
    }
}

internal sealed class ErrorStatConfiguration : IEntityTypeConfiguration<ErrorStat>
{
    public void Configure(EntityTypeBuilder<ErrorStat> stat)
    {
        stat.ToTable("error_stats");
        stat.HasKey(s => s.Id);
        stat.Property(s => s.Id).ValueGeneratedNever();
        stat.Property(s => s.Dimension).HasConversion<string>().HasMaxLength(16);
        stat.Property(s => s.Key).HasMaxLength(64);
        stat.Ignore(s => s.ErrorRate);
        stat.HasIndex(s => new { s.UserId, s.Dimension, s.Key }).IsUnique();
    }
}
