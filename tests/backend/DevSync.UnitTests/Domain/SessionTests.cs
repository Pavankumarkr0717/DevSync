using DevSync.Domain.Identity;
using Microsoft.Extensions.Time.Testing;

namespace DevSync.UnitTests.Domain;

public sealed class SessionTests
{
    private static readonly TimeSpan Idle = TimeSpan.FromDays(7);
    private static readonly TimeSpan Absolute = TimeSpan.FromDays(30);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));

    private Session Start() => Session.Start(Guid.CreateVersion7(), _time.GetUtcNow(), Idle, Absolute);

    [Fact]
    public void A_new_session_is_active_with_idle_and_absolute_limits()
    {
        var session = Start();

        Assert.True(session.IsActive(_time.GetUtcNow()));
        Assert.Equal(_time.GetUtcNow() + Idle, session.IdleExpiresAt);
        Assert.Equal(_time.GetUtcNow() + Absolute, session.AbsoluteExpiresAt);
    }

    [Fact]
    public void Session_is_inactive_at_the_idle_limit()
    {
        var session = Start();
        _time.Advance(Idle);

        Assert.False(session.IsActive(_time.GetUtcNow()));
    }

    [Fact]
    public void Refreshing_slides_the_idle_limit_but_never_past_the_absolute_limit()
    {
        var session = Start();
        _time.Advance(TimeSpan.FromDays(6));
        session.Refreshed(_time.GetUtcNow(), Idle);
        Assert.Equal(_time.GetUtcNow() + Idle, session.IdleExpiresAt);
        Assert.Equal(_time.GetUtcNow(), session.LastRefreshedAt);

        _time.Advance(TimeSpan.FromDays(20)); // day 26
        session.Refreshed(_time.GetUtcNow(), Idle);
        Assert.Equal(session.AbsoluteExpiresAt, session.IdleExpiresAt);

        _time.Advance(TimeSpan.FromDays(4)); // day 30
        Assert.False(session.IsActive(_time.GetUtcNow()));
    }

    [Fact]
    public void A_revoked_session_is_inactive_and_keeps_its_first_reason()
    {
        var session = Start();

        session.Revoke(_time.GetUtcNow(), SessionRevocationReason.Logout);
        session.Revoke(_time.GetUtcNow(), SessionRevocationReason.LogoutAll);

        Assert.False(session.IsActive(_time.GetUtcNow()));
        Assert.Equal(SessionRevocationReason.Logout, session.RevokedReason);
    }

    [Fact]
    public void Presented_tokens_are_classified_by_use()
    {
        var session = Start();
        var token = RefreshToken.Issue(session.Id, new byte[32], _time.GetUtcNow());

        Assert.Equal(PresentedTokenState.Unused, Session.ClassifyPresentedToken(token, _time.GetUtcNow()));

        token.MarkUsed(_time.GetUtcNow());
        _time.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(PresentedTokenState.Conflict, Session.ClassifyPresentedToken(token, _time.GetUtcNow()));

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(PresentedTokenState.Reuse, Session.ClassifyPresentedToken(token, _time.GetUtcNow()));
    }

    [Fact]
    public void Refresh_token_hash_must_be_32_bytes()
    {
        Assert.Throws<ArgumentException>(() => RefreshToken.Issue(Guid.CreateVersion7(), new byte[16], _time.GetUtcNow()));
    }
}
