using System.Net;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Auth;

/// <summary>The authenticated profile (FR-019).</summary>
public sealed class ProfileTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Me_returns_exactly_id_name_email_and_creation_time()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var user = await factory.CreateAccountAsync("Ana", "ana@example.com");

        var response = await (await factory.ClientForAsync(user)).GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(["id", "displayName", "email", "createdAt"], body.EnumerateObject().Select(p => p.Name));
        Assert.Equal(user, body.GetProperty("id").GetGuid());
        Assert.Equal("Ana", body.GetProperty("displayName").GetString());
        Assert.Equal("ana@example.com", body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Me_requires_a_session()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        await (await factory.CreateClient().GetAsync("/api/me")).AssertProblemAsync(401, "unauthenticated");
    }
}
