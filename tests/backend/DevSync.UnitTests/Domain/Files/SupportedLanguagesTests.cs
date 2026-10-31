using DevSync.Domain.Files;

namespace DevSync.UnitTests.Domain.Files;

/// <summary>Spec 003 FR-010/FR-011: the 11-extension allow-list and its languages.</summary>
public sealed class SupportedLanguagesTests
{
    [Theory]
    [InlineData("a.cs", "csharp")]
    [InlineData("a.js", "javascript")]
    [InlineData("a.jsx", "javascript")]
    [InlineData("a.ts", "typescript")]
    [InlineData("a.tsx", "typescript")]
    [InlineData("a.json", "json")]
    [InlineData("a.html", "html")]
    [InlineData("a.css", "css")]
    [InlineData("a.py", "python")]
    [InlineData("a.java", "java")]
    [InlineData("a.md", "markdown")]
    [InlineData("A.CS", "csharp")]
    [InlineData("App.Tsx", "typescript")]
    [InlineData(".eslintrc.json", "json")]
    public void Supported_extensions_map_to_their_language_ignoring_case(string name, string language)
    {
        Assert.Equal(language, SupportedLanguages.TryGetLanguage(ItemName.Create(name).Value));
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("ci.yml")]
    [InlineData("Dockerfile")]
    [InlineData("README")]
    [InlineData(".gitignore")]
    [InlineData("a.ts.bak")]
    public void Anything_else_is_unsupported(string name)
    {
        Assert.Null(SupportedLanguages.TryGetLanguage(ItemName.Create(name).Value));
    }
}
