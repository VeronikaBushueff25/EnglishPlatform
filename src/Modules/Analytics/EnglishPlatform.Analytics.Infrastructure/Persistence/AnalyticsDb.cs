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
