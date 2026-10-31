using DevSync.Application.Common;

namespace DevSync.Application.Files;

/// <summary>
/// Moves a file or folder with its subtree to another folder or the top level (spec 003 FR-014).
/// The workspace lock makes crossed moves safe: the second sees the first's result. Authorization
/// (EditFiles) is checked by the caller.
/// </summary>
public sealed class MoveItemHandler(IProjectFileRepository files, TimeProvider time)
{
    public async Task<Outcome<ItemDto>> HandleAsync(Guid workspaceId, Guid itemId, Guid? newParentId, CancellationToken cancellationToken)
    {
        await using var edit = await files.BeginTreeEditAsync(workspaceId, cancellationToken);
        if (edit.Tree.Move(itemId, newParentId, time.GetUtcNow()) is { } error)
        {
            return FileOutcomes.From<ItemDto>(error);
        }

        return await edit.CommitAsync(cancellationToken)
            ? Outcome<ItemDto>.Ok(ItemDto.From(edit.Tree.Find(itemId)!))
            : Outcome<ItemDto>.Fail(ErrorCodes.NameTaken);
    }
}
