using DevSync.Domain.Diagrams;

namespace DevSync.Application.Diagrams;

/// <summary>Storage for diagrams: one row each, the document as canonical JSON (spec 004 research R1, R6, R7).</summary>
public interface IDiagramRepository
{
    /// <summary>The workspace's diagrams without documents, sorted by name ignoring case.</summary>
    Task<IReadOnlyList<DiagramSummaryDto>> ListAsync(Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>Adds the diagram unless the workspace is at the limit or the name is taken (serialized per workspace).</summary>
    Task<CreateDiagramStatus> CreateAsync(Diagram diagram, CancellationToken cancellationToken);

    /// <summary>The diagram with its stored JSON; null if missing or in another workspace.</summary>
    Task<StoredDiagram?> GetAsync(Guid workspaceId, Guid diagramId, CancellationToken cancellationToken);

    Task<(RenameDiagramStatus Status, DiagramSummaryDto? Summary)> RenameAsync(
        Guid workspaceId, Guid diagramId, DiagramName name, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the document only if the diagram is still at <paramref name="expectedRevision"/> and
    /// its stored version is supported (or <paramref name="replaceUnsupported"/>), as one statement.
    /// </summary>
    Task<SaveDiagramResult> TrySaveAsync(
        Guid workspaceId, Guid diagramId, long expectedRevision, string canonicalJson, int nodeCount, int edgeCount,
        bool replaceUnsupported, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>True if a diagram was deleted.</summary>
    Task<bool> DeleteAsync(Guid workspaceId, Guid diagramId, CancellationToken cancellationToken);
}

public enum CreateDiagramStatus
{
    Created,
    NameTaken,
    TooMany,
}

public enum RenameDiagramStatus
{
    Renamed,
    NameTaken,
    NotFound,
}

public enum SaveDiagramStatus
{
    Saved,
    Conflict,
    NotFound,
    StoredUnsupported,
}

/// <summary>On <see cref="SaveDiagramStatus.Saved"/> the new revision; on a conflict the current one.</summary>
public sealed record SaveDiagramResult(SaveDiagramStatus Status, long Revision = 0, DateTimeOffset UpdatedAt = default);

public sealed record StoredDiagram(DiagramSummaryDto Summary, int SchemaVersion, string Document);
