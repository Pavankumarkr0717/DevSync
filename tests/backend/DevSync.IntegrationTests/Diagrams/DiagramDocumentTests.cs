using System.Net;
using System.Text.Json.Nodes;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static DevSync.IntegrationTests.Infrastructure.DiagramTestData;

namespace DevSync.IntegrationTests.Diagrams;

/// <summary>Spec 004 US3 and FR-033: authoritative validation, canonical storage, versions (FR-022–FR-029).</summary>
public sealed class DiagramDocumentTests(PostgresFixture postgres)
{
    private static async Task<(DevSyncApiFactory Factory, TeamWorkspace Team, Guid Diagram)> SetupAsync(PostgresFixture postgres)
    {
        var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var diagram = await team.Owner.NewDiagramAsync(team.WorkspaceId, "Checkout");
        return (factory, team, diagram);
    }

    [Fact]
    public async Task A_valid_document_round_trips_and_increments_the_revision()
    {
        var (factory, team, id) = await SetupAsync(postgres);
        await using var _ = factory;
        var sent = SampleDocument();

        var save = await team.Editor.SaveDocumentAsync(team.WorkspaceId, id, 1, sent.ToJsonString());

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal("\"2\"", save.Headers.ETag?.Tag);
        var result = await save.ReadJsonAsync();
        Assert.Equal(2, result.GetProperty("revision").GetInt64());
        Assert.Equal(4, result.GetProperty("nodeCount").GetInt32());
        Assert.Equal(3, result.GetProperty("edgeCount").GetInt32());

        var stored = JsonNode.Parse((await team.Viewer.GetDiagramAsync(team.WorkspaceId, id)).GetProperty("document").GetRawText())!;
        // Canonical order is by id; compare as sets of nodes and edges.
        Assert.True(JsonNode.DeepEquals(
            new JsonArray([.. sent["nodes"]!.AsArray().Select(n => n!.DeepClone()).OrderBy(n => n!["id"]!.GetValue<string>())]),
            stored["nodes"]));
        Assert.True(JsonNode.DeepEquals(
            new JsonArray([.. sent["edges"]!.AsArray().Select(e => e!.DeepClone()).OrderBy(e => e!["id"]!.GetValue<string>())]),
            stored["edges"]));
        Assert.True(JsonNode.DeepEquals(sent["viewport"], stored["viewport"]));
        Assert.Equal(4, (await team.Viewer.GetDiagramAsync(team.WorkspaceId, id)).GetProperty("nodeCount").GetInt32());
    }

    public static TheoryData<string, string, string> InvalidDocuments() => new()
    {
        { Document([Node("a")], [Edge("e7", "a", "n12")]).ToJsonString(), "unknown_node_reference", "edges[0].target" },
        { Document([Node("a"), Node("a")]).ToJsonString(), "duplicate_node_id", "nodes[1].id" },
        { Document([Node("a")], [Edge("e", "a", "a")]).ToJsonString(), "self_connection", "edges[0]" },
        { Document([Node("a"), Node("b")], [Edge("e1", "a", "b"), Edge("e2", "a", "b")]).ToJsonString(), "duplicate_edge", "edges[1]" },
        { Document([Node("a", "mainframe")]).ToJsonString(), "unknown_node_type", "nodes[0].type" },
        { Document([Node("a", title: new string('t', 101))]).ToJsonString(), "text_too_long", "nodes[0].title" },
        { Document([Node("a", title: " ")]).ToJsonString(), "text_required", "nodes[0].title" },
        { Document(viewport: new JsonObject { ["x"] = 0, ["y"] = 0, ["zoom"] = 9 }).ToJsonString(), "out_of_range", "viewport.zoom" },
    };

    [Theory]
    [MemberData(nameof(InvalidDocuments))]
    public async Task Every_rule_is_enforced_and_nothing_is_stored(string json, string code, string path)
    {
        var (factory, team, id) = await SetupAsync(postgres);
        await using var _ = factory;
        await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, SampleDocument().ToJsonString());

        var body = await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 2, json)).AssertProblemAsync(400, "invalid_diagram");

        var problem = Assert.Single(body.GetProperty("problems").EnumerateArray());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.Equal(path, problem.GetProperty("path").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("detail").GetString()));
        var stored = await team.Owner.GetDiagramAsync(team.WorkspaceId, id);
        Assert.Equal(2, stored.GetProperty("revision").GetInt64());
        Assert.Equal(4, stored.GetProperty("nodeCount").GetInt32());
    }

    [Fact]
    public async Task Several_problems_are_reported_at_once()
    {
        var (factory, team, id) = await SetupAsync(postgres);
        await using var _ = factory;
        var json = Document([Node("a", "mainframe"), Node("a")], [Edge("e", "a", "ghost")]).ToJsonString();

        var body = await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, json)).AssertProblemAsync(400, "invalid_diagram");

        var codes = body.GetProperty("problems").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToList();
        Assert.Equal(["unknown_node_type", "duplicate_node_id", "unknown_node_reference"], codes);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"nodes":[],"edges":[],"viewport":null,"script":"alert(1)"}""")]
    [InlineData("""{"schemaVersion":1,"nodes":[],"nodes":[],"edges":[],"viewport":null}""")]
    [InlineData("""{"schemaVersion":1,"nodes":[],"edges":[]}""")]
    [InlineData("""{"schemaVersion":1,"nodes":[],"edges":[],"viewport":null,}""")]
    [InlineData("""{"schemaVersion":1,/*hi*/"nodes":[],"edges":[],"viewport":null}""")]
    [InlineData("""{"schemaVersion":1,"nodes":"none","edges":[],"viewport":null}""")]
    [InlineData("""[]""")]
    [InlineData("""not json""")]
    public async Task Malformed_documents_are_rejected(string json)
    {
        var (factory, team, id) = await SetupAsync(postgres);
        await using var _ = factory;

        var body = await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, json)).AssertProblemAsync(400, "invalid_diagram");

        Assert.Equal("malformed", body.GetProperty("problems")[0].GetProperty("code").GetString());
        Assert.DoesNotContain("alert", body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_property_on_a_node_is_rejected()
    {
        var (factory, team, id) = await SetupAsync(postgres);
        await using var _ = factory;
        var node = Node("a");
        node["onClick"] = "steal()";

        var body = await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, Document([node]).ToJsonString()))
            .AssertProblemAsync(400, "invalid_diagram");

        Assert.Equal("malformed", body.GetProperty("problems")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Script_like_metadata_is_stored_as_plain_text()
    {
        var (factory, team, id) = await SetupAsync(postgres);
        await using var _ = factory;
        var node = Node("a");
        node["metadata"] = new JsonArray(
            new JsonObject { ["key"] = "__proto__", ["value"] = "<script>alert(1)</script>" },
            new JsonObject { ["key"] = "constructor", ["value"] = "() => 1" });

        Assert.Equal(HttpStatusCode.OK, (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, Document([node]).ToJsonString())).StatusCode);

        var metadata = (await team.Owner.GetDiagramAsync(team.WorkspaceId, id)).GetProperty("document").GetProperty("nodes")[0].GetProperty("metadata");
        Assert.Equal("__proto__", metadata[0].GetProperty("key").GetString());
        Assert.Equal("<script>alert(1)</script>", metadata[0].GetProperty("value").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Other_schema_versions_are_refused(int version)
    {
        var (factory, team, id) = await SetupAsync(postgres);
        await using var _ = factory;
        var doc = Document();
        doc["schemaVersion"] = version;

        await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, doc.ToJsonString())).AssertProblemAsync(400, "unsupported_schema_version");
    }

    [Fact]
    public async Task Http_preconditions_and_limits()
    {
        var (factory, team, id) = await SetupAsync(postgres);
        await using var _ = factory;
        var empty = Document().ToJsonString();

        await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, null, empty)).AssertProblemAsync(428, "revision_required");
        await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, empty, "text/plain")).AssertProblemAsync(415, "unsupported_media_type");
        var huge = Document([Node("a", title: "x")]);
        huge["nodes"]![0]!["description"] = new string('d', 1024 * 1024);
        await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, huge.ToJsonString())).AssertProblemAsync(413, "content_too_large");
        var stale = await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 7, empty)).AssertProblemAsync(412, "revision_conflict");
        Assert.Equal(1, stale.GetProperty("currentRevision").GetInt64());
    }

    [Fact]
    public async Task A_stored_newer_version_is_shown_read_only_and_never_overwritten_unless_replaced()
    {
        var (factory, team, id) = await SetupAsync(postgres);
        await using var _ = factory;
        await using (var scope = factory.CreateDbScope())
        {
            var newer = """{"schemaVersion":99,"shapes":[]}""";
            await scope.Db.Database.ExecuteSqlAsync($"UPDATE diagrams SET schema_version = 99, document = {newer}::jsonb WHERE id = {id}");
        }

        var diagram = await team.Viewer.GetDiagramAsync(team.WorkspaceId, id);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, diagram.GetProperty("document").ValueKind);
        Assert.Equal(99, diagram.GetProperty("unsupportedSchemaVersion").GetInt32());

        await (await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, SampleDocument().ToJsonString()))
            .AssertProblemAsync(409, "stored_version_unsupported");
        Assert.Equal(99, (await team.Owner.GetDiagramAsync(team.WorkspaceId, id)).GetProperty("unsupportedSchemaVersion").GetInt32());

        var replaced = await team.Owner.SaveDocumentAsync(team.WorkspaceId, id, 1, Document().ToJsonString(), replaceUnsupported: true);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var after = await team.Owner.GetDiagramAsync(team.WorkspaceId, id);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, after.GetProperty("unsupportedSchemaVersion").ValueKind);
        Assert.Equal(1, after.GetProperty("document").GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task An_unreadable_stored_document_is_reported_as_unsupported_version_0()
    {
        var (factory, team, id) = await SetupAsync(postgres);
        await using var _ = factory;
        await using (var scope = factory.CreateDbScope())
        {
            var broken = """{"schemaVersion":1,"nodes":"broken"}""";
            await scope.Db.Database.ExecuteSqlAsync($"UPDATE diagrams SET document = {broken}::jsonb WHERE id = {id}");
        }

        var diagram = await team.Owner.GetDiagramAsync(team.WorkspaceId, id);

        Assert.Equal(0, diagram.GetProperty("unsupportedSchemaVersion").GetInt32());
    }
}
