using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EnglishPlatform.BuildingBlocks.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EnglishPlatform.Identity.Infrastructure.Tokens;

/// <summary>Выпускает access-токены (JWT) и случайные refresh-токены.</summary>
public sealed class TokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;

    public static SymmetricSecurityKey CreateKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(Guid userId, string email, IEnumerable<string> roles)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AppClaims.UserId, userId.ToString()),
            new(AppClaims.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(AppClaims.Role, role)));

        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(CreateKey(_options.SigningKey), SecurityAlgorithms.HmacSha256),
        });

        return (token, expiresAt);
    }

    /// <summary>256 бит случайных данных в base64url.</summary>
    public static string CreateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
