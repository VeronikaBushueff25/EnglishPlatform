using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.Identity.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EnglishPlatform.Identity.Infrastructure.Endpoints;

internal static class AuthEndpoints
{
    /// <summary>Refresh-токен живёт в httpOnly cookie, видимой только маршрутам /api/v1/auth.</summary>
    public const string RefreshCookie = "ep_refresh";
    private const string CookiePath = "/api/v1/auth";

    public static void Map(IEndpointRouteBuilder api)
    {
        RouteGroupBuilder auth = api.MapGroup("/auth")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth);

        auth.MapPost("/register", async (RegisterRequest request, IAuthService service, HttpContext http, CancellationToken ct) =>
                ToSessionResult(await service.RegisterAsync(request, ct), http))
            .WithValidation<RegisterRequest>()
            .WithName("Register")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapPost("/login", async (LoginRequest request, IAuthService service, HttpContext http, CancellationToken ct) =>
                ToSessionResult(await service.LoginAsync(request, ct), http))
            .WithValidation<LoginRequest>()
            .WithName("Login")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/refresh", async (IAuthService service, HttpContext http, CancellationToken ct) =>
            {
                Result<AuthSession> result = await service.RefreshAsync(http.Request.Cookies[RefreshCookie], ct);
                if (!result.IsSuccess)
                {
                    DeleteCookie(http);
                }

                return ToSessionResult(result, http);
            })
            .WithName("Refresh")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/logout", async (IAuthService service, HttpContext http, CancellationToken ct) =>
            {
                await service.LogoutAsync(http.Request.Cookies[RefreshCookie], ct);
                DeleteCookie(http);
                return TypedResults.NoContent();
            })
            .WithName("Logout");

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest request, IAuthService service, CancellationToken ct) =>
            {
                await service.ForgotPasswordAsync(request, ct);
                return TypedResults.Accepted((string?)null);
            })
            .WithValidation<ForgotPasswordRequest>()
            .WithName("ForgotPassword");

        auth.MapPost("/reset-password", async (ResetPasswordRequest request, IAuthService service, CancellationToken ct) =>
                (await service.ResetPasswordAsync(request, ct)).ToHttpResult())
            .WithValidation<ResetPasswordRequest>()
            .WithName("ResetPassword")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static IResult ToSessionResult(Result<AuthSession> result, HttpContext http)
    {
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        AuthSession session = result.Value;
        http.Response.Cookies.Append(RefreshCookie, session.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = session.RefreshTokenExpiresAt,
            IsEssential = true,
        });

        return TypedResults.Ok(session.Response);
    }

    private static void DeleteCookie(HttpContext http) =>
        http.Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = CookiePath, Secure = true, SameSite = SameSiteMode.Strict });
}
