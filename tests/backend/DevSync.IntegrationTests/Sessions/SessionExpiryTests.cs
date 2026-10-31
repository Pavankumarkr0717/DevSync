using System.Net;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Sessions;

/// <summary>Idle (7 days) and absolute (30 days) session limits (FR-011, SC-006).</summary>
public sealed class SessionExpiryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task An_idle_session_expires_after_7_days()
    {
        var clock = DevSyncApiFactory.NewClock();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, time: clock);
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");

        clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1));

        await (await client.RefreshAsync()).AssertProblemAsync(401, "session_ended");
    }

    [Fact]
    public async Task Regular_renewal_keeps_a_session_alive_until_the_30_day_limit()
    {
        var clock = DevSyncApiFactory.NewClock();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, time: clock);
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");

        for (var day = 6; day < 30; day += 6)
        {
            clock.Advance(TimeSpan.FromDays(6));
            Assert.Equal(HttpStatusCode.OK, (await client.RefreshAsync()).StatusCode);
        }

        // Day 24 → day 30 + 1 s: past the absolute limit even though the idle limit has not passed.
        clock.Advance(TimeSpan.FromDays(6) + TimeSpan.FromSeconds(1));
        await (await client.RefreshAsync()).AssertProblemAsync(401, "session_ended");
    }

    [Fact]
    public async Task An_access_token_stops_working_when_its_session_expires()
    {
        var clock = DevSyncApiFactory.NewClock();
        await using var factory = await DevSyncApiFactory.CreateAsync(
            postgres, time: clock, settings: new Dictionary<string, string?> { ["DevSync:Auth:AccessTokenMinutes"] = "100000" });
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");
        var bearer = client.BearerClient(factory);

        clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1));

        await (await bearer.GetAsync("/api/workspaces")).AssertProblemAsync(401, "session_ended");
    }
}
