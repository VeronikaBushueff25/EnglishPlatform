using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.LearningProfiles.Application;
using EnglishPlatform.LearningProfiles.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishPlatform.LearningProfiles.Infrastructure.Persistence;

public sealed class LearningProfilesDb(DbContextOptions<LearningProfilesDb> options) : ModuleDbContext(options), ILearningProfilesDb
{
    public const string SchemaName = "learning_profiles";

    public override string Schema => SchemaName;

    public DbSet<PlacementQuestion> Questions => Set<PlacementQuestion>();

    public DbSet<PlacementAttempt> Attempts => Set<PlacementAttempt>();

    public DbSet<SkillLevel> SkillLevels => Set<SkillLevel>();
}

internal sealed class PlacementQuestionConfiguration : IEntityTypeConfiguration<PlacementQuestion>
{
    public void Configure(EntityTypeBuilder<PlacementQuestion> question)
    {
        question.ToTable("placement_questions");
        question.HasKey(q => q.Id);
        question.Property(q => q.Id).ValueGeneratedNever();
        question.Property(q => q.Skill).HasConversion<string>().HasMaxLength(16);
        question.Property(q => q.Level).HasConversion<string>().HasMaxLength(2);
        question.Property(q => q.Prompt).HasMaxLength(PlacementQuestion.MaxPromptLength);
        question.Property(q => q.Passage).HasMaxLength(PlacementQuestion.MaxPassageLength);
        question.PrimitiveCollection(q => q.Options);
        question.HasIndex(q => new { q.Skill, q.Level, q.IsActive });
    }
}

internal sealed class PlacementAttemptConfiguration : IEntityTypeConfiguration<PlacementAttempt>
{
    public void Configure(EntityTypeBuilder<PlacementAttempt> attempt)
    {
        attempt.ToTable("placement_attempts");
        attempt.HasKey(a => a.Id);
        attempt.Property(a => a.Id).ValueGeneratedNever();
        attempt.Property<string>("AnswersJson").HasColumnName("answers").HasColumnType("jsonb");
        attempt.Property(a => a.CurrentJson).HasColumnName("current_question").HasColumnType("jsonb");
        attempt.Property(a => a.ResultJson).HasColumnName("result").HasColumnType("jsonb");
        attempt.Ignore(a => a.Answers);
        attempt.Ignore(a => a.Current);
        attempt.Ignore(a => a.IsFinished);
        attempt.HasIndex(a => new { a.UserId, a.StartedAt });
    }
}

internal sealed class SkillLevelConfiguration : IEntityTypeConfiguration<SkillLevel>
{
    public void Configure(EntityTypeBuilder<SkillLevel> level)
    {
        level.ToTable("skill_levels");
        level.HasKey(l => new { l.UserId, l.Skill });
        level.Property(l => l.Skill).HasConversion<string>().HasMaxLength(16);
        level.Property(l => l.Level).HasConversion<string>().HasMaxLength(2);
        level.Property(l => l.Source).HasMaxLength(16);
    }
}
