using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.SpacedRepetition.Application;
using EnglishPlatform.SpacedRepetition.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishPlatform.SpacedRepetition.Infrastructure.Persistence;

public sealed class SpacedRepetitionDb(DbContextOptions<SpacedRepetitionDb> options) : ModuleDbContext(options), ISpacedRepetitionDb
{
    public const string SchemaName = "spaced_repetition";

    public override string Schema => SchemaName;

    public DbSet<ReviewCard> Cards => Set<ReviewCard>();

    public DbSet<ReviewLog> Logs => Set<ReviewLog>();
}

internal sealed class ReviewCardConfiguration : IEntityTypeConfiguration<ReviewCard>
{
    public void Configure(EntityTypeBuilder<ReviewCard> card)
    {
        card.ToTable("review_cards");
        card.HasKey(c => c.Id);
        card.Property(c => c.Id).ValueGeneratedNever();
        card.Property(c => c.Mode).HasConversion<string>().HasMaxLength(16);
        card.Property(c => c.State).HasConversion<string>().HasMaxLength(16);
        card.Ignore(c => c.Memory);

        card.HasIndex(c => new { c.UserWordId, c.Mode }).IsUnique();
        card.HasIndex(c => new { c.UserId, c.Due });
        card.HasIndex(c => new { c.UserId, c.State, c.CreatedAt });
    }
}

internal sealed class ReviewLogConfiguration : IEntityTypeConfiguration<ReviewLog>
{
    public void Configure(EntityTypeBuilder<ReviewLog> log)
    {
        log.ToTable("review_logs");
        log.HasKey(l => l.Id);
        log.Property(l => l.Id).ValueGeneratedNever();
        log.Property(l => l.Mode).HasConversion<string>().HasMaxLength(16);
        log.Property(l => l.Rating).HasConversion<string>().HasMaxLength(8);
        log.Property(l => l.StateBefore).HasConversion<string>().HasMaxLength(16);
        log.Property(l => l.StateAfter).HasConversion<string>().HasMaxLength(16);

        // Журнал переживает карточку (слово удалили — история осталась), поэтому без внешнего ключа.
        log.HasIndex(l => new { l.UserId, l.ReviewedAt });
        log.HasIndex(l => l.CardId);
    }
}
