using System.Net;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevSync.IntegrationTests.Files;

/// <summary>Spec 003 US4: file deletion, owner only (FR-015, FR-024).</summary>
public sealed class DeleteItemTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_owner_deletes_a_file_and_its_content()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");
        await team.Owner.SaveContentAsync(team.WorkspaceId, file, 1, "bye");

        var response = await team.Owner.DeleteItemAsync(team.WorkspaceId, file);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await team.Owner.GetItemsAsync(team.WorkspaceId));
        await using var scope = factory.CreateDbScope();
        Assert.False(await scope.Db.FileContents.AnyAsync(c => c.ItemId == file));
    }

    [Fact]
    public async Task Deleting_a_folder_removes_its_whole_subtree()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var src = await team.Owner.NewFolderAsync(team.WorkspaceId, null, "src");
        var removed = new List<Guid> { src };
        foreach (var dir in new[] { "api", "web", "lib" })
        {
            var folder = await team.Owner.NewFolderAsync(team.WorkspaceId, src, dir);
            removed.Add(folder);
            removed.Add(await team.Owner.NewFileAsync(team.WorkspaceId, folder, "index.ts"));
            var nested = await team.Owner.NewFolderAsync(team.WorkspaceId, folder, "deep");
            removed.Add(nested);
        }

        removed.Add(await team.Owner.NewFileAsync(team.WorkspaceId, src, "main.ts"));
        var keep = await team.Owner.NewFileAsync(team.WorkspaceId, null, "README.md");
        Assert.Equal(10, removed.Count - 1); // 10 items inside src

        Assert.Equal(HttpStatusCode.NoContent, (await team.Owner.DeleteItemAsync(team.WorkspaceId, src)).StatusCode);

        Assert.Equal([keep], (await team.Owner.GetItemsAsync(team.WorkspaceId)).Keys);
        await using var scope = factory.CreateDbScope();
        Assert.False(await scope.Db.WorkspaceItems.AnyAsync(i => removed.Contains(i.Id)));
        Assert.False(await scope.Db.FileContents.AnyAsync(c => removed.Contains(c.ItemId)));
        await factory.AssertTreeInvariantsAsync(team.WorkspaceId);
    }

    [Fact]
    public async Task Missing_already_deleted_and_foreign_items_are_not_found()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");
        var other = (await team.Owner.CreateWorkspaceAsync("Other")).GetProperty("id").GetGuid();
        var foreign = await team.Owner.NewFileAsync(other, null, "theirs.ts");

        await team.Owner.DeleteItemAsync(team.WorkspaceId, file);
        await (await team.Owner.DeleteItemAsync(team.WorkspaceId, file)).AssertProblemAsync(404, "item_not_found");
        await (await team.Owner.DeleteItemAsync(team.WorkspaceId, Guid.CreateVersion7())).AssertProblemAsync(404, "item_not_found");
        await (await team.Owner.DeleteItemAsync(team.WorkspaceId, foreign)).AssertProblemAsync(404, "item_not_found");
        Assert.Single(await team.Owner.GetItemsAsync(other));
    }

    [Fact]
    public async Task Editors_and_viewers_cannot_delete()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var src = await team.Editor.NewFolderAsync(team.WorkspaceId, null, "src");
        await team.Editor.NewFileAsync(team.WorkspaceId, src, "mine.ts");

        await (await team.Editor.DeleteItemAsync(team.WorkspaceId, src)).AssertProblemAsync(403, "forbidden");
        await (await team.Viewer.DeleteItemAsync(team.WorkspaceId, src)).AssertProblemAsync(403, "forbidden");
        Assert.Equal(2, (await team.Owner.GetItemsAsync(team.WorkspaceId)).Count);
    }
}
