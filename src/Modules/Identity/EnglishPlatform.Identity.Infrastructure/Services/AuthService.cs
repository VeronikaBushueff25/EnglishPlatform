using System.Text;
using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Identity.Application;
using EnglishPlatform.Identity.Contracts;
using EnglishPlatform.Identity.Domain;
using EnglishPlatform.Identity.Infrastructure.Persistence;
using EnglishPlatform.Identity.Infrastructure.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishPlatform.Identity.Infrastructure.Services;

internal sealed class AuthService(
    UserManager<AppUser> userManager,
    IdentityDb db,
    TokenIssuer tokenIssuer,
    IEventBus eventBus,
    IAuthEmailSender emailSender,
    IOptions<JwtOptions> jwtOptions,
    IOptions<IdentityModuleOptions> moduleOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private TimeSpan RefreshLifetime => TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays);

    public async Task<Result<AuthSession>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        string email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return AuthErrors.EmailTaken;
        }

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            UserName = email,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        IdentityResult created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return created.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")
                ? AuthErrors.EmailTaken
                : AuthErrors.WeakPassword(ToDetails(created, "password"));
        }

        await userManager.AddToRoleAsync(user, Roles.User);
        logger.LogInformation("Зарегистрирован пользователь {UserId}", user.Id);

        await eventBus.PublishAsync(new UserRegistered(user.Id, email, user.CreatedAt), cancellationToken);

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        AppUser? user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return AuthErrors.InvalidCredentials;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return AuthErrors.LockedOut;
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return await userManager.IsLockedOutAsync(user) ? AuthErrors.LockedOut : AuthErrors.InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<Result<AuthSession>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        string hash = TokenIssuer.Hash(refreshToken);
        RefreshToken? stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        if (stored.WasReplaced)
        {
            // Повторное использование заменённого токена: считаем, что его украли, закрываем все сессии.
            logger.LogWarning("Повторно использован refresh-токен пользователя {UserId}, все сессии отозваны", stored.UserId);
            await RevokeAllAsync(stored.UserId, now, cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        if (!stored.IsActive(now))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        AppUser? user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        string newToken = TokenIssuer.CreateRefreshToken();
        var next = RefreshToken.Issue(user.Id, TokenIssuer.Hash(newToken), now, RefreshLifetime);
        stored.ReplaceWith(next, now);
        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(cancellationToken);

        return await BuildSessionAsync(user, newToken, next.ExpiresAt);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        string hash = TokenIssuer.Hash(refreshToken);
        RefreshToken? stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is not null)
        {
            stored.Revoke(timeProvider.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        AppUser? user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user?.Email is null)
        {
            return;
        }

        string token = await userManager.GeneratePasswordResetTokenAsync(user);
        string encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        string link = QueryHelpers.AddQueryString(
            $"{moduleOptions.Value.PublicUrl.TrimEnd('/')}/reset-password",
            new Dictionary<string, string?> { ["email"] = user.Email, ["token"] = encoded });

        await emailSender.SendPasswordResetAsync(user.Email, link, cancellationToken);
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        AppUser? user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return AuthErrors.InvalidResetToken;
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return AuthErrors.InvalidResetToken;
        }

        IdentityResult result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == "InvalidToken")
                ? AuthErrors.InvalidResetToken
                : AuthErrors.WeakPassword(ToDetails(result, "newPassword"));
        }

        // После смены пароля все старые сессии закрываются.
        await RevokeAllAsync(user.Id, timeProvider.GetUtcNow(), cancellationToken);
        await userManager.ResetAccessFailedCountAsync(user);
        return Result.Success();
    }

    private async Task<AuthSession> CreateSessionAsync(AppUser user, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        string refreshToken = TokenIssuer.CreateRefreshToken();
        var stored = RefreshToken.Issue(user.Id, TokenIssuer.Hash(refreshToken), now, RefreshLifetime);
        db.RefreshTokens.Add(stored);
        await db.SaveChangesAsync(cancellationToken);

        return await BuildSessionAsync(user, refreshToken, stored.ExpiresAt);
    }

    private async Task<AuthSession> BuildSessionAsync(AppUser user, string refreshToken, DateTimeOffset refreshExpiresAt)
    {
        IList<string> roles = await userManager.GetRolesAsync(user);
        (string accessToken, DateTimeOffset expiresAt) = tokenIssuer.CreateAccessToken(user.Id, user.Email!, roles);

        return new AuthSession(
            new AuthResponse(accessToken, expiresAt, new AuthUser(user.Id, user.Email!, roles.ToArray())),
            refreshToken,
            refreshExpiresAt);
    }

    private Task RevokeAllAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);

    private static Dictionary<string, string[]> ToDetails(IdentityResult result, string field) =>
        new() { [field] = result.Errors.Select(e => e.Description).ToArray() };
}
