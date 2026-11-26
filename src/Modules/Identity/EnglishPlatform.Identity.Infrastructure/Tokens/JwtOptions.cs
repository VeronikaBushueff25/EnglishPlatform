using System.ComponentModel.DataAnnotations;

namespace EnglishPlatform.Identity.Infrastructure.Tokens;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "english-platform";

    [Required]
    public string Audience { get; set; } = "english-platform";

    /// <summary>Ключ HMAC-SHA256, не короче 32 байт. В репозитории его нет: user-secrets или переменная Jwt__SigningKey.</summary>
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey должен быть не короче 32 символов")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;
}

public sealed class IdentityModuleOptions
{
    public const string Section = "Identity";

    /// <summary>Адрес фронтенда, из него строится ссылка в письме сброса пароля.</summary>
    public string PublicUrl { get; set; } = "http://localhost:4200";

    /// <summary>Первый администратор. Создаётся при старте, если пользователя с таким email ещё нет.</summary>
    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }
}
