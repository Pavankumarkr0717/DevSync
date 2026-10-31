using System.Net;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Sessions;

/// <summary>Sign-out and sign-out everywhere take effect on the very next request (FR-017, FR-018, SC-003).</summary>
public sealed class LogoutTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Logout_ends_the_session_immediately_for_both_credentials()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");
        var bearer = client.BearerClient(factory);
        var cookie = client.RefreshToken!;

        var response = await client.LogoutAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cleared = AuthClient.SetCookie(response)!.Value;
        Assert.Equal(string.Empty, cleared.Value);
        Assert.Contains("max-age=0", cleared.Raw.ToLowerInvariant());
        await (await bearer.GetAsync("/api/workspaces")).AssertProblemAsync(401, "session_ended");
        await (await AuthClient.Create(factory).RefreshAsync(cookie)).AssertProblemAsync(401, "session_ended");
    }

    [Fact]
    public async Task Logout_is_idempotent_and_works_without_credentials()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");

        Assert.Equal(HttpStatusCode.NoContent, (await client.LogoutAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.LogoutAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await AuthClient.Create(factory).LogoutAsync()).StatusCode);
    }

    [Fact]
    public async Task Logout_by_bearer_alone_ends_that_session()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = AuthClient.Create(factory);
        await client.RegisterAsync("ana@example.com");
        var cookie = client.RefreshToken!;
        client.RefreshToken = null; // only the access token is sent

        await client.LogoutAsync();

        await (await AuthClient.Create(factory).RefreshAsync(cookie)).AssertProblemAsync(401, "session_ended");
    }

    [Fact]
    public async Task Logout_all_ends_every_session_of_the_user()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var laptop = AuthClient.Create(factory);
        await laptop.RegisterAsync("ana@example.com");
        var phone = AuthClient.Create(factory);
        await phone.LoginAsync("ana@example.com");
        var phoneBearer = phone.BearerClient(factory);
        var phoneCookie = phone.RefreshToken!;

        Assert.Equal(HttpStatusCode.NoContent, (await laptop.LogoutAllAsync()).StatusCode);

        await (await phoneBearer.GetAsync("/api/workspaces")).AssertProblemAsync(401, "session_ended");
        await (await AuthClient.Create(factory).RefreshAsync(phoneCookie)).AssertProblemAsync(401, "session_ended");
        await (await laptop.BearerClient(factory).GetAsync("/api/workspaces")).AssertProblemAsync(401, "session_ended");

        // Signing in again works.
        Assert.Equal(HttpStatusCode.OK, (await AuthClient.Create(factory).LoginAsync("ana@example.com")).StatusCode);
    }

    [Fact]
    public async Task Logout_all_requires_a_valid_access_token()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        await (await AuthClient.Create(factory).LogoutAllAsync()).AssertProblemAsync(401, "unauthenticated");
    }
}
