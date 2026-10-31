using DevSync.Domain.Workspaces;

namespace DevSync.UnitTests.Domain;

public sealed class WorkspaceNameTests
{
    [Fact]
    public void Name_is_trimmed()
    {
        var result = WorkspaceName.Create("  Interview Prep  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Interview Prep", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t  ")]
    public void Missing_or_blank_name_is_rejected(string? input)
    {
        var result = WorkspaceName.Create(input);

        Assert.False(result.IsSuccess);
        Assert.Equal("name", Assert.Single(result.Errors).Field);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Length_limit_is_100(int length, bool valid)
    {
        Assert.Equal(valid, WorkspaceName.Create(new string('a', length)).IsSuccess);
    }

    [Fact]
    public void Length_is_counted_in_code_points_not_utf16_units()
    {
        const string emoji = "😀"; // one code point, two UTF-16 units

        Assert.True(WorkspaceName.Create(string.Concat(Enumerable.Repeat(emoji, 100))).IsSuccess);
        Assert.False(WorkspaceName.Create(string.Concat(Enumerable.Repeat(emoji, 101))).IsSuccess);
    }

    [Theory]
    [InlineData("bad\u0000name")]
    [InlineData("bad\u001Bname")]
    [InlineData("two\nlines")]
    [InlineData("tab\there")]
    public void Control_characters_are_rejected(string input)
    {
        var result = WorkspaceName.Create(input);

        Assert.False(result.IsSuccess);
        Assert.Equal("name", Assert.Single(result.Errors).Field);
    }
}
