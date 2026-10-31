using DevSync.Application.Diagrams;
using DevSync.Domain.Diagrams;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevSync.Infrastructure.Diagrams;

/// <summary>
/// Diagrams in PostgreSQL (spec 004 research R1, R6, R7). Creates are serialized per workspace by
/// locking its row; saves are one compare-and-swap UPDATE on the revision. Never logs names or documents.
/// </summary>
internal sealed class DiagramRepository(DevSyncDbContext db) : IDiagramRepository
{
    private const string NameIndex = "ux_diagrams_workspace_name";

    public async Task<IReadOnlyList<DiagramSummaryDto>> ListAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var rows = await db.Diagrams.AsNoTracking()
            .Where(d => d.WorkspaceId == workspaceId)
            .Select(d => new DiagramSummaryDto(d.Id, d.Name, d.Revision, d.NodeCount, d.EdgeCount, d.CreatedAt, d.UpdatedAt))
            .ToListAsync(cancellationToken);
        return [.. rows.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Id)];
    }

    public async Task<CreateDiagramStatus> CreateAsync(Diagram diagram, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM workspaces WHERE id = {diagram.WorkspaceId} FOR UPDATE", cancellationToken);

        if (await db.Diagrams.CountAsync(d => d.WorkspaceId == diagram.WorkspaceId, cancellationToken) >= DiagramLimits.MaxDiagramsPerWorkspace)
        {
            return CreateDiagramStatus.TooMany;
        }

        db.Diagrams.Add(diagram);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (IsNameClash(e))
        {
            db.ChangeTracker.Clear();
            return CreateDiagramStatus.NameTaken;
        }

        await transaction.CommitAsync(cancellationToken);
        return CreateDiagramStatus.Created;
    }

    public Task<StoredDiagram?> GetAsync(Guid workspaceId, Guid diagramId, CancellationToken cancellationToken) =>
        db.Diagrams.AsNoTracking()
            .Where(d => d.Id == diagramId && d.WorkspaceId == workspaceId)
            .Select(d => new StoredDiagram(
                new DiagramSummaryDto(d.Id, d.Name, d.Revision, d.NodeCount, d.EdgeCount, d.CreatedAt, d.UpdatedAt),
                d.SchemaVersion,
                d.Document))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<(RenameDiagramStatus Status, DiagramSummaryDto? Summary)> RenameAsync(
        Guid workspaceId, Guid diagramId, DiagramName name, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var diagram = await db.Diagrams.SingleOrDefaultAsync(d => d.Id == diagramId && d.WorkspaceId == workspaceId, cancellationToken);
        if (diagram is null)
        {
            return (RenameDiagramStatus.NotFound, null);
        }

        diagram.Rename(name, userId, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (IsNameClash(e))
        {
            db.ChangeTracker.Clear();
            return (RenameDiagramStatus.NameTaken, null);
        }

        return (RenameDiagramStatus.Renamed, DiagramSummaryDto.From(diagram));
    }

    public async Task<SaveDiagramResult> TrySaveAsync(
        Guid workspaceId, Guid diagramId, long expectedRevision, string canonicalJson, int nodeCount, int edgeCount,
        bool replaceUnsupported, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // The revision check, the "never overwrite a newer format" check and the write are one statement (FR-023, FR-029).
        var updated = await db.Diagrams
            .Where(d => d.Id == diagramId
                && d.WorkspaceId == workspaceId
                && d.Revision == expectedRevision
                && (replaceUnsupported || d.SchemaVersion <= DiagramSchema.CurrentVersion))
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.Document, canonicalJson)
                    .SetProperty(d => d.SchemaVersion, DiagramSchema.CurrentVersion)
                    .SetProperty(d => d.NodeCount, nodeCount)
                    .SetProperty(d => d.EdgeCount, edgeCount)
                    .SetProperty(d => d.Revision, d => d.Revision + 1)
                    .SetProperty(d => d.UpdatedAt, now)
                    .SetProperty(d => d.UpdatedByUserId, (Guid?)userId),
                cancellationToken);

        if (updated == 1)
        {
            return new SaveDiagramResult(SaveDiagramStatus.Saved, expectedRevision + 1, now);
        }

        var current = await db.Diagrams.AsNoTracking()
            .Where(d => d.Id == diagramId && d.WorkspaceId == workspaceId)
            .Select(d => new { d.Revision, d.SchemaVersion })
            .SingleOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            return new SaveDiagramResult(SaveDiagramStatus.NotFound);
        }

        return current.Revision == expectedRevision && current.SchemaVersion > DiagramSchema.CurrentVersion
            ? new SaveDiagramResult(SaveDiagramStatus.StoredUnsupported, current.Revision)
            : new SaveDiagramResult(SaveDiagramStatus.Conflict, current.Revision);
    }

    public async Task<bool> DeleteAsync(Guid workspaceId, Guid diagramId, CancellationToken cancellationToken) =>
        await db.Diagrams.Where(d => d.Id == diagramId && d.WorkspaceId == workspaceId).ExecuteDeleteAsync(cancellationToken) == 1;

    private static bool IsNameClash(DbUpdateException e) =>
        e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: NameIndex };
}
