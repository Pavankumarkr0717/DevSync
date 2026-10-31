namespace DevSync.Domain.Diagrams;

/// <summary>Limits for diagrams and their documents (spec 004 FR-004–FR-019, research R5/R12).</summary>
public static class DiagramLimits
{
    public const int MaxDiagramsPerWorkspace = 50;
    public const int MaxNameLength = 100;
    public const int MaxNodes = 500;
    public const int MaxEdges = 1000;
    public const int MaxTitle = 100;
    public const int MaxDescription = 2000;
    public const int MaxLabel = 200;
    public const int MaxMetadataEntries = 20;
    public const int MaxMetadataKey = 50;
    public const int MaxMetadataValue = 500;
    public const int MaxIdLength = 64;
    public const double MinSize = 40;
    public const double MaxSize = 2000;
    public const double MaxCoordinate = 100_000;
    public const double MinZoom = 0.1;
    public const double MaxZoom = 4;
    public const double MaxViewportOffset = 10_000_000;
    public const int MaxDocumentBytes = 1024 * 1024;

    /// <summary>A save that breaks many rules reports at most this many problems.</summary>
    public const int MaxProblems = 50;
}

/// <summary>The diagram document format versions (research R4).</summary>
public static class DiagramSchema
{
    public const int CurrentVersion = 1;
}
