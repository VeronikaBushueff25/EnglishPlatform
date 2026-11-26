using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.Exercises.Application;
using EnglishPlatform.Exercises.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishPlatform.Exercises.Infrastructure.Persistence;

public sealed class ExercisesDb(DbContextOptions<ExercisesDb> options) : ModuleDbContext(options), IExercisesDb
{
    public const string SchemaName = "exercises";

    public override string Schema => SchemaName;

    public DbSet<ExerciseAttempt> Attempts => Set<ExerciseAttempt>();
}

internal sealed class ExerciseAttemptConfiguration : IEntityTypeConfiguration<ExerciseAttempt>
{
    public void Configure(EntityTypeBuilder<ExerciseAttempt> attempt)
    {
        attempt.ToTable("exercise_attempts");
        attempt.HasKey(a => a.Id);
        attempt.Property(a => a.Id).ValueGeneratedNever();
        attempt.Property(a => a.Kind).HasMaxLength(32);
        attempt.Property(a => a.TaskSnapshot).HasColumnType("jsonb");
        attempt.Property(a => a.Answer).HasMaxLength(512);
        attempt.Property(a => a.Verdict).HasMaxLength(16);
        attempt.Property(a => a.ErrorTag).HasMaxLength(32);
        attempt.Property(a => a.CorrectAnswer).HasMaxLength(512);
        attempt.Property(a => a.Explanation).HasMaxLength(1000);

        attempt.HasIndex(a => new { a.UserId, a.AnsweredAt });
        attempt.HasIndex(a => a.SessionId);
        attempt.HasIndex(a => new { a.UserId, a.ErrorTag });
    }
}
