using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Workspaces;

/// <summary>US3 acceptance scenarios: members see details; everyone else gets an indistinguishable 404.</summary>
public sealed class WorkspaceDetailsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private DevSyncApiFactory _factory = null!;
    private Guid _owner;
    private HttpClient _ownerClient = null!;
    private Guid _workspaceId;

    public async ValueTask InitializeAsync()
    {
        _factory = await DevSyncApiFactory.CreateAsync(postgres);
        _owner = await _factory.CreateAccountAsync("Owner Person");
        _ownerClient = await _factory.ClientForAsync(_owner);
        _workspaceId = (await _ownerClient.CreateWorkspaceAsync("Design Review", "Weekly sessions")).GetProperty("id").GetGuid();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task AS1_member_sees_full_details()
    {
        var response = await _ownerClient.GetAsync($"/api/workspaces/{_workspaceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(_workspaceId, body.GetProperty("id").GetGuid());
        Assert.Equal("Design Review", body.GetProperty("name").GetString());
        Assert.Equal("Weekly sessions", body.GetProperty("description").GetString());
        Assert.Equal(_owner, body.GetProperty("owner").GetProperty("id").GetGuid());
        Assert.Equal("Owner Person", body.GetProperty("owner").GetProperty("displayName").GetString());
        Assert.True(body.TryGetProperty("createdAt", out _));
        Assert.True(body.TryGetProperty("updatedAt", out _));
        var member = Assert.Single(body.GetProperty("members").EnumerateArray());
        Assert.Equal(_owner, member.GetProperty("userId").GetGuid());
        Assert.Equal("Owner Person", member.GetProperty("displayName").GetString());
        Assert.Equal("owner", member.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Missing_description_is_returned_as_null()
    {
        var id = (await _ownerClient.CreateWorkspaceAsync("No description")).GetProperty("id").GetGuid();

        var body = await (await _ownerClient.GetAsync($"/api/workspaces/{id}")).ReadJsonAsync();

        Assert.Equal(JsonValueKind.Null, body.GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task AS2_non_member_gets_not_found()
    {
        var stranger = await _factory.ClientForAsync(await _factory.CreateAccountAsync("Stranger"));

        var response = await stranger.GetAsync($"/api/workspaces/{_workspaceId}");

        await response.AssertProblemAsync(404, "not_found");
    }

    [Theory]
    [InlineData("0192f5a0-1111-7000-8000-000000000000")] // valid, never created
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-uuid")]
    [InlineData("123")]
    [InlineData("0192f5a0-1111-7000-8000-000000000000-extra")]
    public async Task AS3_missing_or_invalid_identifier_gets_not_found(string id)
    {
        var response = await _ownerClient.GetAsync($"/api/workspaces/{id}");

        await response.AssertProblemAsync(404, "not_found");
    }

    [Fact]
    public async Task Not_member_missing_and_malformed_responses_are_indistinguishable()
    {
        var stranger = await _factory.ClientForAsync(await _factory.CreateAccountAsync("Stranger"));

        var forbidden = await NormalizedProblemAsync(await stranger.GetAsync($"/api/workspaces/{_workspaceId}"));
        var missing = await NormalizedProblemAsync(await stranger.GetAsync($"/api/workspaces/{Guid.CreateVersion7()}"));
        var malformed = await NormalizedProblemAsync(await stranger.GetAsync("/api/workspaces/not-a-uuid"));

        Assert.Equal(forbidden, missing);
        Assert.Equal(forbidden, malformed);
    }

    [Fact]
    public async Task Request_without_identity_is_rejected()
    {
        var response = await _factory.CreateClient().GetAsync($"/api/workspaces/{_workspaceId}");

        await response.AssertProblemAsync(401, "unauthenticated");
    }

    [Fact]
    public async Task Details_are_identical_after_an_application_restart()
    {
        var before = (await (await _ownerClient.GetAsync($"/api/workspaces/{_workspaceId}")).ReadJsonAsync()).ToString();
        await _factory.DisposeAsync();

        _factory = new DevSyncApiFactory(_factory.ConnectionString);
        var after = (await (await (await _factory.ClientForAsync(_owner)).GetAsync($"/api/workspaces/{_workspaceId}")).ReadJsonAsync()).ToString();

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Details_return_503_when_the_database_is_unreachable()
    {
        await using var offline = DevSyncApiFactory.CreateWithUnreachableDatabase();

        var response = await offline.ClientWithUnverifiableSession().GetAsync($"/api/workspaces/{_workspaceId}");

        await response.AssertProblemAsync(503, "service_unavailable");
    }

    /// <summary>The problem body with per-request fields removed.</summary>
    private static async Task<string> NormalizedProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        node.Remove("correlationId");
        node.Remove("instance");
        return node.ToJsonString();
    }
}
