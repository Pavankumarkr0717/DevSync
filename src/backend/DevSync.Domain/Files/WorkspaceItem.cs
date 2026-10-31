namespace DevSync.Domain.Files;

/// <summary>
/// A folder or file in one workspace's tree. Its id never changes when it is renamed or moved
/// (spec 003 FR-003). Created and changed only through <see cref="ProjectTree"/>, which enforces
/// the tree rules.
/// </summary>
public sealed class WorkspaceItem
{
    // Used by EF Core when materializing.
    private WorkspaceItem()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    private WorkspaceItem(Guid workspaceId, Guid? parentId, ItemKind kind, ItemName name, Guid createdBy, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7(now);
        WorkspaceId = workspaceId;
        ParentId = parentId;
        Kind = kind;
        Name = name.Value;
        NormalizedName = name.Normalized;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedByUserId = createdBy;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    /// <summary>The containing folder; null for the top level.</summary>
    public Guid? ParentId { get; private set; }

    public ItemKind Kind { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Last create, rename or move. Content saves are tracked on <see cref="FileContent"/>.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    internal static WorkspaceItem Create(Guid workspaceId, Guid? parentId, ItemKind kind, ItemName name, Guid createdBy, DateTimeOffset now) =>
        new(workspaceId, parentId, kind, name, createdBy, now);

    internal void Rename(ItemName name, DateTimeOffset now)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
        UpdatedAt = now;
    }

    internal void MoveTo(Guid? parentId, DateTimeOffset now)
    {
        ParentId = parentId;
        UpdatedAt = now;
    }
}
