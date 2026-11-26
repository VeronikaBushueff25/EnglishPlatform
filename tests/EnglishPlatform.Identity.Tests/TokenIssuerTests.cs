using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Identity.Infrastructure.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace EnglishPlatform.Identity.Tests;

public sealed class TokenIssuerTests
{
    private static readonly JwtOptions JwtSettings = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "unit-test-signing-key-0123456789abcdef",
        AccessTokenMinutes = 15,
    };

    private static TokenIssuer CreateIssuer() =>
        new(Microsoft.Extensions.Options.Options.Create(JwtSettings), TimeProvider.System);

    [Fact]
    public async Task Access_token_carries_user_email_roles_and_validates_with_same_key()
    {
        var userId = Guid.NewGuid();

        (string token, DateTimeOffset expiresAt) = CreateIssuer().CreateAccessToken(userId, "a@b.c", [Roles.User, Roles.Admin]);

        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Validation(JwtSettings.SigningKey));

        result.IsValid.ShouldBeTrue();
        result.Claims[AppClaims.UserId].ShouldBe(userId.ToString());
        result.Claims[AppClaims.Email].ShouldBe("a@b.c");
        new JsonWebToken(token).Claims
            .Where(c => c.Type == AppClaims.Role)
            .Select(c => c.Value)
            .ShouldBe([Roles.User, Roles.Admin], ignoreOrder: true);
        expiresAt.ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
    }

    [Fact]
    public async Task Token_signed_with_other_key_is_rejected()
    {
        (string token, _) = CreateIssuer().CreateAccessToken(Guid.NewGuid(), "a@b.c", []);

        TokenValidationResult result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token, Validation("another-signing-key-0123456789abcdefgh"));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Refresh_tokens_are_random_and_hash_is_stable()
    {
        string first = TokenIssuer.CreateRefreshToken();
        string second = TokenIssuer.CreateRefreshToken();

        first.ShouldNotBe(second);
        TokenIssuer.Hash(first).ShouldBe(TokenIssuer.Hash(first));
        TokenIssuer.Hash(first).Length.ShouldBe(64);
    }

    private static TokenValidationParameters Validation(string key) => new()
    {
        ValidIssuer = JwtSettings.Issuer,
        ValidAudience = JwtSettings.Audience,
        IssuerSigningKey = TokenIssuer.CreateKey(key),
    };
}
