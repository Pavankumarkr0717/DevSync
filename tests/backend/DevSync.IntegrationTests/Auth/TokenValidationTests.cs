using System.Net.Http.Headers;
using DevSync.Infrastructure.Persistence;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DevSync.IntegrationTests.Auth;

/// <summary>Access-token validation and immediate session revocation (FR-017, FR-026, FR-028, FR-029).</summary>
public sealed class TokenValidationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Missing_bearer_token_is_unauthenticated()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        var response = await factory.CreateClient().GetAsync("/api/workspaces");

        await response.AssertProblemAsync(401, "unauthenticated");
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    public async Task Malformed_tokens_are_unauthenticated(string token)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        var response = await factory.ClientWithToken(token).GetAsync("/api/workspaces");

        var body = await response.AssertProblemAsync(401, "unauthenticated");
        Assert.False(body.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Tokens_signed_with_another_key_or_for_another_audience_are_unauthenticated()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var user = await factory.CreateAccountAsync();
        var (_, sessionId) = await factory.StartSessionAsync(user);

        var foreignKey = Forge(user, sessionId, new byte[32], "devsync", "devsync");
        var wrongAudience = Forge(user, sessionId, Convert.FromBase64String(DevSyncApiFactory.TestSigningKey), "devsync", "other");
        var wrongIssuer = Forge(user, sessionId, Convert.FromBase64String(DevSyncApiFactory.TestSigningKey), "other", "devsync");

        foreach (var token in new[] { foreignKey, wrongAudience, wrongIssuer })
        {
            await (await factory.ClientWithToken(token).GetAsync("/api/workspaces")).AssertProblemAsync(401, "unauthenticated");
        }
    }

    [Fact]
    public async Task An_expired_access_token_is_reported_as_token_expired()
    {
        var clock = DevSyncApiFactory.NewClock();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, time: clock);
        var user = await factory.CreateAccountAsync();
        var client = await factory.ClientForAsync(user);
        Assert.True((await client.GetAsync("/api/workspaces")).IsSuccessStatusCode);

        clock.Advance(TimeSpan.FromMinutes(15));
        var response = await client.GetAsync("/api/workspaces");

        await response.AssertProblemAsync(401, "token_expired");
    }

    [Fact]
    public async Task A_token_whose_session_was_revoked_is_rejected_on_the_next_request()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var user = await factory.CreateAccountAsync();
        var (token, sessionId) = await factory.StartSessionAsync(user);
        var client = factory.ClientWithToken(token);
        Assert.True((await client.GetAsync("/api/workspaces")).IsSuccessStatusCode);

        await using (var scope = factory.CreateDbScope())
        {
            await scope.Db.Sessions.Where(s => s.Id == sessionId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow));
        }

        await (await client.GetAsync("/api/workspaces")).AssertProblemAsync(401, "session_ended");
    }

    [Fact]
    public async Task The_development_identity_no_longer_exists()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var user = await factory.CreateAccountAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-DevSync-User", user.ToString());

        await (await client.GetAsync("/api/dev/users")).AssertProblemAsync(401, "unauthenticated");
        await (await client.GetAsync("/api/workspaces")).AssertProblemAsync(401, "unauthenticated");
    }

    [Fact]
    public async Task Health_endpoints_remain_anonymous()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        Assert.True((await factory.CreateClient().GetAsync("/health/ready")).IsSuccessStatusCode);
    }

    private static string Forge(Guid userId, Guid sessionId, byte[] key, string issuer, string audience)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(15),
            Claims = new Dictionary<string, object> { ["sub"] = userId.ToString(), ["sid"] = sessionId.ToString() },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });
    }
}
