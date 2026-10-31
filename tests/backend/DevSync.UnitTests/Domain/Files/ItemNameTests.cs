using DevSync.Domain.Files;

namespace DevSync.UnitTests.Domain.Files;

/// <summary>Spec 003 FR-005: name rules, including path-traversal attempts (research R6).</summary>
public sealed class ItemNameTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("server.ts")]
    [InlineData("résumé.md")]
    [InlineData(".eslintrc.json")]
    [InlineData("my file (1).cs")]
    [InlineData("%2e%2e")]
    [InlineData("..a")]
    [InlineData("a..b.py")]
    public void Valid_names_are_accepted_unchanged(string input)
    {
        var result = ItemName.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(input, result.Value.Value);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(255, true)]
    [InlineData(256, false)]
    public void Length_limit_is_255(int length, bool valid)
    {
        Assert.Equal(valid, ItemName.Create(new string('a', length)).IsSuccess);
    }

    [Fact]
    public void Length_is_counted_in_code_points()
    {
        Assert.True(ItemName.Create(string.Concat(Enumerable.Repeat("😀", 255))).IsSuccess);
        Assert.False(ItemName.Create(string.Concat(Enumerable.Repeat("😀", 256))).IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_name_is_rejected(string? input)
    {
        Assert.False(ItemName.Create(input).IsSuccess);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("con:")]
    [InlineData("a\"b")]
    [InlineData("a|b")]
    [InlineData("x?.ts")]
    [InlineData("*.ts")]
    public void Forbidden_characters_are_rejected(string input)
    {
        Assert.False(ItemName.Create(input).IsSuccess);
    }

    [Theory]
    [InlineData("a\u0000b")]
    [InlineData("a\u0001b")]
    [InlineData("a\u001Fb")]
    [InlineData("a\u007Fb")]
    [InlineData("a\nb")]
    [InlineData("a\tb")]
    public void Control_characters_are_rejected(string input)
    {
        Assert.False(ItemName.Create(input).IsSuccess);
    }

    [Theory]
    [InlineData(" x.ts")]
    [InlineData("x.ts ")]
    [InlineData(" ")]
    public void Leading_or_trailing_spaces_are_rejected_not_trimmed(string input)
    {
        Assert.False(ItemName.Create(input).IsSuccess);
    }

    [Theory]
    [InlineData("x.ts.")]
    [InlineData("folder.")]
    [InlineData(".")]
    [InlineData("..")]
    public void Trailing_dot_and_dot_names_are_rejected(string input)
    {
        Assert.False(ItemName.Create(input).IsSuccess);
    }

    [Theory]
    [InlineData("../etc")]
    [InlineData("..\\etc")]
    [InlineData("../../secret.ts")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\a.cs")]
    public void Path_traversal_attempts_are_rejected(string input)
    {
        Assert.False(ItemName.Create(input).IsSuccess);
    }

    [Fact]
    public void Lone_surrogates_are_rejected()
    {
        Assert.False(ItemName.Create("a\uD800.ts").IsSuccess);
        Assert.False(ItemName.Create("a\uDC00.ts").IsSuccess);
    }

    [Fact]
    public void Failures_name_the_broken_rule_on_the_name_field()
    {
        var error = Assert.Single(ItemName.Create("a/b").Errors);

        Assert.Equal("name", error.Field);
        Assert.Contains("/", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalized_is_upper_invariant()
    {
        Assert.Equal("README.MD", ItemName.Create("ReadMe.md").Value.Normalized);
    }

    [Theory]
    [InlineData("a.b.TS", "TS")]
    [InlineData("server.ts", "ts")]
    [InlineData(".eslintrc.json", "json")]
    [InlineData("Makefile", null)]
    [InlineData(".gitignore", "gitignore")]
    public void Extension_is_the_text_after_the_last_dot(string input, string? extension)
    {
        Assert.Equal(extension, ItemName.Create(input).Value.Extension);
    }
}
