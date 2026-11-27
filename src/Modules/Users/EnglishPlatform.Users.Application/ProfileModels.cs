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
    LearningSettingsDto Settings,
    bool Supporter = false);

/// <summary>
/// «Поддержать проект»: ссылки на сервисы донатов и необязательная цель сбора. Задаются в настройках сервера
/// (раздел Support), чтобы менять их без пересборки. Пустая ссылка — кнопка не показывается.
/// </summary>
public sealed class SupportOptions
{
    public string? CloudTipsUrl { get; set; }

    public string? BoostyUrl { get; set; }

    /// <summary>На что сейчас собираем, например «Разговорный партнёр на ИИ».</summary>
    public string? GoalTitle { get; set; }

    /// <summary>Сумма цели в рублях (строкой: пустое значение из переменной окружения — «цели нет»).</summary>
    public string? GoalAmount { get; set; }

    /// <summary>Сколько уже собрано — обновляется вручную.</summary>
    public string? GoalRaised { get; set; }
}

public sealed record SupportInfoDto(string? CloudTipsUrl, string? BoostyUrl, string? GoalTitle, int? GoalAmount, int? GoalRaised);

public sealed record LearningSettingsDto(
    InterfaceLanguage InterfaceLanguage,
    IReadOnlyCollection<LearningGoal> Goals,
    int DailyNewWords,
    int DailyReviews,
    bool AutoAddWords = true);

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
    int DailyReviews,
    bool? AutoAddWords = null);

public static class ProfileErrors
{
    public static readonly Error UnknownTimeZone =
        Error.Validation("profile.unknown_time_zone", "Неизвестный часовой пояс", new Dictionary<string, string[]>
        {
            ["timeZone"] = ["Неизвестный часовой пояс"],
        });
}
