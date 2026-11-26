using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.LearningSessions.Application;
using EnglishPlatform.LearningSessions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishPlatform.LearningSessions.Infrastructure.Persistence;

public sealed class LearningSessionsDb(DbContextOptions<LearningSessionsDb> options) : ModuleDbContext(options), ILearningSessionsDb
{
    public const string SchemaName = "learning_sessions";

    public override string Schema => SchemaName;

    public DbSet<LearningSession> Sessions => Set<LearningSession>();
}

internal sealed class LearningSessionConfiguration : IEntityTypeConfiguration<LearningSession>
{
    public void Configure(EntityTypeBuilder<LearningSession> session)
    {
        session.ToTable("learning_sessions");
        session.HasKey(s => s.Id);
        session.Property(s => s.Id).ValueGeneratedNever();
        session.Property(s => s.Kind).HasMaxLength(16).HasDefaultValue("Daily");
        session.Property<string>("QueueJson").HasColumnName("queue").HasColumnType("jsonb");
        session.Property<string>("IntrosJson").HasColumnName("introduced_attempts").HasColumnType("jsonb");
        session.Property(s => s.CurrentTaskJson).HasColumnName("current_task").HasColumnType("jsonb");
        session.Ignore(s => s.Queue);
        session.Ignore(s => s.IsFinished);
        session.Ignore(s => s.Remaining);
        session.Ignore(s => s.Current);

        session.HasIndex(s => new { s.UserId, s.StartedAt });
    }
}
