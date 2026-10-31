namespace DevSync.Domain.Files;

/// <summary>
/// The text of one file and its revision (spec 003 FR-017, FR-018). Revision 1 is the empty file
/// created with the item; every successful save adds one. Saves are applied by the repository as a
/// compare-and-swap on <see cref="Revision"/>, so this type only describes the stored state.
/// </summary>
public sealed class FileContent
{
    // Used by EF Core when materializing.
    private FileContent()
    {
        Content = string.Empty;
    }

    private FileContent(Guid itemId, Guid updatedBy, DateTimeOffset now)
    {
        ItemId = itemId;
        Content = string.Empty;
        SizeBytes = 0;
        Revision = 1;
        UpdatedAt = now;
        UpdatedByUserId = updatedBy;
    }

    public Guid ItemId { get; private set; }

    public string Content { get; private set; }

    /// <summary>UTF-8 byte count of <see cref="Content"/>.</summary>
    public int SizeBytes { get; private set; }

    public long Revision { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    internal static FileContent CreateEmpty(Guid itemId, Guid createdBy, DateTimeOffset now) => new(itemId, createdBy, now);
}
