using System.Net;
using System.Net.Http.Json;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Files;

/// <summary>Spec 003 US4: rename and move operations (FR-003, FR-013, FR-014, FR-028).</summary>
public sealed class RenameMoveTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Rename_keeps_id_content_revision_and_children()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var src = await team.Editor.NewFolderAsync(team.WorkspaceId, null, "src");
        var file = await team.Editor.NewFileAsync(team.WorkspaceId, src, "server.ts");
        await team.Editor.SaveContentAsync(team.WorkspaceId, file, 1, "code");

        var renamedFolder = await team.Editor.RenameAsync(team.WorkspaceId, src, "lib");
        var renamedFile = await team.Editor.RenameAsync(team.WorkspaceId, file, "app.ts");

        Assert.Equal(HttpStatusCode.OK, renamedFolder.StatusCode);
        Assert.Equal("app.ts", (await renamedFile.ReadJsonAsync()).GetProperty("name").GetString());
        var items = await team.Viewer.GetItemsAsync(team.WorkspaceId);
        Assert.Equal("lib", items[src].GetProperty("name").GetString());
        Assert.Equal(src, items[file].GetProperty("parentId").GetGuid());
        Assert.Equal(("code", 2L), await team.Viewer.GetContentAsync(team.WorkspaceId, file));
    }

    [Fact]
    public async Task Rename_follows_the_extension_rules()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");

        Assert.Equal(HttpStatusCode.OK, (await team.Owner.RenameAsync(team.WorkspaceId, file, "a.py")).StatusCode);
        await (await team.Owner.RenameAsync(team.WorkspaceId, file, "a.txt")).AssertProblemAsync(400, "unsupported_extension");
        Assert.Equal("a.py", (await team.Owner.GetItemsAsync(team.WorkspaceId))[file].GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_case_only_rename_is_allowed_but_a_sibling_clash_is_not()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var app = await team.Owner.NewFileAsync(team.WorkspaceId, null, "app.ts");
        await team.Owner.NewFileAsync(team.WorkspaceId, null, "other.ts");

        Assert.Equal(HttpStatusCode.OK, (await team.Owner.RenameAsync(team.WorkspaceId, app, "App.ts")).StatusCode);
        await (await team.Owner.RenameAsync(team.WorkspaceId, app, "OTHER.ts")).AssertProblemAsync(409, "name_taken");
    }

    [Theory]
    [InlineData("../app.ts")]
    [InlineData("a/b.ts")]
    [InlineData("..")]
    [InlineData("x.ts.")]
    [InlineData(" app.ts")]
    public async Task Rename_rejects_invalid_and_traversal_names(string name)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "app.ts");

        await (await team.Owner.RenameAsync(team.WorkspaceId, file, name)).AssertProblemAsync(400, "invalid_name");
    }

    [Fact]
    public async Task Rename_that_makes_a_descendant_path_too_long_is_rejected()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var root = await team.Owner.NewFolderAsync(team.WorkspaceId, null, "r");
        Guid? parent = root;
        foreach (var name in new[] { new string('b', 255), new string('c', 255), new string('d', 255), new string('e', 254) })
        {
            parent = await team.Owner.NewFolderAsync(team.WorkspaceId, parent, name);
        }

        await (await team.Owner.RenameAsync(team.WorkspaceId, root, "rr")).AssertProblemAsync(400, "path_too_long");
    }

    [Fact]
    public async Task Files_and_folders_move_with_their_subtree_and_to_the_top_level()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var src = await team.Editor.NewFolderAsync(team.WorkspaceId, null, "src");
        var api = await team.Editor.NewFolderAsync(team.WorkspaceId, src, "api");
        var v1 = await team.Editor.NewFolderAsync(team.WorkspaceId, api, "v1");
        var routes = await team.Editor.NewFileAsync(team.WorkspaceId, v1, "routes.ts");
        var lib = await team.Editor.NewFolderAsync(team.WorkspaceId, null, "lib");

        Assert.Equal(HttpStatusCode.OK, (await team.Editor.MoveAsync(team.WorkspaceId, api, lib)).StatusCode);
        var items = await team.Owner.GetItemsAsync(team.WorkspaceId);
        Assert.Equal(lib, items[api].GetProperty("parentId").GetGuid());
        Assert.Equal(v1, items[routes].GetProperty("parentId").GetGuid());

        var moved = await team.Editor.MoveAsync(team.WorkspaceId, routes, null);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await moved.ReadJsonAsync()).GetProperty("parentId").ValueKind);
        await factory.AssertTreeInvariantsAsync(team.WorkspaceId);
    }

    [Fact]
    public async Task Moving_into_itself_or_a_descendant_is_rejected()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var src = await team.Owner.NewFolderAsync(team.WorkspaceId, null, "src");
        var api = await team.Owner.NewFolderAsync(team.WorkspaceId, src, "api");

        await (await team.Owner.MoveAsync(team.WorkspaceId, src, src)).AssertProblemAsync(400, "invalid_move");
        await (await team.Owner.MoveAsync(team.WorkspaceId, src, api)).AssertProblemAsync(400, "invalid_move");
        await factory.AssertTreeInvariantsAsync(team.WorkspaceId);
    }

    [Fact]
    public async Task Moving_to_the_current_parent_succeeds_without_change()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var src = await team.Owner.NewFolderAsync(team.WorkspaceId, null, "src");
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, src, "a.ts");
        var before = (await team.Owner.GetItemsAsync(team.WorkspaceId))[file].GetProperty("updatedAt").GetString();

        var response = await team.Owner.MoveAsync(team.WorkspaceId, file, src);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, (await response.ReadJsonAsync()).GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task The_target_must_be_a_folder_of_this_workspace_without_a_clash()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var target = await team.Owner.NewFileAsync(team.WorkspaceId, null, "t.ts");
        var lib = await team.Owner.NewFolderAsync(team.WorkspaceId, null, "lib");
        await team.Owner.NewFileAsync(team.WorkspaceId, lib, "A.ts");
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");
        var other = (await team.Owner.CreateWorkspaceAsync("Other")).GetProperty("id").GetGuid();
        var foreign = await team.Owner.NewFolderAsync(other, null, "theirs");

        await (await team.Owner.MoveAsync(team.WorkspaceId, file, target)).AssertProblemAsync(404, "parent_not_found");
        await (await team.Owner.MoveAsync(team.WorkspaceId, file, Guid.CreateVersion7())).AssertProblemAsync(404, "parent_not_found");
        await (await team.Owner.MoveAsync(team.WorkspaceId, file, foreign)).AssertProblemAsync(404, "parent_not_found");
        await (await team.Owner.MoveAsync(team.WorkspaceId, file, lib)).AssertProblemAsync(409, "name_taken");
        await (await team.Owner.MoveAsync(team.WorkspaceId, Guid.CreateVersion7(), null)).AssertProblemAsync(404, "item_not_found");
    }

    [Fact]
    public async Task A_move_that_pushes_the_subtree_too_deep_is_rejected()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var chainTop = await team.Owner.NewFolderAsync(team.WorkspaceId, null, "c0");
        Guid? chain = chainTop;
        for (var depth = 1; depth <= 10; depth++)
        {
            chain = await team.Owner.NewFolderAsync(team.WorkspaceId, chain, $"c{depth}");
        }

        Guid? target = null;
        for (var depth = 0; depth <= 10; depth++)
        {
            target = await team.Owner.NewFolderAsync(team.WorkspaceId, target, $"t{depth}");
        }

        await (await team.Owner.MoveAsync(team.WorkspaceId, chainTop, target)).AssertProblemAsync(400, "too_deep");
    }

    [Fact]
    public async Task A_move_body_must_state_the_parent()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");

        await (await team.Owner.PostAsJsonAsync($"{team.Base}/items/{file}/move", new { })).AssertProblemAsync(400, "malformed_request");
        await (await team.Owner.PostAsJsonAsync($"{team.Base}/items/{file}/rename", new { })).AssertProblemAsync(400, "invalid_name");
    }
}
