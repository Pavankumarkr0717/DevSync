using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DevSync.Domain.Diagrams;

namespace DevSync.Application.Diagrams;

/// <summary>
/// The diagram document's JSON form (contracts/diagram-document.v1.schema.json). Parsing is strict
/// (research R5): unknown or repeated properties, missing properties, nulls where text is expected,
/// comments and trailing commas are all refused. The server stores only its own canonical
/// re-serialization, so nothing but known fields ever reaches the database.
/// </summary>
public static class DiagramJson
{
    private static readonly JsonSerializerOptions Strict = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 16,
    };

    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public enum ParseStatus
    {
        Ok,
        Malformed,
        UnsupportedVersion,
    }

    /// <summary>Parses a submitted document. The version is checked before the shape, so a newer format gets a clear answer.</summary>
    public static (ParseStatus Status, DiagramDocument? Document, string? Error) Parse(ReadOnlySpan<byte> json)
    {
        int? version = null;
        try
        {
            using var peek = JsonDocument.Parse(json.ToArray());
            if (peek.RootElement.ValueKind == JsonValueKind.Object
                && peek.RootElement.TryGetProperty("schemaVersion", out var v)
                && v.ValueKind == JsonValueKind.Number
                && v.TryGetInt32(out var parsed))
            {
                version = parsed;
            }
        }
        catch (JsonException e)
        {
            return (ParseStatus.Malformed, null, Describe(e));
        }

        if (version is { } submitted && submitted != DiagramSchema.CurrentVersion)
        {
            return (ParseStatus.UnsupportedVersion, null, $"Format version {submitted} is not supported; send version {DiagramSchema.CurrentVersion}.");
        }

        try
        {
            var wire = JsonSerializer.Deserialize<WireDocument>(json, Strict);
            return wire is null
                ? (ParseStatus.Malformed, null, "The document is empty.")
                : (ParseStatus.Ok, wire.ToDomain(), null);
        }
        catch (JsonException e)
        {
            return (ParseStatus.Malformed, null, Describe(e));
        }
    }

    /// <summary>Reads a stored document, upgrading older versions (research R4).</summary>
    public static (UpgradeStatus Status, DiagramDocument? Document, int? Version) ReadStored(string json, DiagramDocumentUpgrader upgrader)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return (UpgradeStatus.Unreadable, null, null);
        }

        if (root is null)
        {
            return (UpgradeStatus.Unreadable, null, null);
        }

        var upgraded = upgrader.Upgrade(root);
        if (upgraded.Status != UpgradeStatus.Upgraded)
        {
            return (upgraded.Status, null, upgraded.Version);
        }

        try
        {
            var wire = upgraded.Document.Deserialize<WireDocument>(Strict);
            return wire is null ? (UpgradeStatus.Unreadable, null, null) : (UpgradeStatus.Upgraded, wire.ToDomain(), DiagramSchema.CurrentVersion);
        }
        catch (JsonException)
        {
            return (UpgradeStatus.Unreadable, null, null);
        }
    }

    /// <summary>A stable form: nodes and edges sorted by id, fixed property order, text exactly as given.</summary>
    public static string ToCanonical(DiagramDocument document) =>
        JsonSerializer.Serialize(WireDocument.FromDomain(document), Canonical);

    public static JsonElement ToElement(DiagramDocument document) =>
        JsonSerializer.SerializeToElement(WireDocument.FromDomain(document), Canonical);

    // The parser's message names the path and problem; it never echoes values.
    private static string Describe(JsonException e) =>
        e.Path is { Length: > 0 } path ? $"Malformed JSON at {path}." : "Malformed JSON.";

    private sealed record WireDocument(int SchemaVersion, List<WireNode> Nodes, List<WireEdge> Edges, WireViewport? Viewport)
    {
        public DiagramDocument ToDomain() => new(
            SchemaVersion,
            [.. Nodes.Select(n => n.ToDomain())],
            [.. Edges.Select(e => new DiagramEdge(e.Id, e.Source, e.Target, e.Label))],
            Viewport is null ? null : new Viewport(Viewport.X, Viewport.Y, Viewport.Zoom));

        public static WireDocument FromDomain(DiagramDocument d) => new(
            d.SchemaVersion,
            [.. d.Nodes.OrderBy(n => n.Id, StringComparer.Ordinal).Select(WireNode.FromDomain)],
            [.. d.Edges.OrderBy(e => e.Id, StringComparer.Ordinal).Select(e => new WireEdge(e.Id, e.Source, e.Target, e.Label))],
            d.Viewport is null ? null : new WireViewport(d.Viewport.X, d.Viewport.Y, d.Viewport.Zoom));
    }

    private sealed record WireNode(
        string Id, string Type, string Title, string Description, List<WireMetadata> Metadata, WirePoint Position, WireSize Size)
    {
        public DiagramNode ToDomain() => new(
            Id, Type, Title, Description, [.. Metadata.Select(m => new MetadataEntry(m.Key, m.Value))],
            new Point(Position.X, Position.Y), new Size(Size.Width, Size.Height));

        public static WireNode FromDomain(DiagramNode n) => new(
            n.Id, n.Type, n.Title, n.Description, [.. n.Metadata.Select(m => new WireMetadata(m.Key, m.Value))],
            new WirePoint(n.Position.X, n.Position.Y), new WireSize(n.Size.Width, n.Size.Height));
    }

    private sealed record WireMetadata(string Key, string Value);

    private sealed record WireEdge(string Id, string Source, string Target, string Label);

    private sealed record WirePoint(double X, double Y);

    private sealed record WireSize(double Width, double Height);

    private sealed record WireViewport(double X, double Y, double Zoom);
}
