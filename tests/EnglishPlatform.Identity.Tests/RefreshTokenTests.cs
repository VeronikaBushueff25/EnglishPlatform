using EnglishPlatform.Identity.Domain;
using Shouldly;

namespace EnglishPlatform.Identity.Tests;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_token_is_active_until_expiry()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now, TimeSpan.FromDays(30));

        token.IsActive(Now.AddDays(29)).ShouldBeTrue();
        token.IsActive(Now.AddDays(30)).ShouldBeFalse();
    }

    [Fact]
    public void Replaced_token_is_revoked_and_remembers_successor()
    {
        var current = RefreshToken.Issue(Guid.NewGuid(), "a", Now, TimeSpan.FromDays(30));
        var next = RefreshToken.Issue(current.UserId, "b", Now, TimeSpan.FromDays(30));

        current.ReplaceWith(next, Now.AddMinutes(1));

        current.IsActive(Now.AddMinutes(2)).ShouldBeFalse();
        current.WasReplaced.ShouldBeTrue();
        current.ReplacedByTokenId.ShouldBe(next.Id);
    }

    [Fact]
    public void Revoke_keeps_first_revocation_time()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "a", Now, TimeSpan.FromDays(30));

        token.Revoke(Now.AddMinutes(1));
        token.Revoke(Now.AddMinutes(5));

        token.RevokedAt.ShouldBe(Now.AddMinutes(1));
    }
}
