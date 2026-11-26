using EnglishPlatform.BuildingBlocks.Results;

namespace EnglishPlatform.Identity.Application;

public interface IAuthService
{
    Task<Result<AuthSession>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<AuthSession>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);

    /// <summary>Всегда успешен, чтобы по ответу нельзя было узнать, зарегистрирован ли email.</summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}

/// <summary>Отправка писем модуля. В MVP письмо пишется в лог; SMTP подключается без правок модуля.</summary>
public interface IAuthEmailSender
{
    Task SendPasswordResetAsync(string email, string resetLink, CancellationToken cancellationToken);
}

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "Неверный email или пароль");

    public static readonly Error LockedOut =
        Error.Unauthorized("auth.locked_out", "Слишком много неудачных попыток. Попробуйте через несколько минут");

    public static readonly Error EmailTaken =
        Error.Conflict("auth.email_taken", "Пользователь с таким email уже зарегистрирован");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("auth.invalid_refresh_token", "Сессия истекла, войдите заново");

    public static readonly Error InvalidResetToken =
        Error.Validation("auth.invalid_reset_token", "Ссылка для сброса пароля недействительна или устарела");

    public static Error WeakPassword(IReadOnlyDictionary<string, string[]> details) =>
        Error.Validation("auth.weak_password", "Пароль не подходит", details);
}
