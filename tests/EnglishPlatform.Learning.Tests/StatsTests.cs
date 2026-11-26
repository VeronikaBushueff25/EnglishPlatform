using EnglishPlatform.Analytics.Application;
using Shouldly;

namespace EnglishPlatform.Learning.Tests;

public sealed class StatsTests
{
    private static readonly DateOnly Today = new(2026, 11, 26);

    private static HashSet<DateOnly> Days(params int[] daysAgo) => daysAgo.Select(d => Today.AddDays(-d)).ToHashSet();

    [Fact]
    public void Streak_counts_consecutive_days_up_to_today()
    {
        StatsService.Streaks(Days(0, 1, 2, 5, 6), Today).ShouldBe((3, 3));
    }

    [Fact]
    public void Streak_is_kept_until_the_end_of_the_day_if_today_is_not_done_yet()
    {
        StatsService.Streaks(Days(1, 2), Today).ShouldBe((2, 2));
        StatsService.Streaks(Days(2, 3), Today).Current.ShouldBe(0);
    }

    [Fact]
    public void Best_streak_is_the_longest_run_ever()
    {
        StatsService.Streaks(Days(0, 10, 11, 12, 13), Today).ShouldBe((1, 4));
        StatsService.Streaks(new HashSet<DateOnly>(), Today).ShouldBe((0, 0));
    }

    [Fact]
    public void Today_follows_the_users_time_zone()
    {
        var now = new DateTimeOffset(2026, 11, 26, 22, 30, 0, TimeSpan.Zero);

        StatsService.Today(now, "Europe/Moscow").ShouldBe(new DateOnly(2026, 11, 27));
        StatsService.Today(now, "America/New_York").ShouldBe(new DateOnly(2026, 11, 26));
    }
}
