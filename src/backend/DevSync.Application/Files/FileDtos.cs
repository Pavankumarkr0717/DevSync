using DevSync.Domain.Files;

namespace DevSync.Application.Files;

/// <summary>A folder or file in the tree (contracts/openapi.yaml <c>Item</c>). <see cref="ParentId"/> null = top level.</summary>
public sealed record ItemDto(Guid Id, Guid? ParentId, ItemKind Kind, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static ItemDto From(WorkspaceItem item) =>
        new(item.Id, item.ParentId, item.Kind, item.Name, item.CreatedAt, item.UpdatedAt);
}

/// <summary>The server's limits, so the client validates with the same numbers.</summary>
public sealed record TreeLimitsDto(int MaxNameLength, int MaxPathLength, int MaxDepth, int MaxItems, int MaxContentBytes)
{
    public static readonly TreeLimitsDto Current = new(
        ProjectLimits.MaxNameLength,
        ProjectLimits.MaxPathLength,
        ProjectLimits.MaxDepth,
        ProjectLimits.MaxItemsPerWorkspace,
        ProjectLimits.MaxContentBytes);
}

public sealed record TreeDto(IReadOnlyList<ItemDto> Items, TreeLimitsDto Limits);

public sealed record FileContentDto(string Content, long Revision);

public sealed record SaveResultDto(long Revision, int SizeBytes, DateTimeOffset UpdatedAt);
