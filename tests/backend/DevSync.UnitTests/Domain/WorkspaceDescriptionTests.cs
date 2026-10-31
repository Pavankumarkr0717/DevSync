using DevSync.Domain.Workspaces;

namespace DevSync.UnitTests.Domain;

public sealed class WorkspaceDescriptionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_or_blank_description_becomes_null(string? input)
    {
        var result = WorkspaceDescription.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Description_is_trimmed()
    {
        Assert.Equal("Mock interviews", WorkspaceDescription.Create("  Mock interviews \n").Value!.Value);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Length_limit_is_500(int length, bool valid)
    {
        Assert.Equal(valid, WorkspaceDescription.Create(new string('d', length)).IsSuccess);
    }

    [Fact]
    public void Line_breaks_and_tabs_are_allowed()
    {
        Assert.True(WorkspaceDescription.Create("line one\r\nline two\tcolumn").IsSuccess);
    }

    [Theory]
    [InlineData("bell\u0007")]
    [InlineData("nul\u0000")]
    public void Other_control_characters_are_rejected(string input)
    {
        var result = WorkspaceDescription.Create(input);

        Assert.False(result.IsSuccess);
        Assert.Equal("description", Assert.Single(result.Errors).Field);
    }
}
