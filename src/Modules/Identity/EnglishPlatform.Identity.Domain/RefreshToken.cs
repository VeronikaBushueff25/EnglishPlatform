using EnglishPlatform.BuildingBlocks.Domain;

namespace EnglishPlatform.Identity.Domain;

/// <summary>
/// Refresh-токен. В базе хранится только SHA-256 хеш, сам токен знает лишь клиент.
/// При каждом обновлении токен заменяется новым (ротация). Если клиент предъявил уже заменённый токен,
/// значит его украли или скопировали, и все токены пользователя отзываются.
/// </summary>
public sealed class RefreshToken : Entity<Guid>
{
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    private RefreshToken(Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
        : base(Guid.CreateVersion7())
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public bool WasReplaced => ReplacedByTokenId is not null;

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(userId, tokenHash, now, now + lifetime);

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }

    public void ReplaceWith(RefreshToken next, DateTimeOffset now)
    {
        Revoke(now);
        ReplacedByTokenId = next.Id;
    }
}
