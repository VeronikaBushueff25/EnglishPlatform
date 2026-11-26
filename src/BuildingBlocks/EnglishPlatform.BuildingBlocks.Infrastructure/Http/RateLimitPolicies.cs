namespace EnglishPlatform.BuildingBlocks.Infrastructure.Http;

public static class RateLimitPolicies
{
    /// <summary>Вход, регистрация, сброс пароля: ограничение по IP против перебора.</summary>
    public const string Auth = "auth";

    /// <summary>Поиск во внешнем словаре: ограничение на пользователя, чтобы не завалить источник запросами.</summary>
    public const string Dictionary = "dictionary";
}
