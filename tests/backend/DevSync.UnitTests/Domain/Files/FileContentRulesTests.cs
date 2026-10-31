using System.Text;
using DevSync.Domain.Files;

namespace DevSync.UnitTests.Domain.Files;

/// <summary>Spec 003 FR-016/FR-017: size limit (large file rejection), strict UTF-8, no NUL, exact text.</summary>
public sealed class FileContentRulesTests
{
    [Fact]
    public void Empty_content_is_valid()
    {
        Assert.Null(FileContentRules.Validate([], out var text));
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void Content_at_exactly_the_limit_is_accepted()
    {
        var body = Enumerable.Repeat((byte)'a', ProjectLimits.MaxContentBytes).ToArray();

        Assert.Null(FileContentRules.Validate(body, out var text));
        Assert.Equal(ProjectLimits.MaxContentBytes, text.Length);
    }

    [Fact]
    public void Content_one_byte_over_the_limit_is_rejected()
    {
        var body = Enumerable.Repeat((byte)'a', ProjectLimits.MaxContentBytes + 1).ToArray();

        Assert.Equal(FileErrors.ContentTooLarge, FileContentRules.Validate(body, out _));
    }

    [Fact]
    public void The_limit_is_measured_in_bytes_not_characters()
    {
        // 262,144 four-byte characters = exactly 1 MiB; one more is over even though it is far fewer chars.
        var atLimit = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("𠀀", ProjectLimits.MaxContentBytes / 4)));
        var overLimit = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("𠀀", (ProjectLimits.MaxContentBytes / 4) + 1)));

        Assert.Null(FileContentRules.Validate(atLimit, out _));
        Assert.Equal(FileErrors.ContentTooLarge, FileContentRules.Validate(overLimit, out _));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF })]
    [InlineData(new byte[] { (byte)'a', 0xC3 })]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
    [InlineData(new byte[] { 0xC0, 0xAF })]
    public void Invalid_utf8_is_rejected(byte[] body)
    {
        Assert.Equal(FileErrors.InvalidContent, FileContentRules.Validate(body, out _));
    }

    [Fact]
    public void Nul_characters_are_rejected()
    {
        Assert.Equal(FileErrors.InvalidContent, FileContentRules.Validate("a\0b"u8, out _));
    }

    [Theory]
    [InlineData("line1\r\nline2\r\n")]
    [InlineData("old mac\rline")]
    [InlineData("﻿starts with a BOM")]
    [InlineData("emoji 😀 and CJK 𠀀\n")]
    [InlineData("  leading and trailing spaces  ")]
    public void Text_is_returned_exactly_as_sent(string input)
    {
        Assert.Null(FileContentRules.Validate(Encoding.UTF8.GetBytes(input), out var text));
        Assert.Equal(input, text);
    }
}
