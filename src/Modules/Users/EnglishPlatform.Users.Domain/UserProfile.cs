using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Users.Domain;

public enum InterfaceLanguage
{
    Ru,
    En,
}

public enum LearningGoal
{
    Conversation,
    Travel,
    Work,
    Exams,
    Reading,
    MoviesAndSeries,
    Writing,
}

public enum SubscriptionPlan
{
    Free,
    Premium,
}

/// <summary>Профиль и настройки обучения. Ключ совпадает с идентификатором пользователя из Identity.</summary>
public sealed class UserProfile : Entity<Guid>
{
    public const int MinDailyNewWords = 1;
    public const int MaxDailyNewWords = 100;
    public const int MinDailyReviews = 10;
    public const int MaxDailyReviews = 1000;
    public const int MaxDisplayNameLength = 64;

    private UserProfile()
    {
        TimeZone = DefaultTimeZone;
        Goals = [];
    }

    private UserProfile(Guid userId, DateTimeOffset now)
        : base(userId)
    {
        InterfaceLanguage = InterfaceLanguage.Ru;
        Goals = [];
        DailyNewWords = 10;
        DailyReviews = 100;
        TimeZone = DefaultTimeZone;
        Plan = SubscriptionPlan.Free;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static string DefaultTimeZone => "Europe/Moscow";

    public string? DisplayName { get; private set; }

    public InterfaceLanguage InterfaceLanguage { get; private set; }

    public List<LearningGoal> Goals { get; private set; }

    public int DailyNewWords { get; private set; }

    public int DailyReviews { get; private set; }

    /// <summary>Часовой пояс IANA: по нему считается, когда начинается новый учебный день.</summary>
    public string TimeZone { get; private set; }

    /// <summary>Тариф. В MVP всегда Free, поле заложено под будущую подписку.</summary>
    public SubscriptionPlan Plan { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserProfile CreateDefault(Guid userId, DateTimeOffset now) => new(userId, now);

    public void UpdateProfile(string? displayName, string timeZone, DateTimeOffset now)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        TimeZone = timeZone;
        UpdatedAt = now;
    }

    public void UpdateSettings(
        InterfaceLanguage language,
        IEnumerable<LearningGoal> goals,
        int dailyNewWords,
        int dailyReviews,
        DateTimeOffset now)
    {
        InterfaceLanguage = language;
        Goals = goals.Distinct().Order().ToList();
        DailyNewWords = Math.Clamp(dailyNewWords, MinDailyNewWords, MaxDailyNewWords);
        DailyReviews = Math.Clamp(dailyReviews, MinDailyReviews, MaxDailyReviews);
        UpdatedAt = now;
    }
}
