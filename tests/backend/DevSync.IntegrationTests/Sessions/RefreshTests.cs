using System.Net;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Sessions;

/// <summary>Refresh-token rotation, concurrency and reuse detection (FR-010, FR-012, FR-013, SC-003).</summary>
public sealed class RefreshTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Refresh_rotates_the_cookie_and_issues_a_new_access_token()
    {
        var clock = DevSyncApiFactory.NewClock();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, time: clock);
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");
        var (firstAccess, firstCookie) = (client.AccessToken, client.RefreshToken);
        clock.Advance(TimeSpan.FromSeconds(1));

        var response = await client.RefreshAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(firstCookie, client.RefreshToken);
        Assert.NotEqual(firstAccess, client.AccessToken);
        Assert.True((await client.BearerClient(factory).GetAsync("/api/workspaces")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Replaying_a_used_token_within_10_seconds_is_a_conflict_and_keeps_the_session()
    {
        var clock = DevSyncApiFactory.NewClock();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, time: clock);
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");
        var oldCookie = client.RefreshToken!;
        await client.RefreshAsync();
        clock.Advance(TimeSpan.FromSeconds(5));

        var replay = await client.RefreshAsync(oldCookie);

        await replay.AssertProblemAsync(409, "refresh_conflict");
        Assert.Equal(HttpStatusCode.OK, (await client.RefreshAsync()).StatusCode);
    }

    [Fact]
    public async Task Replaying_a_used_token_later_revokes_the_whole_session()
    {
        var clock = DevSyncApiFactory.NewClock();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, time: clock);
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");
        var stolen = client.RefreshToken!;
        await client.RefreshAsync();
        var currentCookie = client.RefreshToken!;
        var bearer = client.BearerClient(factory);
        clock.Advance(TimeSpan.FromSeconds(11));

        var replay = await AuthClient.Create(factory).RefreshAsync(stolen);

        await replay.AssertProblemAsync(401, "session_ended");
        Assert.Equal(string.Empty, AuthClient.SetCookie(replay)!.Value.Value);
        await (await client.RefreshAsync(currentCookie)).AssertProblemAsync(401, "session_ended");
        await (await bearer.GetAsync("/api/workspaces")).AssertProblemAsync(401, "session_ended");
    }

    [Fact]
    public async Task Parallel_refreshes_with_one_cookie_succeed_exactly_once()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");
        var cookie = client.RefreshToken!;

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => AuthClient.Create(factory).RefreshAsync(cookie)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var winner = AuthClient.SetCookie(responses.Single(r => r.StatusCode == HttpStatusCode.OK))!.Value.Value;
        Assert.Equal(HttpStatusCode.OK, (await AuthClient.Create(factory).RefreshAsync(winner)).StatusCode);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_or_with_an_unknown_one_ends_the_session()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        await (await AuthClient.Create(factory).RefreshAsync()).AssertProblemAsync(401, "session_ended");
        await (await AuthClient.Create(factory).RefreshAsync("not-a-real-token")).AssertProblemAsync(401, "session_ended");
    }

    [Fact]
    public async Task Refresh_without_the_client_header_is_refused()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");

        var response = await client.SendAsync(HttpMethod.Post, "/api/auth/refresh", clientHeader: false);

        await response.AssertProblemAsync(403, "csrf_check_failed");
    }
}
