using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.Grammar.Application;
using EnglishPlatform.Grammar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishPlatform.Grammar.Infrastructure.Persistence;

public sealed class GrammarDb(DbContextOptions<GrammarDb> options) : ModuleDbContext(options), IGrammarDb
{
    public const string SchemaName = "grammar";

    public override string Schema => SchemaName;

    public DbSet<GrammarTopic> Topics => Set<GrammarTopic>();

    public DbSet<ErrorTagInfo> ErrorTags => Set<ErrorTagInfo>();
}

internal sealed class GrammarTopicConfiguration : IEntityTypeConfiguration<GrammarTopic>
{
    public void Configure(EntityTypeBuilder<GrammarTopic> topic)
    {
        topic.ToTable("grammar_topics");
        topic.HasKey(t => t.Id);
        topic.Property(t => t.Id).ValueGeneratedNever();
        topic.Property(t => t.Slug).HasMaxLength(GrammarTopic.MaxSlugLength);
        topic.Property(t => t.Title).HasMaxLength(GrammarTopic.MaxTitleLength);
        topic.Property(t => t.Level).HasMaxLength(2);
        topic.Property(t => t.Summary).HasMaxLength(GrammarTopic.MaxSummaryLength);
        topic.Property(t => t.Body).HasMaxLength(GrammarTopic.MaxBodyLength);
        topic.OwnsMany(t => t.Examples, e => e.ToJson());
        topic.HasIndex(t => t.Slug).IsUnique();
    }
}

internal sealed class ErrorTagConfiguration : IEntityTypeConfiguration<ErrorTagInfo>
{
    public void Configure(EntityTypeBuilder<ErrorTagInfo> tag)
    {
        tag.ToTable("error_tags");
        tag.HasKey(t => t.Code);
        tag.Property(t => t.Code).HasMaxLength(32);
        tag.Property(t => t.Title).HasMaxLength(ErrorTagInfo.MaxTitleLength);
        tag.Property(t => t.Description).HasMaxLength(ErrorTagInfo.MaxDescriptionLength);
        tag.HasOne<GrammarTopic>().WithMany().HasForeignKey(t => t.TopicId).OnDelete(DeleteBehavior.SetNull);
    }
}
