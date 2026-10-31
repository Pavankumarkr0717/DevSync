using System.Net;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevSync.IntegrationTests.Files;

/// <summary>Spec 003 FR-029: concurrent structure changes never leave the tree half-changed (US4-AS8, NFR-003).</summary>
public sealed class StructureConcurrencyTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Crossed_moves_never_create_a_cycle()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        for (var round = 0; round < 10; round++)
        {
            var a = await team.Owner.NewFolderAsync(team.WorkspaceId, null, $"a{round}");
            var b = await team.Owner.NewFolderAsync(team.WorkspaceId, null, $"b{round}");

            var results = await Task.WhenAll(
                team.Owner.MoveAsync(team.WorkspaceId, a, b),
                team.Editor.MoveAsync(team.WorkspaceId, b, a));

            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
            await results.Single(r => r.StatusCode != HttpStatusCode.OK).AssertProblemAsync(400, "invalid_move");
            await factory.AssertTreeInvariantsAsync(team.WorkspaceId);
        }
    }

    [Fact]
    public async Task A_rename_racing_a_delete_either_applies_fully_or_is_not_found()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        for (var round = 0; round < 10; round++)
        {
            var folder = await team.Owner.NewFolderAsync(team.WorkspaceId, null, $"f{round}");
            await team.Owner.NewFileAsync(team.WorkspaceId, folder, "inside.ts");

            var rename = team.Editor.RenameAsync(team.WorkspaceId, folder, $"renamed{round}");
            var delete = team.Owner.DeleteItemAsync(team.WorkspaceId, folder);
            await Task.WhenAll(rename, delete);

            Assert.Equal(HttpStatusCode.NoContent, (await delete).StatusCode);
            Assert.Contains((await rename).StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
            Assert.DoesNotContain((await team.Owner.GetItemsAsync(team.WorkspaceId)).Keys, id => id == folder);
            await factory.AssertTreeInvariantsAsync(team.WorkspaceId);
        }
    }

    [Fact]
    public async Task Parallel_creates_never_exceed_the_item_limit()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        await factory.SeedTopLevelFoldersAsync(team.WorkspaceId, 4995);

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(i =>
            (i % 2 == 0 ? team.Owner : team.Editor).CreateItemAsync(team.WorkspaceId, null, "file", $"p{i}.ts")));

        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        await using var scope = factory.CreateDbScope();
        Assert.Equal(5000, await scope.Db.WorkspaceItems.CountAsync(i => i.WorkspaceId == team.WorkspaceId));
    }
}
