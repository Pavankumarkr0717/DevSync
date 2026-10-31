using System.Net;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevSync.IntegrationTests.Files;

/// <summary>
/// Spec 003 US3: concurrent file updates produce a conflict instead of a silent overwrite
/// (FR-018, FR-019, FR-029, SC-004, NFR-003).
/// </summary>
public sealed class ContentConcurrencyTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Of_ten_parallel_saves_from_one_revision_exactly_one_wins()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "race.ts");
        var clients = new[] { team.Owner, team.Editor };

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            clients[i % 2].SaveContentAsync(team.WorkspaceId, file, 1, $"writer {i}")));

        var winner = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        var winnerIndex = Array.IndexOf(responses, winner);
        foreach (var loser in responses.Where(r => r != winner))
        {
            var body = await loser.AssertProblemAsync(412, "revision_conflict");
            Assert.Equal(2, body.GetProperty("currentRevision").GetInt64());
            Assert.Equal("\"2\"", loser.Headers.ETag?.Tag);
        }

        Assert.Equal(($"writer {winnerIndex}", 2L), await team.Viewer.GetContentAsync(team.WorkspaceId, file));
    }

    [Fact]
    public async Task A_save_based_on_an_old_revision_changes_nothing()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");
        await team.Editor.SaveContentAsync(team.WorkspaceId, file, 1, "newer work");

        var stale = await team.Owner.SaveContentAsync(team.WorkspaceId, file, 1, "older copy");

        var body = await stale.AssertProblemAsync(412, "revision_conflict");
        Assert.Equal(2, body.GetProperty("currentRevision").GetInt64());
        Assert.Equal(("newer work", 2L), await team.Owner.GetContentAsync(team.WorkspaceId, file));

        // An informed overwrite states the current revision.
        Assert.Equal(HttpStatusCode.OK, (await team.Owner.SaveContentAsync(team.WorkspaceId, file, 2, "older copy")).StatusCode);
        Assert.Equal(("older copy", 3L), await team.Owner.GetContentAsync(team.WorkspaceId, file));
    }

    [Fact]
    public async Task The_same_person_in_two_tabs_gets_the_same_conflict()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");
        var secondTab = await factory.ClientForAsync(team.OwnerId);

        Assert.Equal(HttpStatusCode.OK, (await team.Owner.SaveContentAsync(team.WorkspaceId, file, 1, "tab one")).StatusCode);
        await (await secondTab.SaveContentAsync(team.WorkspaceId, file, 1, "tab two")).AssertProblemAsync(412, "revision_conflict");
    }

    [Fact]
    public async Task Rename_and_move_by_someone_else_do_not_block_or_bump_a_save()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var lib = await team.Owner.NewFolderAsync(team.WorkspaceId, null, "lib");
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");

        await using (var scope = factory.CreateDbScope())
        {
            await scope.Db.Database.ExecuteSqlAsync($"""
                UPDATE workspace_items SET name = 'renamed.ts', normalized_name = 'RENAMED.TS', parent_id = {lib} WHERE id = {file}
                """);
        }

        Assert.Equal(1, (await team.Owner.GetContentAsync(team.WorkspaceId, file)).Revision);
        var save = await team.Editor.SaveContentAsync(team.WorkspaceId, file, 1, "still mine");
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal(("still mine", 2L), await team.Owner.GetContentAsync(team.WorkspaceId, file));
    }

    [Fact]
    public async Task Saving_a_deleted_file_is_not_found()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");
        await DeleteDirectlyAsync(factory, file);

        await (await team.Editor.SaveContentAsync(team.WorkspaceId, file, 1, "lost?")).AssertProblemAsync(404, "item_not_found");
    }

    [Fact]
    public async Task A_save_racing_a_delete_either_wins_or_is_not_found_and_never_fails()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        for (var round = 0; round < 20; round++)
        {
            var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, $"r{round}.ts");
            var save = team.Editor.SaveContentAsync(team.WorkspaceId, file, 1, new string('x', 50_000));
            var delete = DeleteDirectlyAsync(factory, file);
            await Task.WhenAll(save, delete);

            Assert.Contains((await save).StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
        }

        await factory.AssertTreeInvariantsAsync(team.WorkspaceId);
    }

    private static async Task DeleteDirectlyAsync(DevSyncApiFactory factory, Guid itemId)
    {
        await using var scope = factory.CreateDbScope();
        await scope.Db.Database.ExecuteSqlAsync($"DELETE FROM workspace_items WHERE id = {itemId}");
    }
}
