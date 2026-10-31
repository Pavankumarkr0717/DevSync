namespace DevSync.Domain.Diagrams;

/// <summary>The eight component kinds a node can be (spec 004 FR-010), by their stable wire values.</summary>
public static class ComponentTypes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "client",
        "api_gateway",
        "microservice",
        "database",
        "cache",
        "message_queue",
        "load_balancer",
        "external_service",
    };

    public static bool IsKnown(string type) => All.Contains(type);
}
