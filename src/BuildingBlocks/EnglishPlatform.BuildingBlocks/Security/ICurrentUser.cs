namespace EnglishPlatform.BuildingBlocks.Security;

/// <summary>Пользователь текущего запроса. Данные берутся из access-токена.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Идентификатор пользователя. Бросает исключение для анонимного запроса.</summary>
    Guid UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }
}

public static class Roles
{
    public const string Admin = "admin";
    public const string User = "user";

    public static IReadOnlyCollection<string> All { get; } = [Admin, User];
}

/// <summary>Имена claim в access-токене. Совпадают с JWT, чтобы токен читался без сопоставлений.</summary>
public static class AppClaims
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Role = "role";
}

public static class AppPolicies
{
    public const string Admin = "admin";
}
