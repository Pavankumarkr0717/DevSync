using DevSync.Domain.Common;

namespace DevSync.Domain.Diagrams;

/// <summary>Trimmed; 1–100 code points; no control characters; unique per workspace ignoring case (spec 004 FR-004).</summary>
public sealed record DiagramName
{
    public const string Field = "name";

    private DiagramName(string value)
    {
        Value = value;
        Normalized = value.ToUpperInvariant();
    }

    public string Value { get; }

    public string Normalized { get; }

    public static Result<DiagramName> Create(string? input)
    {
        var trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result<DiagramName>.Failure(Field, "Name is required.");
        }

        if (Text.CodePointLength(trimmed) > DiagramLimits.MaxNameLength)
        {
            return Result<DiagramName>.Failure(Field, $"Name must be at most {DiagramLimits.MaxNameLength} characters.");
        }

        if (Text.ContainsControlCharacters(trimmed))
        {
            return Result<DiagramName>.Failure(Field, "Name must not contain control characters.");
        }

        return Result<DiagramName>.Success(new DiagramName(trimmed));
    }
}

/// <summary>
/// A named architecture diagram in a workspace (spec 004). Its document is stored with it and
/// replaced whole by a revision compare-and-swap in the repository; this type carries the rest.
/// </summary>
public sealed class Diagram
{
    // Used by EF Core when materializing.
    private Diagram()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
        Document = string.Empty;
    }

    private Diagram(Guid workspaceId, DiagramName name, string emptyDocument, Guid createdBy, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7(now);
        WorkspaceId = workspaceId;
        Name = name.Value;
        NormalizedName = name.Normalized;
        Revision = 1;
        SchemaVersion = DiagramSchema.CurrentVersion;
        Document = emptyDocument;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedByUserId = createdBy;
        UpdatedByUserId = createdBy;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    /// <summary>1 for the empty document; +1 on every successful content save; unchanged by rename.</summary>
    public long Revision { get; private set; }

    public int SchemaVersion { get; private set; }

    /// <summary>The stored document as canonical JSON.</summary>
    public string Document { get; private set; }

    public int NodeCount { get; private set; }

    public int EdgeCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    /// <param name="emptyDocument">The canonical JSON of <see cref="DiagramDocument.Empty"/>.</param>
    public static Diagram Create(Guid workspaceId, DiagramName name, string emptyDocument, Guid createdBy, DateTimeOffset now) =>
        new(workspaceId, name, emptyDocument, createdBy, now);

    public void Rename(DiagramName name, Guid by, DateTimeOffset now)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
        UpdatedAt = now;
        UpdatedByUserId = by;
    }
}
