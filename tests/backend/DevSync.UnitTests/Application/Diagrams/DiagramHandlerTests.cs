using System.Text;
using DevSync.Application.Common;
using DevSync.Application.Diagrams;
using DevSync.Domain.Diagrams;

namespace DevSync.UnitTests.Application.Diagrams;

internal sealed class FakeDiagramRepository : IDiagramRepository
{
    public List<Diagram> Created { get; } = [];

    public CreateDiagramStatus CreateResult { get; set; } = CreateDiagramStatus.Created;

    public SaveDiagramResult SaveResult { get; set; } = new(SaveDiagramStatus.Saved, 2, DateTimeOffset.UnixEpoch);

    public (string Json, int Nodes, int Edges, bool Replace)? LastSave { get; private set; }

    public StoredDiagram? Stored { get; set; }

    public Task<IReadOnlyList<DiagramSummaryDto>> ListAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DiagramSummaryDto>>([.. Created.Select(DiagramSummaryDto.From)]);

    public Task<CreateDiagramStatus> CreateAsync(Diagram diagram, CancellationToken cancellationToken)
    {
        if (CreateResult == CreateDiagramStatus.Created)
        {
            Created.Add(diagram);
        }

        return Task.FromResult(CreateResult);
    }

    public Task<StoredDiagram?> GetAsync(Guid workspaceId, Guid diagramId, CancellationToken cancellationToken) => Task.FromResult(Stored);

    public Task<(RenameDiagramStatus Status, DiagramSummaryDto? Summary)> RenameAsync(
        Guid workspaceId, Guid diagramId, DiagramName name, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<(RenameDiagramStatus, DiagramSummaryDto?)>((RenameDiagramStatus.NotFound, null));

    public Task<SaveDiagramResult> TrySaveAsync(
        Guid workspaceId, Guid diagramId, long expectedRevision, string canonicalJson, int nodeCount, int edgeCount,
        bool replaceUnsupported, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        LastSave = (canonicalJson, nodeCount, edgeCount, replaceUnsupported);
        return Task.FromResult(SaveResult);
    }

    public Task<bool> DeleteAsync(Guid workspaceId, Guid diagramId, CancellationToken cancellationToken) => Task.FromResult(false);
}

public sealed class DiagramHandlerTests
{
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();
    private readonly FakeDiagramRepository _diagrams = new();

    private SaveDiagramDocumentHandler Save() => new(_diagrams, new FakeCurrentUser(Guid.CreateVersion7()), TimeProvider.System);

    private static ReadOnlyMemory<byte> Json(string json) => Encoding.UTF8.GetBytes(json);

    private const string Valid = """
        {"schemaVersion":1,"nodes":[{"id":"b","type":"database","title":"DB","description":"","metadata":[],"position":{"x":0,"y":0},"size":{"width":100,"height":60}},
        {"id":"a","type":"client","title":"App","description":"","metadata":[],"position":{"x":0,"y":0},"size":{"width":100,"height":60}}],
        "edges":[{"id":"e","source":"a","target":"b","label":"SQL"}],"viewport":null}
        """;

    [Fact]
    public async Task A_valid_document_is_stored_in_canonical_form_with_its_counts()
    {
        var outcome = await Save().HandleAsync(WorkspaceId, Guid.CreateVersion7(), 1, Json(Valid), false, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal((2L, 2, 1), (outcome.Value.Revision, outcome.Value.NodeCount, outcome.Value.EdgeCount));
        var (json, nodes, edges, replace) = _diagrams.LastSave!.Value;
        Assert.Equal((2, 1, false), (nodes, edges, replace));
        Assert.True(json.IndexOf("\"id\":\"a\"", StringComparison.Ordinal) < json.IndexOf("\"id\":\"b\"", StringComparison.Ordinal));
        Assert.StartsWith("{\"schemaVersion\":1,", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_documents_never_reach_the_repository()
    {
        var outcome = await Save().HandleAsync(WorkspaceId, Guid.CreateVersion7(), 1, Json(Valid.Replace("\"target\":\"b\"", "\"target\":\"zz\"")), false, CancellationToken.None);

        Assert.Equal(ErrorCodes.InvalidDiagram, outcome.Error);
        var problems = Assert.IsAssignableFrom<IReadOnlyList<DiagramProblem>>(outcome.Extra![SaveDiagramDocumentHandler.ProblemsKey]);
        Assert.Equal("unknown_node_reference", Assert.Single(problems).Code);
        Assert.Null(_diagrams.LastSave);
    }

    [Theory]
    [InlineData(SaveDiagramStatus.Conflict, ErrorCodes.RevisionConflict)]
    [InlineData(SaveDiagramStatus.NotFound, ErrorCodes.DiagramNotFound)]
    [InlineData(SaveDiagramStatus.StoredUnsupported, ErrorCodes.StoredVersionUnsupported)]
    public async Task Repository_outcomes_map_to_error_codes(SaveDiagramStatus status, string code)
    {
        _diagrams.SaveResult = new SaveDiagramResult(status, 5);

        var outcome = await Save().HandleAsync(WorkspaceId, Guid.CreateVersion7(), 1, Json(Valid), true, CancellationToken.None);

        Assert.Equal(code, outcome.Error);
        Assert.True(_diagrams.LastSave!.Value.Replace);
        if (status == SaveDiagramStatus.Conflict)
        {
            Assert.Equal(5L, outcome.Extra![SaveDiagramDocumentHandler.CurrentRevision]);
        }
    }

    [Fact]
    public async Task Create_validates_the_name_and_maps_limits()
    {
        var create = new CreateDiagramHandler(_diagrams, new FakeCurrentUser(Guid.CreateVersion7()), TimeProvider.System);

        Assert.Equal(ErrorCodes.ValidationFailed, (await create.HandleAsync(WorkspaceId, " ", CancellationToken.None)).Error);
        var ok = await create.HandleAsync(WorkspaceId, " Checkout ", CancellationToken.None);
        Assert.Equal(("Checkout", 1L, 0), (ok.Value.Name, ok.Value.Revision, ok.Value.NodeCount));
        Assert.Equal(CreateDiagramHandler.EmptyDocument, _diagrams.Created[0].Document);

        _diagrams.CreateResult = CreateDiagramStatus.TooMany;
        Assert.Equal(ErrorCodes.TooManyDiagrams, (await create.HandleAsync(WorkspaceId, "x", CancellationToken.None)).Error);
        _diagrams.CreateResult = CreateDiagramStatus.NameTaken;
        Assert.Equal(ErrorCodes.DiagramNameTaken, (await create.HandleAsync(WorkspaceId, "x", CancellationToken.None)).Error);
    }

    [Fact]
    public async Task Get_upgrades_or_reports_unsupported_versions()
    {
        var summary = new DiagramSummaryDto(Guid.CreateVersion7(), "D", 3, 0, 0, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var get = new GetDiagramHandler(_diagrams, DiagramDocumentUpgrader.Production);

        _diagrams.Stored = new StoredDiagram(summary, 1, CreateDiagramHandler.EmptyDocument);
        var current = await get.HandleAsync(WorkspaceId, summary.Id, CancellationToken.None);
        Assert.NotNull(current!.Document);
        Assert.Null(current.UnsupportedSchemaVersion);

        _diagrams.Stored = new StoredDiagram(summary, 7, """{"schemaVersion":7}""");
        var newer = await get.HandleAsync(WorkspaceId, summary.Id, CancellationToken.None);
        Assert.Null(newer!.Document);
        Assert.Equal(7, newer.UnsupportedSchemaVersion);
    }
}
