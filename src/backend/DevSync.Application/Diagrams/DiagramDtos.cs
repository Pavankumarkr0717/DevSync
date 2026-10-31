using System.Text.Json;
using DevSync.Domain.Diagrams;

namespace DevSync.Application.Diagrams;

/// <summary>A diagram in the list (contracts/openapi.yaml <c>DiagramSummary</c>): no document.</summary>
public sealed record DiagramSummaryDto(
    Guid Id, string Name, long Revision, int NodeCount, int EdgeCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static DiagramSummaryDto From(Diagram d) =>
        new(d.Id, d.Name, d.Revision, d.NodeCount, d.EdgeCount, d.CreatedAt, d.UpdatedAt);
}

/// <summary>
/// A diagram with its document upgraded to the current version, or <c>Document = null</c> and
/// <see cref="UnsupportedSchemaVersion"/> set when this release can't read it (0 = unreadable).
/// </summary>
public sealed record DiagramDto(
    Guid Id,
    string Name,
    long Revision,
    int NodeCount,
    int EdgeCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    JsonElement? Document,
    int? UnsupportedSchemaVersion);

public sealed record SaveDiagramResultDto(long Revision, int NodeCount, int EdgeCount, DateTimeOffset UpdatedAt);
