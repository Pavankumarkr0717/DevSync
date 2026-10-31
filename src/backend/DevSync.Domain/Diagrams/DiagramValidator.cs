using System.Text;
using DevSync.Domain.Common;

namespace DevSync.Domain.Diagrams;

/// <summary>
/// The authoritative structural rules for a diagram document (spec 004 FR-033, research R5). Collects
/// every problem (up to <see cref="DiagramLimits.MaxProblems"/>) so the person sees them all at once.
/// The app checks some of these too, for instant feedback only.
/// </summary>
public static class DiagramValidator
{
    public static IReadOnlyList<DiagramProblem> Validate(DiagramDocument document)
    {
        var problems = new Problems();

        if (document.Nodes.Count > DiagramLimits.MaxNodes)
        {
            problems.Add("too_many_nodes", "nodes", $"A diagram can have at most {DiagramLimits.MaxNodes} components.");
        }

        if (document.Edges.Count > DiagramLimits.MaxEdges)
        {
            problems.Add("too_many_edges", "edges", $"A diagram can have at most {DiagramLimits.MaxEdges} connections.");
        }

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < document.Nodes.Count; i++)
        {
            ValidateNode(document.Nodes[i], $"nodes[{i}]", nodeIds, problems);
        }

        var edgeIds = new HashSet<string>(StringComparer.Ordinal);
        var pairs = new HashSet<(string, string)>();
        for (var i = 0; i < document.Edges.Count; i++)
        {
            ValidateEdge(document.Edges[i], $"edges[{i}]", nodeIds, edgeIds, pairs, problems);
        }

        if (document.Viewport is { } viewport)
        {
            Range(viewport.X, -DiagramLimits.MaxViewportOffset, DiagramLimits.MaxViewportOffset, "viewport.x", problems);
            Range(viewport.Y, -DiagramLimits.MaxViewportOffset, DiagramLimits.MaxViewportOffset, "viewport.y", problems);
            Range(viewport.Zoom, DiagramLimits.MinZoom, DiagramLimits.MaxZoom, "viewport.zoom", problems);
        }

        return problems.List;
    }

    private static void ValidateNode(DiagramNode node, string path, HashSet<string> nodeIds, Problems problems)
    {
        if (Id(node.Id, $"{path}.id", problems) && !nodeIds.Add(node.Id))
        {
            problems.Add("duplicate_node_id", $"{path}.id", $"Two components have the id \"{node.Id}\".");
        }

        if (!ComponentTypes.IsKnown(node.Type))
        {
            problems.Add("unknown_node_type", $"{path}.type", $"\"{node.Type}\" is not a known component type.");
        }

        Text(node.Title, $"{path}.title", required: true, DiagramLimits.MaxTitle, allowLineBreaks: false, problems);
        Text(node.Description, $"{path}.description", required: false, DiagramLimits.MaxDescription, allowLineBreaks: true, problems);

        if (node.Metadata.Count > DiagramLimits.MaxMetadataEntries)
        {
            problems.Add("too_many_metadata", $"{path}.metadata", $"A component can have at most {DiagramLimits.MaxMetadataEntries} metadata entries.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var m = 0; m < node.Metadata.Count; m++)
        {
            var entry = node.Metadata[m];
            var entryPath = $"{path}.metadata[{m}]";
            if (Text(entry.Key, $"{entryPath}.key", required: true, DiagramLimits.MaxMetadataKey, allowLineBreaks: false, problems)
                && !keys.Add(entry.Key.ToUpperInvariant()))
            {
                problems.Add("duplicate_metadata_key", $"{entryPath}.key", $"The metadata key \"{entry.Key}\" is used twice.");
            }

            Text(entry.Value, $"{entryPath}.value", required: false, DiagramLimits.MaxMetadataValue, allowLineBreaks: false, problems);
        }

        Range(node.Position.X, -DiagramLimits.MaxCoordinate, DiagramLimits.MaxCoordinate, $"{path}.position.x", problems);
        Range(node.Position.Y, -DiagramLimits.MaxCoordinate, DiagramLimits.MaxCoordinate, $"{path}.position.y", problems);
        Range(node.Size.Width, DiagramLimits.MinSize, DiagramLimits.MaxSize, $"{path}.size.width", problems);
        Range(node.Size.Height, DiagramLimits.MinSize, DiagramLimits.MaxSize, $"{path}.size.height", problems);
    }

    private static void ValidateEdge(
        DiagramEdge edge, string path, HashSet<string> nodeIds, HashSet<string> edgeIds,
        HashSet<(string, string)> pairs, Problems problems)
    {
        if (Id(edge.Id, $"{path}.id", problems) && (nodeIds.Contains(edge.Id) || !edgeIds.Add(edge.Id)))
        {
            problems.Add("duplicate_edge_id", $"{path}.id", $"The id \"{edge.Id}\" is used more than once.");
        }

        var sourceKnown = nodeIds.Contains(edge.Source);
        var targetKnown = nodeIds.Contains(edge.Target);
        if (!sourceKnown)
        {
            problems.Add("unknown_node_reference", $"{path}.source", $"Connection \"{edge.Id}\" starts at unknown component \"{edge.Source}\".");
        }

        if (!targetKnown)
        {
            problems.Add("unknown_node_reference", $"{path}.target", $"Connection \"{edge.Id}\" refers to unknown component \"{edge.Target}\".");
        }

        if (sourceKnown && targetKnown)
        {
            if (edge.Source == edge.Target)
            {
                problems.Add("self_connection", path, "A component can't connect to itself.");
            }
            else if (!pairs.Add((edge.Source, edge.Target)))
            {
                problems.Add("duplicate_edge", path, "These components are already connected in this direction.");
            }
        }

        Text(edge.Label, $"{path}.label", required: false, DiagramLimits.MaxLabel, allowLineBreaks: false, problems);
    }

    private static bool Id(string id, string path, Problems problems)
    {
        var valid = id.Length is > 0 and <= DiagramLimits.MaxIdLength
            && id.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-');
        if (!valid)
        {
            problems.Add("invalid_id", path, $"Ids are 1–{DiagramLimits.MaxIdLength} letters, digits, \"_\" or \"-\".");
        }

        return valid;
    }

    private static bool Text(string value, string path, bool required, int max, bool allowLineBreaks, Problems problems)
    {
        if (required && string.IsNullOrWhiteSpace(value))
        {
            problems.Add("text_required", path, "This text is required.");
            return false;
        }

        if (!IsWellFormed(value) || value.Any(c => (char.IsControl(c) && !(allowLineBreaks && c is '\n' or '\r' or '\t'))))
        {
            problems.Add("invalid_text", path, "The text contains characters that can't be saved.");
            return false;
        }

        if (Common.Text.CodePointLength(value) > max)
        {
            problems.Add("text_too_long", path, $"At most {max} characters.");
            return false;
        }

        return true;
    }

    private static void Range(double value, double min, double max, string path, Problems problems)
    {
        if (!double.IsFinite(value) || value < min || value > max)
        {
            problems.Add("out_of_range", path, $"Must be between {min} and {max}.");
        }
    }

    private static bool IsWellFormed(string value)
    {
        var span = value.AsSpan();
        while (!span.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(span, out _, out var consumed) != System.Buffers.OperationStatus.Done)
            {
                return false;
            }

            span = span[consumed..];
        }

        return true;
    }

    private sealed class Problems
    {
        public List<DiagramProblem> List { get; } = [];

        public void Add(string code, string path, string message)
        {
            if (List.Count < DiagramLimits.MaxProblems)
            {
                List.Add(new DiagramProblem(code, path, message));
            }
        }
    }
}
