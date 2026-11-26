using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Grammar.Domain;

/// <summary>Пример к правилу. <c>IsWrong</c> — так говорить нельзя (показывается зачёркнутым рядом с правильным).</summary>
public sealed class GrammarExample(string text, string? translation, bool isWrong)
{
    public string Text { get; private set; } = text;

    public string? Translation { get; private set; } = translation;

    public bool IsWrong { get; private set; } = isWrong;
}

/// <summary>Грамматическая тема: короткое правило для разбора ошибки и подробное объяснение с примерами.</summary>
public sealed class GrammarTopic : Entity<Guid>
{
    public const int MaxSlugLength = 64;
    public const int MaxTitleLength = 128;
    public const int MaxSummaryLength = 600;
    public const int MaxBodyLength = 8000;
    public const int MaxExamples = 20;

    private GrammarTopic()
    {
        Slug = string.Empty;
        Title = string.Empty;
        Summary = string.Empty;
        Body = string.Empty;
        Examples = [];
    }

    public string Slug { get; private set; }

    public string Title { get; private set; }

    /// <summary>Уровень CEFR, с которого тема нужна (A1–C2), строкой.</summary>
    public string? Level { get; private set; }

    /// <summary>Правило в двух-трёх предложениях — его показывают сразу после ошибки.</summary>
    public string Summary { get; private set; }

    /// <summary>Подробное объяснение: абзацы через пустую строку.</summary>
    public string Body { get; private set; }

    public List<GrammarExample> Examples { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static GrammarTopic Create(GrammarTopicData data, DateTimeOffset now)
    {
        var topic = new GrammarTopic { Id = Guid.CreateVersion7() };
        topic.Update(data, now);
        return topic;
    }

    public void Update(GrammarTopicData data, DateTimeOffset now)
    {
        Slug = data.Slug.Trim().ToLowerInvariant();
        Title = data.Title.Trim();
        Level = string.IsNullOrWhiteSpace(data.Level) ? null : data.Level.Trim().ToUpperInvariant();
        Summary = data.Summary.Trim();
        Body = data.Body.Trim().Replace("\r\n", "\n");
        Examples = data.Examples.Where(e => !string.IsNullOrWhiteSpace(e.Text)).Take(MaxExamples).ToList();
        SortOrder = data.SortOrder;
        UpdatedAt = now;
    }
}

public sealed record GrammarTopicData(
    string Slug,
    string Title,
    string? Level,
    string Summary,
    string Body,
    IReadOnlyList<GrammarExample> Examples,
    int SortOrder);

/// <summary>Тип ошибки из проверки ответа (Typo, Article…) с человеческим названием и ссылкой на тему.</summary>
public sealed class ErrorTagInfo
{
    public const int MaxTitleLength = 64;
    public const int MaxDescriptionLength = 300;

    private ErrorTagInfo()
    {
        Code = string.Empty;
        Title = string.Empty;
        Description = string.Empty;
    }

    public string Code { get; private set; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public Guid? TopicId { get; private set; }

    public static ErrorTagInfo Create(string code, string title, string description, Guid? topicId) => new()
    {
        Code = code,
        Title = title.Trim(),
        Description = description.Trim(),
        TopicId = topicId,
    };

    public void Update(string title, string description, Guid? topicId)
    {
        Title = title.Trim();
        Description = description.Trim();
        TopicId = topicId;
    }
}
