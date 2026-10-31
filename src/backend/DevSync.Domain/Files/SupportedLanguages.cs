namespace DevSync.Domain.Files;

/// <summary>The file extensions DevSync accepts and the editor language each implies (spec 003 FR-010, FR-011).</summary>
public static class SupportedLanguages
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cs"] = "csharp",
        ["js"] = "javascript",
        ["jsx"] = "javascript",
        ["ts"] = "typescript",
        ["tsx"] = "typescript",
        ["json"] = "json",
        ["html"] = "html",
        ["css"] = "css",
        ["py"] = "python",
        ["java"] = "java",
        ["md"] = "markdown",
    };

    /// <summary>The language for a file name, or null when its extension is not supported.</summary>
    public static string? TryGetLanguage(ItemName name) =>
        name.Extension is { } extension && ByExtension.TryGetValue(extension, out var language) ? language : null;
}
