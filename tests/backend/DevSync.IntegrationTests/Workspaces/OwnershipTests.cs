using System.Net;
using System.Net.Http.Json;
using DevSync.Domain.Workspaces;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Workspaces;

/// <summary>US3: workspaces belong to accounts; nobody else can see or touch them (FR-027, FR-031).</summary>
public sealed class OwnershipTests(PostgresFixture postgres) : IAsyncLifetime
{
    private DevSyncApiFactory _factory = null!;
    private Guid _ana;
    private HttpClient _anaClient = null!;
    private Guid _workspaceId;

    public async ValueTask InitializeAsync()
    {
        _factory = await DevSyncApiFactory.CreateAsync(postgres);
        _ana = await _factory.CreateAccountAsync("Ana", "ana@example.com");
        _anaClient = await _factory.ClientForAsync(_ana);
        _workspaceId = (await _anaClient.CreateWorkspaceAsync("Ana's workspace")).GetProperty("id").GetGuid();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task The_creator_is_recorded_as_owner()
    {
        var body = await (await _anaClient.GetAsync($"/api/workspaces/{_workspaceId}")).ReadJsonAsync();

        Assert.Equal(_ana, body.GetProperty("owner").GetProperty("id").GetGuid());
        Assert.Equal("owner", body.GetProperty("myRole").GetString());
    }

    [Fact]
    public async Task The_list_shows_every_workspace_where_the_caller_has_any_role()
    {
        var ben = await _factory.CreateAccountAsync("Ben");
        var benClient = await _factory.ClientForAsync(ben);
        var benOwn = (await benClient.CreateWorkspaceAsync("Ben's own")).GetProperty("id").GetGuid();
        var anaSecond = (await _anaClient.CreateWorkspaceAsync("Ana's second")).GetProperty("id").GetGuid();
        await _factory.AddMemberAsync(_workspaceId, ben, WorkspaceRole.Editor);
        await _factory.AddMemberAsync(anaSecond, ben, WorkspaceRole.Viewer);

        var page = await (await benClient.GetAsync("/api/workspaces")).ReadJsonAsync();

        var roles = page.GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("id").GetGuid(), i => i.GetProperty("role").GetString());
        Assert.Equal(3, roles.Count);
        Assert.Equal("owner", roles[benOwn]);
        Assert.Equal("editor", roles[_workspaceId]);
        Assert.Equal("viewer", roles[anaSecond]);
    }

    [Fact]
    public async Task Another_account_gets_not_found_for_every_workspace_operation()
    {
        var mallory = await _factory.ClientForAsync(await _factory.CreateAccountAsync("Mallory"));
        var random = Guid.CreateVersion7();
        var paths = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, "", null),
            (HttpMethod.Put, "", new { name = "Hijacked" }),
            (HttpMethod.Patch, $"/members/{_ana}", new { role = "viewer" }),
            (HttpMethod.Delete, $"/members/{_ana}", null),
            (HttpMethod.Get, "/invitations", null),
            (HttpMethod.Post, "/invitations", new { email = "x@example.com", role = "editor" }),
        };

        foreach (var (method, path, body) in paths)
        {
            var onReal = await SendAsync(mallory, method, $"/api/workspaces/{_workspaceId}{path}", body);
            var onRandom = await SendAsync(mallory, method, $"/api/workspaces/{random}{path}", body);

            await onReal.AssertProblemAsync(404, "not_found");
            Assert.Equal(await onRandom.NormalizedBodyAsync(), await onReal.NormalizedBodyAsync());
        }

        var details = await (await _anaClient.GetAsync($"/api/workspaces/{_workspaceId}")).ReadJsonAsync();
        Assert.Equal("Ana's workspace", details.GetProperty("name").GetString());
        Assert.Single(details.GetProperty("members").EnumerateArray());
    }

    [Fact]
    public async Task Only_the_owner_sees_member_emails()
    {
        var ben = await _factory.CreateAccountAsync("Ben", "ben@example.com");
        await _factory.AddMemberAsync(_workspaceId, ben, WorkspaceRole.Editor);

        var asOwner = await (await _anaClient.GetAsync($"/api/workspaces/{_workspaceId}")).ReadJsonAsync();
        var asEditor = await (await (await _factory.ClientForAsync(ben)).GetAsync($"/api/workspaces/{_workspaceId}")).ReadJsonAsync();

        Assert.All(asOwner.GetProperty("members").EnumerateArray(), m => Assert.True(m.TryGetProperty("email", out _)));
        Assert.Contains(asOwner.GetProperty("members").EnumerateArray(), m => m.GetProperty("email").GetString() == "ben@example.com");
        Assert.All(asEditor.GetProperty("members").EnumerateArray(), m => Assert.False(m.TryGetProperty("email", out _)));
        Assert.Equal("editor", asEditor.GetProperty("myRole").GetString());
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request);
    }
}
