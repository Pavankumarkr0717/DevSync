using DevSync.Application.Common;
using DevSync.Domain.Diagrams;

namespace DevSync.Application.Diagrams;

// Use cases for spec 004. Authorization (Read / EditDiagrams / DeleteDiagrams) is checked by the caller.

public sealed class ListDiagramsHandler(IDiagramRepository diagrams)
{
    public Task<IReadOnlyList<DiagramSummaryDto>> HandleAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        diagrams.ListAsync(workspaceId, cancellationToken);
}

public sealed class CreateDiagramHandler(IDiagramRepository diagrams, ICurrentUser currentUser, TimeProvider time)
{
    public static readonly string EmptyDocument = DiagramJson.ToCanonical(DiagramDocument.Empty);

    public async Task<Outcome<DiagramSummaryDto>> HandleAsync(Guid workspaceId, string? name, CancellationToken cancellationToken)
    {
        var diagramName = DiagramName.Create(name);
        if (!diagramName.IsSuccess)
        {
            return Outcome<DiagramSummaryDto>.Invalid(diagramName.Errors);
        }

        var diagram = Diagram.Create(workspaceId, diagramName.Value, EmptyDocument, currentUser.UserId, time.GetUtcNow());
        return await diagrams.CreateAsync(diagram, cancellationToken) switch
        {
            CreateDiagramStatus.Created => Outcome<DiagramSummaryDto>.Ok(DiagramSummaryDto.From(diagram)),
            CreateDiagramStatus.TooMany => Outcome<DiagramSummaryDto>.Fail(ErrorCodes.TooManyDiagrams),
            _ => Outcome<DiagramSummaryDto>.Fail(ErrorCodes.DiagramNameTaken),
        };
    }
}

public sealed class GetDiagramHandler(IDiagramRepository diagrams, DiagramDocumentUpgrader upgrader)
{
    /// <summary>The diagram upgraded to the current format, or read-only metadata when it can't be read (FR-028, FR-029).</summary>
    public async Task<DiagramDto?> HandleAsync(Guid workspaceId, Guid diagramId, CancellationToken cancellationToken)
    {
        var stored = await diagrams.GetAsync(workspaceId, diagramId, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var s = stored.Summary;
        var (status, document, version) = DiagramJson.ReadStored(stored.Document, upgrader);
        return status == UpgradeStatus.Upgraded
            ? new DiagramDto(s.Id, s.Name, s.Revision, s.NodeCount, s.EdgeCount, s.CreatedAt, s.UpdatedAt, DiagramJson.ToElement(document!), null)
            : new DiagramDto(s.Id, s.Name, s.Revision, s.NodeCount, s.EdgeCount, s.CreatedAt, s.UpdatedAt, null,
                status == UpgradeStatus.Unsupported ? version ?? stored.SchemaVersion : 0);
    }
}

public sealed class RenameDiagramHandler(IDiagramRepository diagrams, ICurrentUser currentUser, TimeProvider time)
{
    public async Task<Outcome<DiagramSummaryDto>> HandleAsync(Guid workspaceId, Guid diagramId, string? name, CancellationToken cancellationToken)
    {
        var diagramName = DiagramName.Create(name);
        if (!diagramName.IsSuccess)
        {
            return Outcome<DiagramSummaryDto>.Invalid(diagramName.Errors);
        }

        var (status, summary) = await diagrams.RenameAsync(
            workspaceId, diagramId, diagramName.Value, currentUser.UserId, time.GetUtcNow(), cancellationToken);
        return status switch
        {
            RenameDiagramStatus.Renamed => Outcome<DiagramSummaryDto>.Ok(summary!),
            RenameDiagramStatus.NameTaken => Outcome<DiagramSummaryDto>.Fail(ErrorCodes.DiagramNameTaken),
            _ => Outcome<DiagramSummaryDto>.Fail(ErrorCodes.DiagramNotFound),
        };
    }
}

public sealed class SaveDiagramDocumentHandler(IDiagramRepository diagrams, ICurrentUser currentUser, TimeProvider time)
{
    public const string CurrentRevision = "currentRevision";
    public const string ProblemsKey = "problems";

    /// <summary>
    /// Parses strictly, checks the version, validates every rule, then stores the canonical form with a
    /// revision compare-and-swap (FR-022–FR-029, FR-033). Any problem refuses the whole save.
    /// </summary>
    public async Task<Outcome<SaveDiagramResultDto>> HandleAsync(
        Guid workspaceId, Guid diagramId, long expectedRevision, ReadOnlyMemory<byte> body, bool replaceUnsupported,
        CancellationToken cancellationToken)
    {
        var (status, document, error) = DiagramJson.Parse(body.Span);
        if (status == DiagramJson.ParseStatus.UnsupportedVersion)
        {
            return Outcome<SaveDiagramResultDto>.Fail(ErrorCodes.UnsupportedSchemaVersion, error);
        }

        if (status == DiagramJson.ParseStatus.Malformed)
        {
            return Invalid([new DiagramProblem("malformed", string.Empty, error ?? "Malformed JSON.")]);
        }

        var problems = DiagramValidator.Validate(document!);
        if (problems.Count > 0)
        {
            return Invalid(problems);
        }

        var result = await diagrams.TrySaveAsync(
            workspaceId, diagramId, expectedRevision, DiagramJson.ToCanonical(document!), document!.Nodes.Count,
            document.Edges.Count, replaceUnsupported, currentUser.UserId, time.GetUtcNow(), cancellationToken);

        return result.Status switch
        {
            SaveDiagramStatus.Saved => Outcome<SaveDiagramResultDto>.Ok(
                new SaveDiagramResultDto(result.Revision, document.Nodes.Count, document.Edges.Count, result.UpdatedAt)),
            SaveDiagramStatus.Conflict => Outcome<SaveDiagramResultDto>.Fail(
                ErrorCodes.RevisionConflict, null, new Dictionary<string, object?> { [CurrentRevision] = result.Revision }),
            SaveDiagramStatus.StoredUnsupported => Outcome<SaveDiagramResultDto>.Fail(ErrorCodes.StoredVersionUnsupported),
            _ => Outcome<SaveDiagramResultDto>.Fail(ErrorCodes.DiagramNotFound),
        };
    }

    private static Outcome<SaveDiagramResultDto> Invalid(IReadOnlyList<DiagramProblem> problems) =>
        Outcome<SaveDiagramResultDto>.Fail(
            ErrorCodes.InvalidDiagram, problems[0].Message, new Dictionary<string, object?> { [ProblemsKey] = problems });
}

public sealed class DeleteDiagramHandler(IDiagramRepository diagrams)
{
    public async Task<Outcome<bool>> HandleAsync(Guid workspaceId, Guid diagramId, CancellationToken cancellationToken) =>
        await diagrams.DeleteAsync(workspaceId, diagramId, cancellationToken)
            ? Outcome<bool>.Ok(true)
            : Outcome<bool>.Fail(ErrorCodes.DiagramNotFound);
}
