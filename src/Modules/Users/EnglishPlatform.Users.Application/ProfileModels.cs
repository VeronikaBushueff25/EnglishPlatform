using System.ComponentModel.DataAnnotations;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.Users.Domain;

namespace EnglishPlatform.Users.Application;

public sealed record MeResponse(
    Guid Id,
    string Email,
    IReadOnlyCollection<string> Roles,
    string? DisplayName,
    string TimeZone,
    SubscriptionPlan Plan,
    LearningSettingsDto Settings);

public sealed record LearningSettingsDto(
    InterfaceLanguage InterfaceLanguage,
    IReadOnlyCollection<LearningGoal> Goals,
    int DailyNewWords,
    int DailyReviews);

public sealed record UpdateMeRequest(
    [property: MaxLength(UserProfile.MaxDisplayNameLength, ErrorMessage = "Имя не длиннее 64 символов")]
    string? DisplayName,
    [property: Required(ErrorMessage = "Укажите часовой пояс")]
    [property: MaxLength(64)]
    string TimeZone);

public sealed record UpdateSettingsRequest(
    InterfaceLanguage InterfaceLanguage,
    [property: Required]
    [property: MaxLength(10)]
    IReadOnlyCollection<LearningGoal> Goals,
    [property: Range(UserProfile.MinDailyNewWords, UserProfile.MaxDailyNewWords, ErrorMessage = "От 1 до 100 новых слов в день")]
    int DailyNewWords,
    [property: Range(UserProfile.MinDailyReviews, UserProfile.MaxDailyReviews, ErrorMessage = "От 10 до 1000 повторений в день")]
    int DailyReviews);

public static class ProfileErrors
{
    public static readonly Error UnknownTimeZone =
        Error.Validation("profile.unknown_time_zone", "Неизвестный часовой пояс", new Dictionary<string, string[]>
        {
            ["timeZone"] = ["Неизвестный часовой пояс"],
        });
}
