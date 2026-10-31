namespace DevSync.Domain.Files;

/// <summary>Limits for one workspace's project files (spec 003 FR-005–FR-008, FR-016).</summary>
public static class ProjectLimits
{
    public const int MaxNameLength = 255;
    public const int MaxPathLength = 1024;

    /// <summary>Deepest allowed item; top-level items are at depth 0.</summary>
    public const int MaxDepth = 20;

    public const int MaxItemsPerWorkspace = 5000;
    public const int MaxContentBytes = 1024 * 1024;
}
