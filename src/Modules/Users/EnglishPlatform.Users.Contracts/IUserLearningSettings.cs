namespace EnglishPlatform.Users.Contracts;

/// <summary>Настройки, которые нужны другим модулям при сборке занятия.</summary>
public sealed record UserLearningSettings(Guid UserId, int DailyNewWords, int DailyReviews, string TimeZone);

public interface IUserLearningSettings
{
    /// <summary>Настройки пользователя; если профиля ещё нет, возвращаются значения по умолчанию.</summary>
    Task<UserLearningSettings> GetAsync(Guid userId, CancellationToken cancellationToken);
}
