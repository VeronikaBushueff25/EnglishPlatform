using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Analytics.Domain;

/// <summary>Что считаем: все ответы, тип ошибки, формат задания или конкретное слово.</summary>
public enum StatDimension
{
    Total,
    Tag,
    Format,
    Word,
}

/// <summary>
/// Счётчик ответов и ошибок пользователя в одном разрезе (error_stats из плана).
/// Агрегат: его можно пересобрать из журнала попыток Exercises.
/// </summary>
public sealed class ErrorStat : Entity<Guid>
{
    private ErrorStat()
    {
        Key = string.Empty;
    }

    public Guid UserId { get; private set; }

    public StatDimension Dimension { get; private set; }

    /// <summary>Код тега, формат задания, id слова или «all».</summary>
    public string Key { get; private set; }

    public int Attempts { get; private set; }

    public int Errors { get; private set; }

    public int Almost { get; private set; }

    public DateTimeOffset LastAnsweredAt { get; private set; }

    public DateTimeOffset? LastErrorAt { get; private set; }

    public static ErrorStat Create(Guid userId, StatDimension dimension, string key) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        Dimension = dimension,
        Key = key,
    };

    public void Count(bool wrong, bool almost, DateTimeOffset at)
    {
        Attempts++;
        if (wrong)
        {
            Errors++;
            LastErrorAt = at;
        }
        else if (almost)
        {
            Almost++;
            LastErrorAt = at;
        }

        if (at > LastAnsweredAt)
        {
            LastAnsweredAt = at;
        }
    }

    /// <summary>Доля неудачных ответов: ошибка — 1, «почти» — 0,5.</summary>
    public double ErrorRate => Attempts == 0 ? 0 : (Errors + Almost * 0.5) / Attempts;
}
