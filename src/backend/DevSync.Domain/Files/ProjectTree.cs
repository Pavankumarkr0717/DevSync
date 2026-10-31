using DevSync.Domain.Common;

namespace DevSync.Domain.Files;

/// <summary>
/// One workspace's folders and files, loaded whole so every structural rule can be checked before
/// anything changes (spec 003 research R2). The caller holds the workspace lock while using it, so
/// the checks cannot race.
/// Invariants: no cycles; every parent is a folder of this workspace; sibling names are unique
/// ignoring case; no item deeper than <see cref="ProjectLimits.MaxDepth"/>; no path longer than
/// <see cref="ProjectLimits.MaxPathLength"/>; at most <see cref="ProjectLimits.MaxItemsPerWorkspace"/> items.
/// </summary>
public sealed class ProjectTree
{
    private readonly Guid _workspaceId;
    private readonly Dictionary<Guid, WorkspaceItem> _items;

    public ProjectTree(Guid workspaceId, IEnumerable<WorkspaceItem> items)
    {
        _workspaceId = workspaceId;
        _items = items.ToDictionary(i => i.Id);
    }

    public IReadOnlyCollection<WorkspaceItem> Items => _items.Values;

    public WorkspaceItem? Find(Guid itemId) => _items.GetValueOrDefault(itemId);

    /// <summary>Creates a folder, or a file with empty content at revision 1. Changes nothing on failure.</summary>
    public DomainError? Create(
        Guid? parentId, ItemKind kind, string? name, Guid userId, DateTimeOffset now,
        out WorkspaceItem? item, out FileContent? content)
    {
        item = null;
        content = null;

        if (parentId is { } pid && !IsFolder(pid))
        {
            return FileErrors.ParentNotFound;
        }

        if (CheckName(name, kind, out var itemName) is { } nameError)
        {
            return nameError;
        }

        if (HasSibling(parentId, itemName.Normalized, except: null))
        {
            return FileErrors.NameTaken;
        }

        if (DepthBelow(parentId) > ProjectLimits.MaxDepth)
        {
            return FileErrors.TooDeep;
        }

        if (PrefixLength(parentId) + Text.CodePointLength(itemName.Value) > ProjectLimits.MaxPathLength)
        {
            return FileErrors.PathTooLong;
        }

        if (_items.Count >= ProjectLimits.MaxItemsPerWorkspace)
        {
            return FileErrors.TooManyItems;
        }

        item = WorkspaceItem.Create(_workspaceId, parentId, kind, itemName, userId, now);
        content = kind == ItemKind.File ? FileContent.CreateEmpty(item.Id, userId, now) : null;
        _items.Add(item.Id, item);
        return null;
    }

    /// <summary>Renames an item, keeping its id, content and children. A change of letter case only is allowed.</summary>
    public DomainError? Rename(Guid itemId, string? name, DateTimeOffset now)
    {
        if (Find(itemId) is not { } item)
        {
            return FileErrors.ItemNotFound;
        }

        if (CheckName(name, item.Kind, out var itemName) is { } nameError)
        {
            return nameError;
        }

        if (HasSibling(item.ParentId, itemName.Normalized, except: item.Id))
        {
            return FileErrors.NameTaken;
        }

        if (PrefixLength(item.ParentId) + Text.CodePointLength(itemName.Value) + LongestSuffix(item) > ProjectLimits.MaxPathLength)
        {
            return FileErrors.PathTooLong;
        }

        item.Rename(itemName, now);
        return null;
    }

    /// <summary>Moves an item and its subtree to another folder or the top level. Moving to the current parent is a no-op.</summary>
    public DomainError? Move(Guid itemId, Guid? newParentId, DateTimeOffset now)
    {
        if (Find(itemId) is not { } item)
        {
            return FileErrors.ItemNotFound;
        }

        if (newParentId is { } pid && !IsFolder(pid))
        {
            return FileErrors.ParentNotFound;
        }

        if (newParentId == item.ParentId)
        {
            return null;
        }

        if (newParentId is { } target && (target == item.Id || IsAncestor(item.Id, target)))
        {
            return FileErrors.InvalidMove;
        }

        if (HasSibling(newParentId, item.NormalizedName, except: item.Id))
        {
            return FileErrors.NameTaken;
        }

        if (DepthBelow(newParentId) + Height(item) > ProjectLimits.MaxDepth)
        {
            return FileErrors.TooDeep;
        }

        if (PrefixLength(newParentId) + Text.CodePointLength(item.Name) + LongestSuffix(item) > ProjectLimits.MaxPathLength)
        {
            return FileErrors.PathTooLong;
        }

        item.MoveTo(newParentId, now);
        return null;
    }

    /// <summary>Removes an item and its whole subtree. <paramref name="removed"/> lists every removed id.</summary>
    public DomainError? Delete(Guid itemId, out IReadOnlyList<Guid> removed)
    {
        if (Find(itemId) is not { } item)
        {
            removed = [];
            return FileErrors.ItemNotFound;
        }

        var ids = new List<Guid> { item.Id };
        ids.AddRange(Descendants(item).Select(d => d.Id));
        foreach (var id in ids)
        {
            _items.Remove(id);
        }

        removed = ids;
        return null;
    }

    /// <summary>The item's names from the top level, joined with <c>/</c>.</summary>
    public string PathOf(Guid itemId)
    {
        var names = new List<string>();
        for (var current = Find(itemId); current is not null; current = current.ParentId is { } p ? Find(p) : null)
        {
            names.Add(current.Name);
        }

        names.Reverse();
        return string.Join('/', names);
    }

    private static DomainError? CheckName(string? name, ItemKind kind, out ItemName itemName)
    {
        var result = ItemName.Create(name);
        if (!result.IsSuccess)
        {
            itemName = null!;
            return FileErrors.InvalidName(result.Errors[0].Message);
        }

        itemName = result.Value;
        return kind == ItemKind.File && SupportedLanguages.TryGetLanguage(itemName) is null
            ? FileErrors.UnsupportedExtension
            : null;
    }

    private bool IsFolder(Guid id) => Find(id) is { Kind: ItemKind.Folder };

    private bool HasSibling(Guid? parentId, string normalizedName, Guid? except) =>
        _items.Values.Any(i => i.ParentId == parentId && i.Id != except && i.NormalizedName == normalizedName);

    /// <summary>The depth an item placed directly in <paramref name="parentId"/> would have.</summary>
    private int DepthBelow(Guid? parentId)
    {
        var depth = 0;
        for (var current = parentId; current is { } id; current = _items[id].ParentId)
        {
            depth++;
        }

        return depth;
    }

    /// <summary>Path length (code points) of <paramref name="parentId"/> plus a separator; 0 for the top level.</summary>
    private int PrefixLength(Guid? parentId) =>
        parentId is { } id ? Text.CodePointLength(PathOf(id)) + 1 : 0;

    private bool IsAncestor(Guid ancestorId, Guid itemId)
    {
        for (var current = _items[itemId].ParentId; current is { } id; current = _items[id].ParentId)
        {
            if (id == ancestorId)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<WorkspaceItem> Children(Guid parentId) => _items.Values.Where(i => i.ParentId == parentId);

    private IEnumerable<WorkspaceItem> Descendants(WorkspaceItem item)
    {
        foreach (var child in Children(item.Id))
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Levels below the item (0 for a file or an empty folder).</summary>
    private int Height(WorkspaceItem item) =>
        Children(item.Id).Select(c => Height(c) + 1).DefaultIfEmpty(0).Max();

    /// <summary>The longest path tail below the item, including the leading separators (0 if none).</summary>
    private int LongestSuffix(WorkspaceItem item) =>
        Children(item.Id).Select(c => 1 + Text.CodePointLength(c.Name) + LongestSuffix(c)).DefaultIfEmpty(0).Max();
}
