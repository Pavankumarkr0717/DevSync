using DevSync.Application.Common;
using DevSync.Application.Files;

namespace DevSync.UnitTests.Application.Files;

public sealed class RenameMoveDeleteHandlerTests
{
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Created = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(1);

    private readonly FakeProjectFileRepository _files = new();

    private async Task<ItemDto> CreateAsync(Guid? parentId, string kind, string name) =>
        (await new CreateItemHandler(_files, new FakeCurrentUser(Guid.CreateVersion7()), new FixedTimeProvider(Created))
            .HandleAsync(WorkspaceId, new CreateItemCommand(parentId, kind, name), CancellationToken.None)).Value;

    [Fact]
    public async Task Rename_and_move_update_the_item_and_its_timestamp()
    {
        var src = await CreateAsync(null, "folder", "src");
        var file = await CreateAsync(null, "file", "a.ts");

        var renamed = await new RenameItemHandler(_files, new FixedTimeProvider(Later))
            .HandleAsync(WorkspaceId, file.Id, "b.ts", CancellationToken.None);
        var moved = await new MoveItemHandler(_files, new FixedTimeProvider(Later))
            .HandleAsync(WorkspaceId, file.Id, src.Id, CancellationToken.None);

        Assert.Equal("b.ts", renamed.Value.Name);
        Assert.Equal(Later, renamed.Value.UpdatedAt);
        Assert.Equal(src.Id, moved.Value.ParentId);
        Assert.Equal(4, _files.Commits); // two creates, the rename and the move
    }

    [Fact]
    public async Task Rule_failures_pass_through_and_nothing_is_committed()
    {
        var src = await CreateAsync(null, "folder", "src");
        var file = await CreateAsync(null, "file", "a.ts");
        await CreateAsync(null, "file", "b.ts");
        var rename = new RenameItemHandler(_files, TimeProvider.System);
        var move = new MoveItemHandler(_files, TimeProvider.System);

        Assert.Equal(ErrorCodes.InvalidName, (await rename.HandleAsync(WorkspaceId, file.Id, "../x.ts", CancellationToken.None)).Error);
        Assert.Equal(ErrorCodes.NameTaken, (await rename.HandleAsync(WorkspaceId, file.Id, "B.TS", CancellationToken.None)).Error);
        Assert.Equal(ErrorCodes.UnsupportedExtension, (await rename.HandleAsync(WorkspaceId, file.Id, "SRC", CancellationToken.None)).Error);
        Assert.Equal(ErrorCodes.InvalidMove, (await move.HandleAsync(WorkspaceId, src.Id, src.Id, CancellationToken.None)).Error);
        Assert.Equal(ErrorCodes.ItemNotFound, (await move.HandleAsync(WorkspaceId, Guid.CreateVersion7(), null, CancellationToken.None)).Error);
        Assert.Equal(3, _files.Commits); // only the three creates
    }

    [Fact]
    public async Task Delete_removes_the_item_and_its_subtree()
    {
        var src = await CreateAsync(null, "folder", "src");
        var inner = await CreateAsync(src.Id, "file", "a.ts");
        var keep = await CreateAsync(null, "file", "keep.md");

        var outcome = await new DeleteItemHandler(_files).HandleAsync(WorkspaceId, src.Id, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal([keep.Id], _files.Items[WorkspaceId].Select(i => i.Id));
        Assert.False(_files.Contents.ContainsKey(inner.Id));
        Assert.Equal(ErrorCodes.ItemNotFound, (await new DeleteItemHandler(_files).HandleAsync(WorkspaceId, src.Id, CancellationToken.None)).Error);
    }
}
