using DevSync.Application.Common;
using DevSync.Domain.Files;

namespace DevSync.Application.Files;

public sealed record CreateItemCommand(Guid? ParentId, string? Kind, string? Name);

/// <summary>Creates a folder, or an empty file at revision 1 (spec 003 FR-012). Authorization (EditFiles) is checked by the caller.</summary>
public sealed class CreateItemHandler(IProjectFileRepository files, ICurrentUser currentUser, TimeProvider time)
{
    public async Task<Outcome<ItemDto>> HandleAsync(Guid workspaceId, CreateItemCommand command, CancellationToken cancellationToken)
    {
        ItemKind? kind = command.Kind switch
        {
            "folder" => ItemKind.Folder,
            "file" => ItemKind.File,
            _ => null,
        };
        if (kind is null)
        {
            return Outcome<ItemDto>.Invalid("kind", "Kind must be \"folder\" or \"file\".");
        }

        await using var edit = await files.BeginTreeEditAsync(workspaceId, cancellationToken);
        var error = edit.Tree.Create(
            command.ParentId, kind.Value, command.Name, currentUser.UserId, time.GetUtcNow(), out var item, out var content);
        if (error is not null)
        {
            return FileOutcomes.From<ItemDto>(error);
        }

        edit.Add(item!, content);
        return await edit.CommitAsync(cancellationToken)
            ? Outcome<ItemDto>.Ok(ItemDto.From(item!))
            : Outcome<ItemDto>.Fail(ErrorCodes.NameTaken);
    }
}
