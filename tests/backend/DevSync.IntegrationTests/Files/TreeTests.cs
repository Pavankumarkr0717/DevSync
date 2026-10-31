using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Files;

/// <summary>Spec 003 US1: nested folder creation and a tree that persists (FR-001, FR-002, FR-012).</summary>
public sealed class TreeTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_new_workspace_has_an_empty_tree_with_the_server_limits()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        var tree = await team.Viewer.GetTreeAsync(team.WorkspaceId);

        Assert.Empty(tree.GetProperty("items").EnumerateArray());
        var limits = tree.GetProperty("limits");
        Assert.Equal(255, limits.GetProperty("maxNameLength").GetInt32());
        Assert.Equal(1024, limits.GetProperty("maxPathLength").GetInt32());
        Assert.Equal(20, limits.GetProperty("maxDepth").GetInt32());
        Assert.Equal(5000, limits.GetProperty("maxItems").GetInt32());
        Assert.Equal(1048576, limits.GetProperty("maxContentBytes").GetInt32());
    }

    [Fact]
    public async Task Nested_folders_and_a_file_are_created_and_seen_by_every_member()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        var src = await team.Editor.NewFolderAsync(team.WorkspaceId, null, "src");
        var api = await team.Editor.NewFolderAsync(team.WorkspaceId, src, "api");
        var server = await team.Editor.NewFileAsync(team.WorkspaceId, api, "server.ts");

        // A different member, on a fresh request, sees the same tree.
        var items = await team.Viewer.GetItemsAsync(team.WorkspaceId);
        Assert.Equal(3, items.Count);
        Assert.Equal(JsonNull, items[src].GetProperty("parentId").ValueKind);
        Assert.Equal("folder", items[src].GetProperty("kind").GetString());
        Assert.Equal(src, items[api].GetProperty("parentId").GetGuid());
        Assert.Equal(api, items[server].GetProperty("parentId").GetGuid());
        Assert.Equal("file", items[server].GetProperty("kind").GetString());
        Assert.Equal("server.ts", items[server].GetProperty("name").GetString());
        Assert.False(items[server].TryGetProperty("content", out _));
        await factory.AssertTreeInvariantsAsync(team.WorkspaceId);
    }

    [Fact]
    public async Task A_created_item_is_returned_with_its_location()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        var response = await team.Owner.CreateItemAsync(team.WorkspaceId, null, "file", "README.md");

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/workspaces/{team.WorkspaceId}/items/{id}", response.Headers.Location?.ToString());
        Assert.Equal("README.md", body.GetProperty("name").GetString());
        Assert.True(body.TryGetProperty("createdAt", out _));
        Assert.True(body.TryGetProperty("updatedAt", out _));
    }

    [Fact]
    public async Task A_new_file_is_empty_at_revision_1()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "main.py");

        var (text, revision) = await team.Viewer.GetContentAsync(team.WorkspaceId, file);

        Assert.Equal(string.Empty, text);
        Assert.Equal(1, revision);
    }

    private const System.Text.Json.JsonValueKind JsonNull = System.Text.Json.JsonValueKind.Null;
}
