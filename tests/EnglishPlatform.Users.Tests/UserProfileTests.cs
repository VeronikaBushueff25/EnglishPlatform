using EnglishPlatform.Users.Domain;
using Shouldly;

namespace EnglishPlatform.Users.Tests;

public sealed class UserProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_profile_is_free_russian_with_moderate_load()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), Now);

        profile.Plan.ShouldBe(SubscriptionPlan.Free);
        profile.InterfaceLanguage.ShouldBe(InterfaceLanguage.Ru);
        profile.DailyNewWords.ShouldBe(10);
        profile.DailyReviews.ShouldBe(100);
        profile.Goals.ShouldBeEmpty();
    }

    [Fact]
    public void Settings_are_clamped_and_goals_deduplicated()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), Now);

        profile.UpdateSettings(
            InterfaceLanguage.En,
            [LearningGoal.Work, LearningGoal.Travel, LearningGoal.Work],
            dailyNewWords: 500,
            dailyReviews: 1,
            Now.AddHours(1));

        profile.InterfaceLanguage.ShouldBe(InterfaceLanguage.En);
        profile.Goals.ShouldBe([LearningGoal.Travel, LearningGoal.Work]);
        profile.DailyNewWords.ShouldBe(UserProfile.MaxDailyNewWords);
        profile.DailyReviews.ShouldBe(UserProfile.MinDailyReviews);
        profile.UpdatedAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Blank_display_name_is_stored_as_null()
    {
        var profile = UserProfile.CreateDefault(Guid.NewGuid(), Now);

        profile.UpdateProfile("   ", "Europe/Moscow", Now);

        profile.DisplayName.ShouldBeNull();
    }
}
