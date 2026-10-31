using DevSync.Application.Common;

namespace DevSync.Application.Files;

/// <summary>
/// Permanently deletes a file, or a folder with its whole subtree and contents (spec 003 FR-015).
/// Authorization (DeleteFiles, Owner only) is checked by the caller.
/// </summary>
public sealed class DeleteItemHandler(IProjectFileRepository files)
{
    public async Task<Outcome<bool>> HandleAsync(Guid workspaceId, Guid itemId, CancellationToken cancellationToken)
    {
        await using var edit = await files.BeginTreeEditAsync(workspaceId, cancellationToken);
        var item = edit.Tree.Find(itemId);
        if (edit.Tree.Delete(itemId, out _) is { } error)
        {
            return FileOutcomes.From<bool>(error);
        }

        edit.Remove(item!);
        await edit.CommitAsync(cancellationToken);
        return Outcome<bool>.Ok(true);
    }
}
