using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.Vocabulary.Application;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishPlatform.Vocabulary.Infrastructure.Persistence;

public sealed class VocabularyDb(DbContextOptions<VocabularyDb> options) : ModuleDbContext(options), IVocabularyDb
{
    public const string SchemaName = "vocabulary";

    public override string Schema => SchemaName;

    public DbSet<Lexeme> Lexemes => Set<Lexeme>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<UserWord> UserWords => Set<UserWord>();
}

internal sealed class LexemeConfiguration : IEntityTypeConfiguration<Lexeme>
{
    public void Configure(EntityTypeBuilder<Lexeme> lexeme)
    {
        lexeme.ToTable("lexemes");
        lexeme.HasKey(l => l.Id);
        lexeme.Property(l => l.Id).ValueGeneratedNever();
        lexeme.Property(l => l.Text).HasMaxLength(Lexeme.MaxTextLength);
        lexeme.Property(l => l.NormalizedText).HasMaxLength(Lexeme.MaxTextLength);
        lexeme.Property(l => l.SearchText);
        lexeme.Property(l => l.Type).HasConversion<string>().HasMaxLength(16);
        lexeme.Property(l => l.PartOfSpeech).HasConversion<string>().HasMaxLength(16);
        lexeme.Property(l => l.Level).HasConversion<string>().HasMaxLength(2);
        lexeme.Property(l => l.Transcription).HasMaxLength(Lexeme.MaxTranscriptionLength);
        lexeme.Property(l => l.AudioUrl).HasMaxLength(Lexeme.MaxAudioUrlLength);
        lexeme.PrimitiveCollection(l => l.CategoryIds);

        // Переводы и примеры читаются и пишутся только вместе со словом — храним их в jsonb.
        lexeme.OwnsMany(l => l.Translations, t => t.ToJson());
        lexeme.OwnsMany(l => l.Examples, e => e.ToJson());

        // Одно и то же слово может быть разными частями речи (run — глагол и существительное), но не дважды одной.
        lexeme.HasIndex(l => new { l.NormalizedText, l.PartOfSpeech }).IsUnique().AreNullsDistinct(false);
        lexeme.HasIndex(l => l.CategoryIds).HasMethod("gin");
        lexeme.HasIndex(l => l.Level);
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> category)
    {
        category.ToTable("categories");
        category.HasKey(c => c.Id);
        category.Property(c => c.Id).ValueGeneratedNever();
        category.Property(c => c.Name).HasMaxLength(Category.MaxNameLength);
        category.Property(c => c.Slug).HasMaxLength(Category.MaxSlugLength);
        category.Property(c => c.Description).HasMaxLength(Category.MaxDescriptionLength);
        category.HasIndex(c => c.Slug).IsUnique();
    }
}

internal sealed class UserWordConfiguration : IEntityTypeConfiguration<UserWord>
{
    public void Configure(EntityTypeBuilder<UserWord> word)
    {
        word.ToTable("user_words");
        word.HasKey(w => w.Id);
        word.Property(w => w.Id).ValueGeneratedNever();
        word.Property(w => w.Text).HasMaxLength(Lexeme.MaxTextLength);
        word.Property(w => w.NormalizedText).HasMaxLength(Lexeme.MaxTextLength);
        word.Property(w => w.Type).HasConversion<string>().HasMaxLength(16);
        word.Property(w => w.Transcription).HasMaxLength(Lexeme.MaxTranscriptionLength);
        word.Property(w => w.Level).HasConversion<string>().HasMaxLength(2);
        word.Property(w => w.PartOfSpeech).HasConversion<string>().HasMaxLength(16);
        word.Property(w => w.Example).HasMaxLength(Lexeme.MaxExampleLength);
        word.Property(w => w.ExampleTranslation).HasMaxLength(Lexeme.MaxExampleLength);
        word.Property(w => w.Note).HasMaxLength(UserWord.MaxNoteLength);
        word.PrimitiveCollection(w => w.Translations);

        // Слово общей базы — не больше одного раза в словаре; свои слова — без повторов по тексту.
        word.HasIndex(w => new { w.UserId, w.LexemeId }).IsUnique().HasFilter("lexeme_id IS NOT NULL");
        word.HasIndex(w => new { w.UserId, w.NormalizedText }).IsUnique().HasFilter("lexeme_id IS NULL");
        word.HasIndex(w => new { w.UserId, w.AddedAt });
        word.HasIndex(w => w.LexemeId);
    }
}
