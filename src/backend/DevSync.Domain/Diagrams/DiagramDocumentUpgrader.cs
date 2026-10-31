using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevSync.Domain.Diagrams;

public enum UpgradeStatus
{
    Upgraded,
    Unsupported,
    Unreadable,
}

/// <summary>The outcome of reading a stored document: the current-version JSON, or why it can't be read.</summary>
public sealed record UpgradeResult(UpgradeStatus Status, JsonObject? Document, int? Version);

/// <summary>
/// Brings a stored document up to <see cref="DiagramSchema.CurrentVersion"/> with forward-only, pure
/// steps (spec 004 FR-028, research R4). Reads never write: the upgraded form is stored on the next
/// save. A version above the current one, or with no path to it, is never touched (FR-029).
/// </summary>
public sealed class DiagramDocumentUpgrader(IReadOnlyDictionary<int, Func<JsonObject, JsonObject>>? steps = null)
{
    private readonly IReadOnlyDictionary<int, Func<JsonObject, JsonObject>> _steps = steps ?? new Dictionary<int, Func<JsonObject, JsonObject>>();

    /// <summary>Version 1 is the first format, so production has no steps yet.</summary>
    public static DiagramDocumentUpgrader Production { get; } = new();

    public UpgradeResult Upgrade(JsonObject stored)
    {
        if (!TryReadVersion(stored, out var version))
        {
            return new UpgradeResult(UpgradeStatus.Unreadable, null, null);
        }

        if (version > DiagramSchema.CurrentVersion)
        {
            return new UpgradeResult(UpgradeStatus.Unsupported, null, version);
        }

        var document = stored;
        for (var v = version; v < DiagramSchema.CurrentVersion; v++)
        {
            if (!_steps.TryGetValue(v, out var step))
            {
                return new UpgradeResult(UpgradeStatus.Unsupported, null, version);
            }

            document = step(document);
            document["schemaVersion"] = v + 1;
        }

        return new UpgradeResult(UpgradeStatus.Upgraded, document, DiagramSchema.CurrentVersion);
    }

    private static bool TryReadVersion(JsonObject document, out int version)
    {
        version = 0;
        return document["schemaVersion"] is JsonValue value
            && value.GetValueKind() == JsonValueKind.Number
            && value.TryGetValue(out version);
    }
}
