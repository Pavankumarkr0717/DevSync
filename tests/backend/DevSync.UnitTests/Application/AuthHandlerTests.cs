using DevSync.Application.Auth;
using DevSync.Application.Common;
using DevSync.Domain.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DevSync.UnitTests.Application;

/// <summary>Register, login and refresh use cases (spec 002 FR-001–FR-013).</summary>
public sealed class AuthHandlerTests
{
    private const string Password = "correct horse battery";
    private static readonly SessionSettings Settings = new(TimeSpan.FromDays(7), TimeSpan.FromDays(30));

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeUserAccounts _accounts = new();
    private readonly FakeSessionRepository _sessions = new();
    private readonly FakeLoginLimiter _limiter = new();

    private SessionStarter Starter() => new(_sessions, new FakeAccessTokenIssuer(_time), Settings, _time);

    private RegisterHandler Register() => new(_accounts, Starter(), NullLogger<RegisterHandler>.Instance);

    private LoginHandler Login() => new(_accounts, _limiter, Starter(), NullLogger<LoginHandler>.Instance);

    private RefreshHandler Refresh() =>
        new(_sessions, _accounts, new FakeAccessTokenIssuer(_time), Settings, _time, NullLogger<RefreshHandler>.Instance);

    [Theory]
    [InlineData("", "ana@example.com", Password, "displayName")]
    [InlineData("   ", "ana@example.com", Password, "displayName")]
    [InlineData("Ana", "not-an-email", Password, "email")]
    [InlineData("Ana", "ana@example.com", "elevenchars", "password")]
    [InlineData("Ana", "ana@example.com", "ana@example.com", "password")]
    [InlineData("Ana", "ana@example.com", "ANA@EXAMPLE.COM", "password")]
    public async Task Register_rejects_invalid_input_without_creating_anything(string name, string email, string password, string field)
    {
        var outcome = await Register().HandleAsync(new RegisterCommand(name, email, password), CancellationToken.None);

        Assert.Equal(ErrorCodes.ValidationFailed, outcome.Error);
        Assert.Contains(outcome.FieldErrors, e => e.Field == field);
        Assert.Empty(_accounts.ByEmail);
        Assert.Empty(_sessions.Sessions);
    }

    [Theory]
    [InlineData(12, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public async Task Password_length_limits_are_12_to_128(int length, bool valid)
    {
        var outcome = await Register().HandleAsync(new RegisterCommand("Ana", "ana@example.com", new string('p', length)), CancellationToken.None);

        Assert.Equal(valid, outcome.IsSuccess);
    }

    [Fact]
    public async Task Display_name_over_100_characters_is_rejected()
    {
        var outcome = await Register().HandleAsync(new RegisterCommand(new string('n', 101), "ana@example.com", Password), CancellationToken.None);

        Assert.Contains(outcome.FieldErrors, e => e.Field == "displayName");
    }

    [Fact]
    public async Task Register_never_trims_the_password_and_starts_one_session()
    {
        var outcome = await Register().HandleAsync(new RegisterCommand(" Ana ", " ana@example.com ", "  spaced password  "), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("  spaced password  ", Assert.Single(_accounts.CreatedPasswords));
        Assert.Equal("Ana", outcome.Value.Session.User.DisplayName);
        Assert.Single(_sessions.Sessions);
        var token = Assert.Single(_sessions.Tokens);
        Assert.Equal(RefreshTokens.Hash(outcome.Value.RefreshToken), token.TokenHash);
        Assert.NotEqual(outcome.Value.RefreshToken, Convert.ToBase64String(token.TokenHash));
    }

    [Fact]
    public async Task Register_with_a_taken_email_fails_with_email_taken()
    {
        _accounts.Add("ana@example.com", Password);

        var outcome = await Register().HandleAsync(new RegisterCommand("Ana", "ana@example.com", Password), CancellationToken.None);

        Assert.Equal(ErrorCodes.EmailTaken, outcome.Error);
        Assert.Empty(_sessions.Sessions);
    }

    [Fact]
    public async Task Login_succeeds_resets_the_limiter_and_starts_a_session()
    {
        _accounts.Add("ana@example.com", Password);

        var outcome = await Login().HandleAsync(new LoginCommand("ana@example.com", Password), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(["ANA@EXAMPLE.COM"], _limiter.Resets);
        Assert.Single(_sessions.Sessions);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_both_verify_and_fail_identically()
    {
        _accounts.Add("ana@example.com", Password);

        var wrong = await Login().HandleAsync(new LoginCommand("ana@example.com", "wrong password!"), CancellationToken.None);
        var unknown = await Login().HandleAsync(new LoginCommand("nobody@example.com", Password), CancellationToken.None);

        Assert.Equal(ErrorCodes.InvalidCredentials, wrong.Error);
        Assert.Equal(ErrorCodes.InvalidCredentials, unknown.Error);
        Assert.Equal(2, _accounts.VerifyCalls);
        Assert.Equal(["ANA@EXAMPLE.COM", "NOBODY@EXAMPLE.COM"], _limiter.Failures);
        Assert.Empty(_sessions.Sessions);
    }

    [Fact]
    public async Task A_blocked_email_is_refused_without_checking_the_password()
    {
        _accounts.Add("ana@example.com", Password);
        _limiter.Blocked.Add("ANA@EXAMPLE.COM");

        var outcome = await Login().HandleAsync(new LoginCommand("Ana@Example.com", Password), CancellationToken.None);

        Assert.Equal(ErrorCodes.TooManyAttempts, outcome.Error);
        Assert.Equal(0, _accounts.VerifyCalls);
    }

    [Fact]
    public async Task Missing_login_fields_are_validation_errors()
    {
        var outcome = await Login().HandleAsync(new LoginCommand(" ", ""), CancellationToken.None);

        Assert.Equal(ErrorCodes.ValidationFailed, outcome.Error);
        Assert.Equal(["email", "password"], outcome.FieldErrors.Select(e => e.Field));
    }

    [Fact]
    public async Task Refresh_rotates_an_unused_token_and_slides_idle_expiry()
    {
        var started = await SignInAsync();
        _time.Advance(TimeSpan.FromDays(1));

        var outcome = await Refresh().HandleAsync(started.RefreshToken, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.NotEqual(started.RefreshToken, outcome.Value.RefreshToken);
        Assert.NotNull(_sessions.Tokens[0].UsedAt);
        Assert.Equal(_time.GetUtcNow() + Settings.IdleLifetime, _sessions.Sessions[0].IdleExpiresAt);
    }

    [Fact]
    public async Task A_token_reused_within_10_seconds_is_a_conflict_and_keeps_the_session()
    {
        var started = await SignInAsync();
        await Refresh().HandleAsync(started.RefreshToken, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(5));

        var again = await Refresh().HandleAsync(started.RefreshToken, CancellationToken.None);

        Assert.Equal(ErrorCodes.RefreshConflict, again.Error);
        Assert.True(_sessions.Sessions[0].IsActive(_time.GetUtcNow()));
    }

    [Fact]
    public async Task A_token_reused_later_revokes_the_session()
    {
        var started = await SignInAsync();
        await Refresh().HandleAsync(started.RefreshToken, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(11));

        var again = await Refresh().HandleAsync(started.RefreshToken, CancellationToken.None);

        Assert.Equal(ErrorCodes.SessionEnded, again.Error);
        Assert.Equal(SessionRevocationReason.RefreshTokenReuse, _sessions.Sessions[0].RevokedReason);
    }

    [Fact]
    public async Task Losing_the_rotation_race_is_a_conflict()
    {
        var started = await SignInAsync();
        _sessions.RotationLosesRace = true;

        var outcome = await Refresh().HandleAsync(started.RefreshToken, CancellationToken.None);

        Assert.Equal(ErrorCodes.RefreshConflict, outcome.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-token")]
    public async Task Missing_or_unknown_tokens_end_the_session(string? token)
    {
        var outcome = await Refresh().HandleAsync(token, CancellationToken.None);

        Assert.Equal(ErrorCodes.SessionEnded, outcome.Error);
    }

    [Fact]
    public async Task An_idle_expired_session_cannot_be_refreshed()
    {
        var started = await SignInAsync();
        _time.Advance(Settings.IdleLifetime);

        var outcome = await Refresh().HandleAsync(started.RefreshToken, CancellationToken.None);

        Assert.Equal(ErrorCodes.SessionEnded, outcome.Error);
    }

    private async Task<AuthResult> SignInAsync()
    {
        _accounts.Add("ana@example.com", Password);
        return (await Login().HandleAsync(new LoginCommand("ana@example.com", Password), CancellationToken.None)).Value;
    }
}
