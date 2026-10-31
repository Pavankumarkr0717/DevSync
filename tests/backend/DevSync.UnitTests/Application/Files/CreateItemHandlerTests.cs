using DevSync.Application.Common;
using DevSync.Application.Files;
using DevSync.Domain.Files;

namespace DevSync.UnitTests.Application.Files;

public sealed class CreateItemHandlerTests
{
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeProjectFileRepository _files = new();

    private CreateItemHandler Handler() => new(_files, new FakeCurrentUser(UserId), new FixedTimeProvider(Now));

    [Fact]
    public async Task A_file_is_created_with_empty_content_at_revision_1()
    {
        var outcome = await Handler().HandleAsync(WorkspaceId, new CreateItemCommand(null, "file", "main.py"), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var item = outcome.Value;
        Assert.Equal(ItemKind.File, item.Kind);
        Assert.Equal("main.py", item.Name);
        Assert.Null(item.ParentId);
        Assert.Equal(Now, item.CreatedAt);
        Assert.Equal((string.Empty, 1L, (Guid?)UserId), _files.Contents[item.Id]);
        Assert.Equal(1, _files.Commits);
    }

    [Fact]
    public async Task A_folder_has_no_content()
    {
        var outcome = await Handler().HandleAsync(WorkspaceId, new CreateItemCommand(null, "folder", "src"), CancellationToken.None);

        Assert.Equal(ItemKind.Folder, outcome.Value.Kind);
        Assert.Empty(_files.Contents);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Folder")]
    [InlineData("symlink")]
    public async Task An_unknown_kind_is_a_validation_error(string? kind)
    {
        var outcome = await Handler().HandleAsync(WorkspaceId, new CreateItemCommand(null, kind, "a.ts"), CancellationToken.None);

        Assert.Equal(ErrorCodes.ValidationFailed, outcome.Error);
        Assert.Equal("kind", Assert.Single(outcome.FieldErrors).Field);
        Assert.Equal(0, _files.Commits);
    }

    [Fact]
    public async Task Rule_failures_pass_through_with_the_rule_as_detail_for_names()
    {
        var invalid = await Handler().HandleAsync(WorkspaceId, new CreateItemCommand(null, "folder", "a/b"), CancellationToken.None);
        Assert.Equal(ErrorCodes.InvalidName, invalid.Error);
        Assert.Contains("/", invalid.Detail, StringComparison.Ordinal);

        var unsupported = await Handler().HandleAsync(WorkspaceId, new CreateItemCommand(null, "file", "notes.txt"), CancellationToken.None);
        Assert.Equal(ErrorCodes.UnsupportedExtension, unsupported.Error);

        await Handler().HandleAsync(WorkspaceId, new CreateItemCommand(null, "folder", "src"), CancellationToken.None);
        var taken = await Handler().HandleAsync(WorkspaceId, new CreateItemCommand(null, "file", "SRC.ts"), CancellationToken.None);
        Assert.True(taken.IsSuccess);
        var duplicate = await Handler().HandleAsync(WorkspaceId, new CreateItemCommand(null, "folder", "Src"), CancellationToken.None);
        Assert.Equal(ErrorCodes.NameTaken, duplicate.Error);
    }

    [Fact]
    public async Task A_name_clash_caught_by_the_database_is_reported_as_name_taken()
    {
        _files.FailNextCommitWithNameClash = true;

        var outcome = await Handler().HandleAsync(WorkspaceId, new CreateItemCommand(null, "folder", "src"), CancellationToken.None);

        Assert.Equal(ErrorCodes.NameTaken, outcome.Error);
    }
}

public sealed class GetTreeHandlerTests
{
    [Fact]
    public async Task The_tree_lists_every_item_with_the_limits()
    {
        var workspaceId = Guid.CreateVersion7();
        var files = new FakeProjectFileRepository();
        var create = new CreateItemHandler(files, new FakeCurrentUser(Guid.CreateVersion7()), TimeProvider.System);
        var src = (await create.HandleAsync(workspaceId, new CreateItemCommand(null, "folder", "src"), CancellationToken.None)).Value;
        await create.HandleAsync(workspaceId, new CreateItemCommand(src.Id, "file", "a.ts"), CancellationToken.None);

        var tree = await new GetTreeHandler(files).HandleAsync(workspaceId, CancellationToken.None);

        Assert.Equal(2, tree.Items.Count);
        Assert.Equal(TreeLimitsDto.Current, tree.Limits);
        Assert.Equal(5000, tree.Limits.MaxItems);
    }
}
