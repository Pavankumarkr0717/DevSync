using DevSync.Application.Files;
using DevSync.Domain.Files;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace DevSync.Infrastructure.Files;

/// <summary>
/// Project files in PostgreSQL. Structure changes lock the workspace row (research R2); content saves
/// are a single compare-and-swap UPDATE on the revision (research R4). Never logs names or content.
/// </summary>
internal sealed class ProjectFileRepository(DevSyncDbContext db) : IProjectFileRepository
{
    private const string SiblingNameIndex = "ux_workspace_items_sibling_name";

    public async Task<ITreeEdit> BeginTreeEditAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serializes every structure change of this workspace until commit or rollback.
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM workspaces WHERE id = {workspaceId} FOR UPDATE", cancellationToken);
            var items = await db.WorkspaceItems.Where(i => i.WorkspaceId == workspaceId).ToListAsync(cancellationToken);
            return new TreeEdit(db, transaction, new ProjectTree(workspaceId, items));
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<ItemDto>> GetTreeAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await db.WorkspaceItems.AsNoTracking()
            .Where(i => i.WorkspaceId == workspaceId)
            .Select(i => new ItemDto(i.Id, i.ParentId, i.Kind, i.Name, i.CreatedAt, i.UpdatedAt))
            .ToListAsync(cancellationToken);

    public Task<FileContentDto?> GetContentAsync(Guid workspaceId, Guid itemId, CancellationToken cancellationToken) =>
        (from c in db.FileContents.AsNoTracking()
         join i in db.WorkspaceItems.AsNoTracking() on c.ItemId equals i.Id
         where i.Id == itemId && i.WorkspaceId == workspaceId && i.Kind == ItemKind.File
         select new FileContentDto(c.Content, c.Revision))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<SaveContentResult> TrySaveContentAsync(
        Guid workspaceId, Guid itemId, long expectedRevision, string content, int sizeBytes,
        Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // The revision check and the write are one statement, so two saves from the same revision
        // can never both succeed (FR-019).
        var updated = await db.FileContents
            .Where(c => c.ItemId == itemId
                && c.Revision == expectedRevision
                && db.WorkspaceItems.Any(i => i.Id == itemId && i.WorkspaceId == workspaceId))
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.Content, content)
                    .SetProperty(c => c.SizeBytes, sizeBytes)
                    .SetProperty(c => c.Revision, c => c.Revision + 1)
                    .SetProperty(c => c.UpdatedAt, now)
                    .SetProperty(c => c.UpdatedByUserId, (Guid?)userId),
                cancellationToken);

        if (updated == 1)
        {
            return new SaveContentResult(SaveContentStatus.Saved, expectedRevision + 1, now);
        }

        // Nothing matched: the file moved on (conflict) or is gone (deleted, or never in this workspace).
        var current = await (
                from c in db.FileContents.AsNoTracking()
                join i in db.WorkspaceItems.AsNoTracking() on c.ItemId equals i.Id
                where i.Id == itemId && i.WorkspaceId == workspaceId
                select (long?)c.Revision)
            .SingleOrDefaultAsync(cancellationToken);

        return current is { } revision
            ? new SaveContentResult(SaveContentStatus.Conflict, revision)
            : new SaveContentResult(SaveContentStatus.NotFound);
    }

    private sealed class TreeEdit(DevSyncDbContext db, IDbContextTransaction transaction, ProjectTree tree) : ITreeEdit
    {
        private readonly List<Guid> _removed = [];
        private bool _committed;

        public ProjectTree Tree { get; } = tree;

        public void Add(WorkspaceItem item, FileContent? content)
        {
            db.WorkspaceItems.Add(item);
            if (content is not null)
            {
                db.FileContents.Add(content);
            }
        }

        public void Remove(WorkspaceItem item) => _removed.Add(item.Id);

        public async Task<bool> CommitAsync(CancellationToken cancellationToken)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: SiblingNameIndex,
            })
            {
                return false;
            }

            // One DELETE per removed root; ON DELETE CASCADE removes the subtree and its contents atomically.
            if (_removed.Count > 0)
            {
                await db.WorkspaceItems.Where(i => _removed.Contains(i.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _committed = true;
            return true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
            {
                db.ChangeTracker.Clear();
            }

            await transaction.DisposeAsync(); // rolls back when not committed
        }
    }
}
