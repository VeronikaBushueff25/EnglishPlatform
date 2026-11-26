using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Vocabulary.Domain;

/// <summary>
/// Ответ внешнего словаря на запрос слова: сохраняем, чтобы не спрашивать одно слово дважды.
/// <c>Found = false</c> — слова в словаре нет (такие ответы живут недолго, словарь пополняется).
/// </summary>
public sealed class DictionaryCacheEntry : Entity<string>
{
    public const int MaxKeyLength = 128;

    private DictionaryCacheEntry()
    {
        Payload = string.Empty;
    }

    public string Payload { get; private set; }

    public bool Found { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; }

    public static DictionaryCacheEntry Create(string key, string payload, bool found, DateTimeOffset now) =>
        new() { Id = key, Payload = payload, Found = found, FetchedAt = now };

    public void Refresh(string payload, bool found, DateTimeOffset now)
    {
        Payload = payload;
        Found = found;
        FetchedAt = now;
    }
}
