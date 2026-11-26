using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Vocabulary.Domain;

/// <summary>
/// Текст для чтения: короткая статья или рассказ своего уровня. Слова в тексте кликабельны —
/// перевод, транскрипция и «в мои слова» прямо при чтении.
/// </summary>
public sealed class ReadingText : Entity<Guid>
{
    public const int MaxTitleLength = 160;
    public const int MaxTopicLength = 64;
    public const int MaxBodyLength = 20000;

    private ReadingText()
    {
        Title = string.Empty;
        Body = string.Empty;
    }

    public string Title { get; private set; }

    public CefrLevel Level { get; private set; }

    public string? Topic { get; private set; }

    /// <summary>Текст; пустая строка — новый абзац.</summary>
    public string Body { get; private set; }

    public int WordCount { get; private set; }

    public bool IsPublished { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ReadingText Create(ReadingTextData data, DateTimeOffset now)
    {
        var text = new ReadingText { Id = Guid.CreateVersion7() };
        text.Update(data, now);
        return text;
    }

    public void Update(ReadingTextData data, DateTimeOffset now)
    {
        Title = TextNormalizer.Clean(data.Title);
        Level = data.Level;
        Topic = TextNormalizer.NullIfEmpty(data.Topic);
        Body = data.Body.Replace("\r\n", "\n").Trim();
        WordCount = Body.Split((char[])[' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        IsPublished = data.IsPublished;
        UpdatedAt = now;
    }
}

public sealed record ReadingTextData(string Title, CefrLevel Level, string? Topic, string Body, bool IsPublished);
