namespace DevSync.Domain.Diagrams;

/// <summary>
/// A diagram's whole content, version 1 (spec 004 FR-022, research R3). Replaced as a unit on every
/// save. Values are as received: <see cref="DiagramValidator"/> decides whether they are acceptable.
/// </summary>
public sealed record DiagramDocument(
    int SchemaVersion,
    IReadOnlyList<DiagramNode> Nodes,
    IReadOnlyList<DiagramEdge> Edges,
    Viewport? Viewport)
{
    public static readonly DiagramDocument Empty = new(DiagramSchema.CurrentVersion, [], [], null);
}

/// <summary>A component on the canvas. <see cref="Type"/> is the wire value, e.g. <c>api_gateway</c>.</summary>
public sealed record DiagramNode(
    string Id,
    string Type,
    string Title,
    string Description,
    IReadOnlyList<MetadataEntry> Metadata,
    Point Position,
    Size Size);

/// <summary>Free-form documentation on a node. Inert text: never evaluated or used as an object key.</summary>
public sealed record MetadataEntry(string Key, string Value);

/// <summary>A directed connection from <see cref="Source"/> to <see cref="Target"/>.</summary>
public sealed record DiagramEdge(string Id, string Source, string Target, string Label);

public sealed record Point(double X, double Y);

public sealed record Size(double Width, double Height);

/// <summary>Where the canvas looks and how far it is zoomed.</summary>
public sealed record Viewport(double X, double Y, double Zoom);

/// <summary>One rule a document breaks: a stable code, the JSON path of the offending value, and a message.</summary>
public sealed record DiagramProblem(string Code, string Path, string Message);
