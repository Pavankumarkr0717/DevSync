using DevSync.Application.Common;
using DevSync.Application.Files;

namespace DevSync.UnitTests.Application.Files;

public sealed class SaveFileContentHandlerTests
{
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeProjectFileRepository _files = new();

    private async Task<Guid> NewFileAsync(string name = "a.ts") =>
        (await new CreateItemHandler(_files, new FakeCurrentUser(UserId), new FixedTimeProvider(Now))
            .HandleAsync(WorkspaceId, new CreateItemCommand(null, "file", name), CancellationToken.None)).Value.Id;

    private SaveFileContentHandler Handler() => new(_files, new FakeCurrentUser(UserId), new FixedTimeProvider(Now));

    [Fact]
    public async Task A_save_at_the_current_revision_stores_the_text_and_its_utf8_size()
    {
        var file = await NewFileAsync();

        var outcome = await Handler().HandleAsync(WorkspaceId, file, 1, "é😀", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(new SaveResultDto(2, 6, Now), outcome.Value);
        Assert.Equal(("é😀", 2L, (Guid?)UserId), _files.Contents[file]);
    }

    [Fact]
    public async Task A_stale_revision_is_a_conflict_carrying_the_current_revision()
    {
        var file = await NewFileAsync();
        await Handler().HandleAsync(WorkspaceId, file, 1, "first", CancellationToken.None);

        var outcome = await Handler().HandleAsync(WorkspaceId, file, 1, "second", CancellationToken.None);

        Assert.Equal(ErrorCodes.RevisionConflict, outcome.Error);
        Assert.Equal(2L, outcome.Extra!["currentRevision"]);
        Assert.Equal("first", _files.Contents[file].Content);
    }

    [Fact]
    public async Task A_missing_file_is_not_found()
    {
        var outcome = await Handler().HandleAsync(WorkspaceId, Guid.CreateVersion7(), 1, "x", CancellationToken.None);

        Assert.Equal(ErrorCodes.ItemNotFound, outcome.Error);
    }
}

public sealed class GetFileContentHandlerTests
{
    [Fact]
    public async Task Content_is_returned_with_its_revision_and_folders_are_not_found()
    {
        var workspaceId = Guid.CreateVersion7();
        var files = new FakeProjectFileRepository();
        var create = new CreateItemHandler(files, new FakeCurrentUser(Guid.CreateVersion7()), TimeProvider.System);
        var file = (await create.HandleAsync(workspaceId, new CreateItemCommand(null, "file", "a.md"), CancellationToken.None)).Value;
        var folder = (await create.HandleAsync(workspaceId, new CreateItemCommand(null, "folder", "src"), CancellationToken.None)).Value;
        var handler = new GetFileContentHandler(files);

        Assert.Equal(new FileContentDto(string.Empty, 1), await handler.HandleAsync(workspaceId, file.Id, CancellationToken.None));
        Assert.Null(await handler.HandleAsync(workspaceId, folder.Id, CancellationToken.None));
        Assert.Null(await handler.HandleAsync(Guid.CreateVersion7(), file.Id, CancellationToken.None));
    }
}
