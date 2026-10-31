using System.Net;
using System.Text.Json.Nodes;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static DevSync.IntegrationTests.Infrastructure.DiagramTestData;

namespace DevSync.IntegrationTests.Diagrams;

/// <summary>Spec 004 FR-023, SC-004: concurrent saves conflict instead of overwriting.</summary>
public sealed class DiagramConcurrencyTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Of_ten_parallel_saves_from_one_revision_exactly_one_wins()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var id = await team.Owner.NewDiagramAsync(team.WorkspaceId, "Race");

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            (i % 2 == 0 ? team.Owner : team.Editor).SaveDocumentAsync(
                team.WorkspaceId, id, 1, Document([Node($"n{i}", title: $"writer {i}")]).ToJsonString())));

        var winner = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        foreach (var loser in responses.Where(r => r != winner))
        {
            Assert.Equal(2, (await loser.AssertProblemAsync(412, "revision_conflict")).GetProperty("currentRevision").GetInt64());
        }

        var stored = await team.Viewer.GetDiagramAsync(team.WorkspaceId, id);
        Assert.Equal(2, stored.GetProperty("revision").GetInt64());
        Assert.Equal($"writer {Array.IndexOf(responses, winner)}", stored.GetProperty("document").GetProperty("nodes")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_save_racing_a_delete_wins_or_is_not_found_and_never_fails()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        for (var round = 0; round < 15; round++)
        {
            var id = await team.Owner.NewDiagramAsync(team.WorkspaceId, $"r{round}");
            var save = team.Editor.SaveDocumentAsync(team.WorkspaceId, id, 1, SampleDocument().ToJsonString());
            var delete = team.Owner.DeleteDiagramAsync(team.WorkspaceId, id);
            await Task.WhenAll(save, delete);

            Assert.Contains((await save).StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
            Assert.Equal(HttpStatusCode.NoContent, (await delete).StatusCode);
        }
    }
}

/// <summary>Spec 004 US6, SC-006, FR-032: diagrams are independent and scoped to their workspace.</summary>
public sealed class DiagramIsolationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Changing_or_deleting_one_diagram_never_touches_another()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var a = await team.Owner.NewDiagramAsync(team.WorkspaceId, "Current");
        var b = await team.Owner.NewDiagramAsync(team.WorkspaceId, "Target");
        await team.Owner.SaveDocumentAsync(team.WorkspaceId, b, 1, Document([Node("only-b", "cache", "Redis-like")]).ToJsonString());
        var before = (await team.Owner.GetDiagramAsync(team.WorkspaceId, b)).GetRawText();

        await team.Owner.SaveDocumentAsync(team.WorkspaceId, a, 1, SampleDocument().ToJsonString());
        await team.Owner.RenameDiagramAsync(team.WorkspaceId, a, "Current 2");
        await team.Owner.DeleteDiagramAsync(team.WorkspaceId, a);

        Assert.Equal(before, (await team.Owner.GetDiagramAsync(team.WorkspaceId, b)).GetRawText());
    }

    [Fact]
    public async Task A_diagram_of_another_workspace_is_not_found_even_for_a_member_of_both()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var other = (await team.Owner.CreateWorkspaceAsync("Other")).GetProperty("id").GetGuid();
        var foreign = await team.Owner.NewDiagramAsync(other, "Theirs");

        await (await team.Owner.GetDiagramResponseAsync(team.WorkspaceId, foreign)).AssertProblemAsync(404, "diagram_not_found");
        await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, foreign, 1, Document().ToJsonString())).AssertProblemAsync(404, "diagram_not_found");
        await (await team.Owner.RenameDiagramAsync(team.WorkspaceId, foreign, "x")).AssertProblemAsync(404, "diagram_not_found");
        await (await team.Owner.DeleteDiagramAsync(team.WorkspaceId, foreign)).AssertProblemAsync(404, "diagram_not_found");
        Assert.Equal("Theirs", (await team.Owner.GetDiagramAsync(other, foreign)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Deleting_a_workspace_removes_its_diagrams()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        await team.Owner.NewDiagramAsync(team.WorkspaceId, "One");

        await using var scope = factory.CreateDbScope();
        await scope.Db.Database.ExecuteSqlAsync($"DELETE FROM workspaces WHERE id = {team.WorkspaceId}");
        Assert.False(await scope.Db.Diagrams.AnyAsync(d => d.WorkspaceId == team.WorkspaceId));
    }
}

/// <summary>Diagram text never reaches the logs (spec 004 NFR-001).</summary>
public sealed class DiagramLogScanTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Diagram_names_titles_labels_and_metadata_never_appear_in_logs()
    {
        var logs = new CapturingLoggerProvider();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, loggerProvider: logs);
        var team = await factory.TeamWorkspaceAsync();
        var id = await team.Owner.NewDiagramAsync(team.WorkspaceId, "marker-NAME");
        var node = Node("a", title: "marker-TITLE");
        node["metadata"] = new JsonArray(new JsonObject { ["key"] = "marker-KEY", ["value"] = "marker-VALUE" });
        await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1,
            Document([node, Node("b")], [Edge("e", "a", "b", "marker-LABEL")]).ToJsonString());
        await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, Document([Node("a", title: "marker-STALE")]).ToJsonString());
        await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 2, Document([Node("a", "marker-TYPE")]).ToJsonString());
        await team.Viewer.SaveDocumentAsync(team.WorkspaceId, id, 2, Document([Node("a", title: "marker-VIEWER")]).ToJsonString());
        await team.Owner.RenameDiagramAsync(team.WorkspaceId, id, "marker-RENAMED");

        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(logs.Messages, m => m.Contains("marker-", StringComparison.Ordinal));
    }
}
