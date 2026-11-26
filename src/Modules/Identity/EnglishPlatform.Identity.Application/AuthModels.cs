using System.ComponentModel.DataAnnotations;

namespace EnglishPlatform.Identity.Application;

public sealed record RegisterRequest(
    [property: Required(ErrorMessage = "Укажите email")]
    [property: EmailAddress(ErrorMessage = "Некорректный email")]
    [property: MaxLength(256)]
    string Email,
    [property: Required(ErrorMessage = "Укажите пароль")]
    [property: MinLength(8, ErrorMessage = "Пароль не короче 8 символов")]
    [property: MaxLength(128)]
    string Password);

public sealed record LoginRequest(
    [property: Required(ErrorMessage = "Укажите email")]
    [property: MaxLength(256)]
    string Email,
    [property: Required(ErrorMessage = "Укажите пароль")]
    [property: MaxLength(128)]
    string Password);

public sealed record ForgotPasswordRequest(
    [property: Required(ErrorMessage = "Укажите email")]
    [property: EmailAddress(ErrorMessage = "Некорректный email")]
    [property: MaxLength(256)]
    string Email);

public sealed record ResetPasswordRequest(
    [property: Required(ErrorMessage = "Укажите email")]
    [property: EmailAddress(ErrorMessage = "Некорректный email")]
    [property: MaxLength(256)]
    string Email,
    [property: Required(ErrorMessage = "Нет кода сброса")]
    [property: MaxLength(2048)]
    string Token,
    [property: Required(ErrorMessage = "Укажите новый пароль")]
    [property: MinLength(8, ErrorMessage = "Пароль не короче 8 символов")]
    [property: MaxLength(128)]
    string NewPassword);

/// <summary>Ответ входа и обновления. Refresh-токен сюда не попадает: он уходит в httpOnly cookie.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, AuthUser User);

public sealed record AuthUser(Guid Id, string Email, IReadOnlyCollection<string> Roles);

/// <summary>Результат входа для эндпоинта: ответ клиенту и refresh-токен для cookie.</summary>
public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
