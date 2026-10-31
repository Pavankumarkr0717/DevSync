using DevSync.Application.Common;

namespace DevSync.Application.Files;

/// <summary>Renames a file or folder, keeping its id, content and children (spec 003 FR-013). Authorization (EditFiles) is checked by the caller.</summary>
public sealed class RenameItemHandler(IProjectFileRepository files, TimeProvider time)
{
    public async Task<Outcome<ItemDto>> HandleAsync(Guid workspaceId, Guid itemId, string? name, CancellationToken cancellationToken)
    {
        await using var edit = await files.BeginTreeEditAsync(workspaceId, cancellationToken);
        if (edit.Tree.Rename(itemId, name, time.GetUtcNow()) is { } error)
        {
            return FileOutcomes.From<ItemDto>(error);
        }

        return await edit.CommitAsync(cancellationToken)
            ? Outcome<ItemDto>.Ok(ItemDto.From(edit.Tree.Find(itemId)!))
            : Outcome<ItemDto>.Fail(ErrorCodes.NameTaken);
    }
}
