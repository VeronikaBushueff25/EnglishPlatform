namespace EnglishPlatform.Users.Contracts;

/// <summary>Настройки, которые нужны другим модулям при сборке занятия.</summary>
/// <summary><c>Goals</c> — цели обучения строками (Conversation, Travel, Work…), нужны для подбора стартовых слов.</summary>
public sealed record UserLearningSettings(Guid UserId, int DailyNewWords, int DailyReviews, string TimeZone)
{
    public IReadOnlyList<string> Goals { get; init; } = [];

    /// <summary>Добавлять слова своего уровня из общей базы, когда свои новые слова закончились.</summary>
    public bool AutoAddWords { get; init; } = true;

    /// <summary>Поддержал проект — питомцу открыто сердечко.</summary>
    public bool Supporter { get; init; }
}

public interface IUserLearningSettings
{
    /// <summary>Настройки пользователя; если профиля ещё нет, возвращаются значения по умолчанию.</summary>
    Task<UserLearningSettings> GetAsync(Guid userId, CancellationToken cancellationToken);
}
