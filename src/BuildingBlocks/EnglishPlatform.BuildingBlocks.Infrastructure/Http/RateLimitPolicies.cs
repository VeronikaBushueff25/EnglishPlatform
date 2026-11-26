namespace EnglishPlatform.BuildingBlocks.Infrastructure.Http;

public static class RateLimitPolicies
{
    /// <summary>Вход, регистрация, сброс пароля: ограничение по IP против перебора.</summary>
    public const string Auth = "auth";
}
