using System.Net;
using System.Text.Json;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Diagrams;

/// <summary>Spec 004 US1/US6: creating, listing, opening, renaming and deleting diagrams (FR-001–FR-007).</summary>
public sealed class DiagramCrudTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_new_diagram_is_empty_at_revision_1()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        var created = await team.Editor.CreateDiagramAsync(team.WorkspaceId, "  Checkout  ");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var summary = await created.ReadJsonAsync();
        var id = summary.GetProperty("id").GetGuid();
        Assert.Equal($"/api/workspaces/{team.WorkspaceId}/diagrams/{id}", created.Headers.Location?.ToString());
        Assert.Equal("Checkout", summary.GetProperty("name").GetString());
        Assert.Equal(1, summary.GetProperty("revision").GetInt64());
        Assert.Equal(0, summary.GetProperty("nodeCount").GetInt32());

        var get = await team.Viewer.GetDiagramResponseAsync(team.WorkspaceId, id);
        Assert.Equal("\"1\"", get.Headers.ETag?.Tag);
        Assert.True(get.Headers.CacheControl?.NoStore);
        var diagram = await get.ReadJsonAsync();
        var document = diagram.GetProperty("document");
        Assert.Equal(1, document.GetProperty("schemaVersion").GetInt32());
        Assert.Empty(document.GetProperty("nodes").EnumerateArray());
        Assert.Empty(document.GetProperty("edges").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, document.GetProperty("viewport").ValueKind);
        Assert.Equal(JsonValueKind.Null, diagram.GetProperty("unsupportedSchemaVersion").ValueKind);
    }

    [Fact]
    public async Task The_list_is_sorted_by_name_ignoring_case_and_carries_no_documents()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        foreach (var name in new[] { "target", "Current", "b-side" })
        {
            await team.Owner.NewDiagramAsync(team.WorkspaceId, name);
        }

        var list = await (await team.Viewer.GetAsync(DiagramTestData.DiagramsPath(team.WorkspaceId))).ReadJsonAsync();

        Assert.Equal(["b-side", "Current", "target"], list.EnumerateArray().Select(d => d.GetProperty("name").GetString()));
        Assert.All(list.EnumerateArray(), d => Assert.False(d.TryGetProperty("document", out _)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("a\u0001b")]
    public async Task Invalid_names_are_rejected(string? name)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        var body = await (await team.Owner.CreateDiagramAsync(team.WorkspaceId, name)).AssertProblemAsync(400, "validation_failed");

        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Names_are_unique_in_a_workspace_ignoring_case_but_not_across_workspaces()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var other = (await team.Owner.CreateWorkspaceAsync("Other")).GetProperty("id").GetGuid();
        await team.Owner.NewDiagramAsync(team.WorkspaceId, "Checkout");

        await (await team.Owner.CreateDiagramAsync(team.WorkspaceId, "CHECKOUT")).AssertProblemAsync(409, "diagram_name_taken");
        Assert.Equal(HttpStatusCode.Created, (await team.Owner.CreateDiagramAsync(other, "Checkout")).StatusCode);
    }

    [Fact]
    public async Task A_workspace_holds_at_most_50_diagrams_even_under_parallel_creates()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        for (var i = 0; i < 45; i++)
        {
            await team.Owner.NewDiagramAsync(team.WorkspaceId, $"d{i}");
        }

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            (i % 2 == 0 ? team.Owner : team.Editor).CreateDiagramAsync(team.WorkspaceId, $"p{i}")));

        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await refused.AssertProblemAsync(409, "too_many_diagrams");
        }
    }

    [Fact]
    public async Task Rename_keeps_the_revision_and_content_and_allows_a_case_only_change()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var id = await team.Owner.NewDiagramAsync(team.WorkspaceId, "target");
        await team.Owner.NewDiagramAsync(team.WorkspaceId, "Current");
        await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, DiagramTestData.SampleDocument().ToJsonString());

        var renamed = await team.Editor.RenameDiagramAsync(team.WorkspaceId, id, "Target 2027");
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await team.Editor.RenameDiagramAsync(team.WorkspaceId, id, "TARGET 2027")).StatusCode);
        await (await team.Editor.RenameDiagramAsync(team.WorkspaceId, id, "current")).AssertProblemAsync(409, "diagram_name_taken");

        var diagram = await team.Viewer.GetDiagramAsync(team.WorkspaceId, id);
        Assert.Equal("TARGET 2027", diagram.GetProperty("name").GetString());
        Assert.Equal(2, diagram.GetProperty("revision").GetInt64());
        Assert.Equal(4, diagram.GetProperty("document").GetProperty("nodes").GetArrayLength());
    }

    [Fact]
    public async Task The_owner_deletes_a_diagram_permanently()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var id = await team.Owner.NewDiagramAsync(team.WorkspaceId, "Gone soon");

        Assert.Equal(HttpStatusCode.NoContent, (await team.Owner.DeleteDiagramAsync(team.WorkspaceId, id)).StatusCode);

        await (await team.Owner.GetDiagramResponseAsync(team.WorkspaceId, id)).AssertProblemAsync(404, "diagram_not_found");
        await (await team.Owner.DeleteDiagramAsync(team.WorkspaceId, id)).AssertProblemAsync(404, "diagram_not_found");
        await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, DiagramTestData.Document().ToJsonString()))
            .AssertProblemAsync(404, "diagram_not_found");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Malformed_or_missing_ids_are_not_found(string id)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        await (await team.Owner.GetAsync($"{DiagramTestData.DiagramsPath(team.WorkspaceId)}/{id}")).AssertProblemAsync(404, "diagram_not_found");
    }
}
