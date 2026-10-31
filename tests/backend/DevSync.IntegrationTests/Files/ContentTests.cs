using System.Net;
using System.Text;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Files;

/// <summary>Spec 003 US2: reading and saving content exactly, with revisions and limits (FR-016–FR-019).</summary>
public sealed class ContentTests(PostgresFixture postgres)
{
    private const int Limit = 1024 * 1024;

    [Fact]
    public async Task A_new_file_reads_as_empty_text_at_revision_1_and_is_never_cached()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");

        var response = await team.Viewer.GetContentResponseAsync(team.WorkspaceId, file);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Saving_increments_the_revision_and_returns_the_new_etag()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.py");

        var response = await team.Editor.SaveContentAsync(team.WorkspaceId, file, 1, "print('hi')\n");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"2\"", response.Headers.ETag?.Tag);
        var body = await response.ReadJsonAsync();
        Assert.Equal(2, body.GetProperty("revision").GetInt64());
        Assert.Equal(12, body.GetProperty("sizeBytes").GetInt32());
        Assert.True(body.TryGetProperty("updatedAt", out _));

        var (text, revision) = await team.Viewer.GetContentAsync(team.WorkspaceId, file);
        Assert.Equal("print('hi')\n", text);
        Assert.Equal(2, revision);
    }

    [Theory]
    [InlineData("line1\r\nline2\r\n")]
    [InlineData("old mac\rline")]
    [InlineData("﻿with a BOM")]
    [InlineData("emoji 😀 and CJK 𠀀 and accents é\n")]
    [InlineData("  \t indented and trailing   \n\n")]
    public async Task Content_round_trips_byte_for_byte(string text)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.md");
        var bytes = Encoding.UTF8.GetBytes(text);

        Assert.Equal(HttpStatusCode.OK, (await team.Owner.SaveContentBytesAsync(team.WorkspaceId, file, 1, bytes)).StatusCode);

        var read = await team.Owner.GetContentResponseAsync(team.WorkspaceId, file);
        Assert.Equal(bytes, await read.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Content_at_exactly_1_MiB_is_accepted_and_one_byte_more_is_rejected()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "big.json");

        var tooLarge = await team.Owner.SaveContentBytesAsync(team.WorkspaceId, file, 1, Enumerable.Repeat((byte)'a', Limit + 1).ToArray());
        await tooLarge.AssertProblemAsync(413, "content_too_large");
        Assert.Equal((string.Empty, 1L), await team.Owner.GetContentAsync(team.WorkspaceId, file));

        var atLimit = await team.Owner.SaveContentBytesAsync(team.WorkspaceId, file, 1, Enumerable.Repeat((byte)'a', Limit).ToArray());
        Assert.Equal(HttpStatusCode.OK, atLimit.StatusCode);
        Assert.Equal(Limit, (await atLimit.ReadJsonAsync()).GetProperty("sizeBytes").GetInt32());
    }

    [Fact]
    public async Task A_large_body_without_a_content_length_is_still_cut_off_at_the_limit()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "big.json");
        var request = new HttpRequestMessage(HttpMethod.Put, $"{team.Base}/files/{file}/content")
        {
            Content = new StreamContent(new MemoryStream(new byte[3 * Limit])),
        };
        request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(FileTestData.TextPlain);
        request.Headers.TransferEncodingChunked = true;
        request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");

        await (await team.Owner.SendAsync(request)).AssertProblemAsync(413, "content_too_large");
    }

    [Theory]
    [InlineData(new byte[] { (byte)'a', 0, (byte)'b' })]
    [InlineData(new byte[] { 0xFF })]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
    public async Task Nul_characters_and_invalid_utf8_are_rejected(byte[] body)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");

        await (await team.Owner.SaveContentBytesAsync(team.WorkspaceId, file, 1, body)).AssertProblemAsync(400, "invalid_content");
        Assert.Equal((string.Empty, 1L), await team.Owner.GetContentAsync(team.WorkspaceId, file));
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/html; charset=utf-8")]
    [InlineData("text/plain; charset=iso-8859-1")]
    public async Task Other_content_types_are_rejected(string contentType)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");

        await (await team.Owner.SaveContentAsync(team.WorkspaceId, file, 1, "x", contentType)).AssertProblemAsync(415, "unsupported_media_type");
    }

    [Fact]
    public async Task Plain_text_without_a_charset_is_accepted_as_utf8()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");

        Assert.Equal(HttpStatusCode.OK, (await team.Owner.SaveContentAsync(team.WorkspaceId, file, 1, "é", "text/plain")).StatusCode);
        Assert.Equal("é", (await team.Owner.GetContentAsync(team.WorkspaceId, file)).Text);
    }

    [Fact]
    public async Task A_save_must_state_its_revision_as_one_strong_etag()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, null, "a.ts");
        var body = "x"u8.ToArray();

        await (await team.Owner.SaveContentBytesAsync(team.WorkspaceId, file, null, body)).AssertProblemAsync(428, "revision_required");
        foreach (var ifMatch in new[] { "*", "W/\"1\"", "\"abc\"", "\"0\"", "\"1\", \"2\"", "1" })
        {
            await (await team.Owner.SaveContentBytesAsync(team.WorkspaceId, file, null, body, ifMatch: ifMatch))
                .AssertProblemAsync(400, "malformed_request");
        }

        Assert.Equal((string.Empty, 1L), await team.Owner.GetContentAsync(team.WorkspaceId, file));
    }

    [Fact]
    public async Task Content_of_a_folder_or_a_missing_item_is_not_found()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var folder = await team.Owner.NewFolderAsync(team.WorkspaceId, null, "src");

        await (await team.Owner.GetContentResponseAsync(team.WorkspaceId, folder)).AssertProblemAsync(404, "item_not_found");
        await (await team.Owner.SaveContentAsync(team.WorkspaceId, folder, 1, "x")).AssertProblemAsync(404, "item_not_found");
        await (await team.Owner.GetContentResponseAsync(team.WorkspaceId, Guid.CreateVersion7())).AssertProblemAsync(404, "item_not_found");
    }
}
