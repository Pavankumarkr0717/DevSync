namespace DevSync.Infrastructure.Persistence;

/// <summary>Bound from the <c>DevSync:Database</c> configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "DevSync:Database";

    public bool ApplyMigrationsOnStartup { get; set; }
}
