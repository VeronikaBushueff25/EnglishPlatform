using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Vocabulary.Domain;

/// <summary>Тематическая категория общей базы лексики («Еда», «Путешествия»).</summary>
public sealed class Category : Entity<Guid>
{
    public const int MaxNameLength = 64;
    public const int MaxSlugLength = 64;
    public const int MaxDescriptionLength = 256;

    private Category()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    private Category(Guid id)
        : base(id)
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    public string Name { get; private set; }

    /// <summary>Латинский идентификатор для ссылок и импорта CSV, например <c>food</c>.</summary>
    public string Slug { get; private set; }

    public string? Description { get; private set; }

    public static Category Create(string name, string slug, string? description)
    {
        var category = new Category(Guid.CreateVersion7());
        category.Update(name, slug, description);
        return category;
    }

    public void Update(string name, string slug, string? description)
    {
        Name = TextNormalizer.Clean(name);
        Slug = TextNormalizer.Normalize(slug);
        Description = TextNormalizer.NullIfEmpty(description);
    }
}
