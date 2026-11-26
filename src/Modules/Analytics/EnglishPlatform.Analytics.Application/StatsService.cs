using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.LearningProfiles.Contracts;
using EnglishPlatform.SpacedRepetition.Contracts;
using EnglishPlatform.Users.Contracts;
using EnglishPlatform.Vocabulary.Contracts;

namespace EnglishPlatform.Analytics.Application;

public sealed class StatsOptions
{
    public const string Section = "Statistics";

    /// <summary>
    /// Слово активное, если карточка вспоминания выучена со стабильностью не меньше стольких дней:
    /// пользователь вспоминает его сам и через три недели. Порог из плана («выживаемость на несколько недель»).
    /// </summary>
    public double ActiveStabilityDays { get; set; } = 21;

    public int ChartDays { get; set; } = 30;
}

public sealed record DayStatDto(DateOnly Day, int Reviews, int Successful, int NewWords);

public sealed record SkillStatDto(string Skill, string Level, DateTimeOffset AssessedAt);

/// <summary>
/// Сводка для страницы статистики: путь слов «новые → учатся → узнаю → использую», серия дней,
/// активность и точность за неделю, ответы по дням за месяц, уровни по навыкам.
/// </summary>
public sealed record StatsSummaryDto(
    int TotalWords,
    int NotStarted,
    int Learning,
    int Passive,
    int Active,
    double ActiveThresholdDays,
    int CurrentStreak,
    int BestStreak,
    bool StudiedToday,
    int ReviewsToday,
    int ReviewsWeek,
    double AccuracyWeek,
    int NewWordsWeek,
    int MinutesWeek,
    IReadOnlyList<DayStatDto> Days,
    IReadOnlyList<SkillStatDto> Skills);

public sealed class StatsService(
    IReviewStats reviews,
    IUserWordsReader words,
    IUserLearningSettings settings,
    ISkillLevelsReader levels,
    ICurrentUser currentUser,
    StatsOptions options,
    TimeProvider time)
{
    public async Task<StatsSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        Guid userId = currentUser.UserId;
        DateTimeOffset now = time.GetUtcNow();
        UserLearningSettings s = await settings.GetAsync(userId, cancellationToken);
        DateOnly today = Today(now, s.TimeZone);

        int total = (await words.ListIdsAsync(userId, cancellationToken)).Count;
        VocabularyProgress progress = await reviews.GetProgressAsync(userId, options.ActiveStabilityDays, cancellationToken);
        IReadOnlyList<DailyReviews> year = await reviews.GetDailyAsync(userId, now.AddDays(-366), s.TimeZone, cancellationToken);
        IReadOnlyList<SkillLevelInfo> skills = await levels.GetAsync(userId, cancellationToken);

        (int current, int best) = Streaks(year.Select(d => d.Day).ToHashSet(), today);
        List<DailyReviews> week = year.Where(d => d.Day > today.AddDays(-7)).ToList();
        int weekReviews = week.Sum(d => d.Reviews);
        DailyReviews? todayStats = year.FirstOrDefault(d => d.Day == today);

        Dictionary<DateOnly, DailyReviews> byDay = year.ToDictionary(d => d.Day);
        List<DayStatDto> days = Enumerable.Range(0, options.ChartDays)
            .Select(i => today.AddDays(i - options.ChartDays + 1))
            .Select(day => byDay.TryGetValue(day, out DailyReviews? d)
                ? new DayStatDto(day, d.Reviews, d.Successful, d.NewWords)
                : new DayStatDto(day, 0, 0, 0))
            .ToList();

        int started = progress.Learning + progress.Passive + progress.Active;
        return new StatsSummaryDto(
            total,
            Math.Max(0, total - started),
            progress.Learning,
            progress.Passive,
            progress.Active,
            options.ActiveStabilityDays,
            current,
            best,
            todayStats is not null,
            todayStats?.Reviews ?? 0,
            weekReviews,
            weekReviews == 0 ? 0 : Math.Round((double)week.Sum(d => d.Successful) / weekReviews, 3),
            week.Sum(d => d.NewWords),
            (int)Math.Round(week.Sum(d => d.AnswerMs) / 60000.0),
            days,
            skills.Select(l => new SkillStatDto(l.Skill, l.Level, l.AssessedAt)).ToList());
    }

    /// <summary>
    /// Серия — дни подряд с хотя бы одним ответом. Если сегодня ещё не занимались, серия не прерывается до конца дня:
    /// считаем от вчера.
    /// </summary>
    public static (int Current, int Best) Streaks(IReadOnlySet<DateOnly> activeDays, DateOnly today)
    {
        int current = 0;
        DateOnly day = activeDays.Contains(today) ? today : today.AddDays(-1);
        while (activeDays.Contains(day))
        {
            current++;
            day = day.AddDays(-1);
        }

        int best = 0, run = 0;
        DateOnly? previous = null;
        foreach (DateOnly d in activeDays.Order())
        {
            run = previous is { } p && p.AddDays(1) == d ? run + 1 : 1;
            best = Math.Max(best, run);
            previous = d;
        }

        return (current, Math.Max(best, current));
    }

    public static DateOnly Today(DateTimeOffset now, string timeZone)
    {
        TimeZoneInfo zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out TimeZoneInfo? found) ? found : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }
}
