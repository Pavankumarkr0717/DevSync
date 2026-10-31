using System.Net;
using System.Net.Http.Json;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Files;

/// <summary>Spec 003 US1-AS4: creation rules, including duplicate names in the same folder (FR-004–FR-010, FR-028, FR-029).</summary>
public sealed class CreateItemTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Duplicate_names_in_the_same_folder_are_rejected_ignoring_case()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var src = await team.Owner.NewFolderAsync(team.WorkspaceId, null, "src");
        await team.Owner.NewFileAsync(team.WorkspaceId, src, "Main.cs");

        await (await team.Owner.CreateItemAsync(team.WorkspaceId, src, "file", "main.CS")).AssertProblemAsync(409, "name_taken");
        await (await team.Owner.CreateItemAsync(team.WorkspaceId, src, "folder", "MAIN.cs")).AssertProblemAsync(409, "name_taken");

        // The same name elsewhere is fine.
        await team.Owner.NewFileAsync(team.WorkspaceId, null, "Main.cs");
        var lib = await team.Owner.NewFolderAsync(team.WorkspaceId, null, "lib");
        await team.Owner.NewFileAsync(team.WorkspaceId, lib, "main.cs");
        await (await team.Owner.CreateItemAsync(team.WorkspaceId, null, "folder", "SRC")).AssertProblemAsync(409, "name_taken");
        await factory.AssertTreeInvariantsAsync(team.WorkspaceId);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("Dockerfile")]
    [InlineData("ci.yml")]
    public async Task Files_with_unsupported_extensions_are_rejected(string name)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        await (await team.Owner.CreateItemAsync(team.WorkspaceId, null, "file", name)).AssertProblemAsync(400, "unsupported_extension");
    }

    [Fact]
    public async Task The_parent_must_be_a_folder_of_this_workspace()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");
        var otherWorkspace = (await team.Owner.CreateWorkspaceAsync("Other")).GetProperty("id").GetGuid();
        var foreignFolder = await team.Owner.NewFolderAsync(otherWorkspace, null, "theirs");

        await (await team.Owner.CreateItemAsync(team.WorkspaceId, file, "file", "b.ts")).AssertProblemAsync(404, "parent_not_found");
        await (await team.Owner.CreateItemAsync(team.WorkspaceId, Guid.CreateVersion7(), "file", "b.ts")).AssertProblemAsync(404, "parent_not_found");
        await (await team.Owner.CreateItemAsync(team.WorkspaceId, foreignFolder, "file", "b.ts")).AssertProblemAsync(404, "parent_not_found");
        Assert.Single(await team.Owner.GetItemsAsync(team.WorkspaceId));
    }

    [Fact]
    public async Task Malformed_requests_are_rejected()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var url = $"{team.Base}/items";

        await (await team.Owner.PostAsJsonAsync(url, new { parentId = "not-a-guid", kind = "file", name = "a.ts" })).AssertProblemAsync(400, "malformed_request");
        await (await team.Owner.PostAsJsonAsync(url, new { kind = "file", name = "a.ts" })).AssertProblemAsync(400, "malformed_request");
        await (await team.Owner.PostAsJsonAsync(url, new { parentId = (Guid?)null, name = "a.ts" })).AssertProblemAsync(400, "validation_failed");
        await (await team.Owner.PostAsJsonAsync(url, new { parentId = (Guid?)null, kind = "symlink", name = "a.ts" })).AssertProblemAsync(400, "validation_failed");
        await (await team.Owner.PostAsJsonAsync(url, new { parentId = (Guid?)null, kind = "file" })).AssertProblemAsync(400, "invalid_name");
        Assert.Empty(await team.Owner.GetItemsAsync(team.WorkspaceId));
    }

    [Fact]
    public async Task Depth_is_limited_to_20()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        Guid? parent = null;
        for (var depth = 0; depth <= 20; depth++)
        {
            parent = await team.Owner.NewFolderAsync(team.WorkspaceId, parent, $"d{depth}");
        }

        await (await team.Owner.CreateItemAsync(team.WorkspaceId, parent, "file", "deep.ts")).AssertProblemAsync(400, "too_deep");
    }

    [Fact]
    public async Task Paths_are_limited_to_1024_characters()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        Guid? parent = null;
        foreach (var name in new[] { new string('a', 255), new string('b', 255), new string('c', 255), new string('d', 254) })
        {
            parent = await team.Owner.NewFolderAsync(team.WorkspaceId, parent, name);
        }

        await team.Owner.NewFolderAsync(team.WorkspaceId, parent, "x"); // 1,024
        await (await team.Owner.CreateItemAsync(team.WorkspaceId, parent, "folder", "xy")).AssertProblemAsync(400, "path_too_long");
    }

    [Fact]
    public async Task A_workspace_holds_at_most_5000_items()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        await factory.SeedTopLevelFoldersAsync(team.WorkspaceId, 4999);

        await team.Owner.NewFileAsync(team.WorkspaceId, null, "last.ts");
        await (await team.Owner.CreateItemAsync(team.WorkspaceId, null, "file", "one-more.ts")).AssertProblemAsync(409, "too_many_items");
    }

    [Fact]
    public async Task Two_parallel_creates_of_the_same_name_produce_one_item()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        for (var round = 0; round < 5; round++)
        {
            var name = $"race{round}.ts";
            var responses = await Task.WhenAll(
                team.Owner.CreateItemAsync(team.WorkspaceId, null, "file", name),
                team.Editor.CreateItemAsync(team.WorkspaceId, null, "file", name.ToUpperInvariant()));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            await responses.Single(r => r.StatusCode != HttpStatusCode.Created).AssertProblemAsync(409, "name_taken");
        }

        await factory.AssertTreeInvariantsAsync(team.WorkspaceId);
    }
}
