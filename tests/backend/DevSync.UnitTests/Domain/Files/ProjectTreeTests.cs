using DevSync.Domain.Files;

namespace DevSync.UnitTests.Domain.Files;

/// <summary>Spec 003 FR-001–FR-008, FR-012–FR-015: tree rules, validated before anything changes.</summary>
public sealed class ProjectTreeTests
{
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly ProjectTree _tree = new(WorkspaceId, []);

    // ---------- create ----------

    [Fact]
    public void Nested_folders_and_a_file_can_be_created()
    {
        var src = Folder(null, "src");
        var api = Folder(src.Id, "api");
        var file = File(api.Id, "server.ts", out var content);

        Assert.Equal(api.Id, file.ParentId);
        Assert.Equal(ItemKind.File, file.Kind);
        Assert.Equal(WorkspaceId, file.WorkspaceId);
        Assert.Equal("src/api/server.ts", _tree.PathOf(file.Id));
        Assert.NotNull(content);
        Assert.Equal(file.Id, content.ItemId);
        Assert.Equal(1, content.Revision);
        Assert.Equal(string.Empty, content.Content);
        Assert.Equal(0, content.SizeBytes);
        Assert.Equal(UserId, content.UpdatedByUserId);
        AssertInvariants();
    }

    [Fact]
    public void Creating_a_folder_returns_no_content()
    {
        Assert.Null(_tree.Create(null, ItemKind.Folder, "src", UserId, Now, out var item, out var content));
        Assert.NotNull(item);
        Assert.Null(content);
    }

    [Fact]
    public void Nesting_is_allowed_to_depth_20_and_refused_at_21()
    {
        var parent = Folder(null, "d0");
        for (var depth = 1; depth <= ProjectLimits.MaxDepth; depth++)
        {
            parent = Folder(parent.Id, $"d{depth}");
        }

        Assert.Equal(FileErrors.TooDeep, _tree.Create(parent.Id, ItemKind.File, "deep.ts", UserId, Now, out _, out _));
        AssertInvariants();
    }

    [Fact]
    public void Duplicate_names_in_the_same_folder_are_refused_ignoring_case()
    {
        var src = Folder(null, "src");
        File(src.Id, "Main.cs", out _);

        Assert.Equal(FileErrors.NameTaken, _tree.Create(src.Id, ItemKind.File, "main.CS", UserId, Now, out _, out _));
        Assert.Equal(FileErrors.NameTaken, _tree.Create(src.Id, ItemKind.Folder, "MAIN.cs", UserId, Now, out _, out _));
    }

    [Fact]
    public void The_same_name_is_allowed_in_different_folders_and_at_the_top_level()
    {
        var a = Folder(null, "a");
        var b = Folder(null, "b");
        File(a.Id, "index.ts", out _);
        File(b.Id, "index.ts", out _);
        File(null, "index.ts", out _);

        Assert.Equal(FileErrors.NameTaken, _tree.Create(null, ItemKind.Folder, "INDEX.TS", UserId, Now, out _, out _));
        AssertInvariants();
    }

    [Fact]
    public void Creating_under_a_file_or_a_missing_parent_is_refused()
    {
        var file = File(null, "a.ts", out _);

        Assert.Equal(FileErrors.ParentNotFound, _tree.Create(file.Id, ItemKind.File, "b.ts", UserId, Now, out _, out _));
        Assert.Equal(FileErrors.ParentNotFound, _tree.Create(Guid.CreateVersion7(), ItemKind.File, "b.ts", UserId, Now, out _, out _));
    }

    [Fact]
    public void Invalid_names_are_refused_with_the_rule_in_the_message()
    {
        var error = _tree.Create(null, ItemKind.Folder, "../etc", UserId, Now, out var item, out _);

        Assert.NotNull(error);
        Assert.Equal("invalid_name", error.Code);
        Assert.Null(item);
        Assert.Empty(_tree.Items);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("Dockerfile")]
    [InlineData("ci.yml")]
    public void Files_need_a_supported_extension(string name)
    {
        Assert.Equal(FileErrors.UnsupportedExtension, _tree.Create(null, ItemKind.File, name, UserId, Now, out _, out _));
    }

    [Fact]
    public void Folders_have_no_extension_rule()
    {
        Folder(null, "Dockerfile");
        Folder(null, "notes.txt");
        AssertInvariants();
    }

    [Fact]
    public void Paths_longer_than_1024_characters_are_refused()
    {
        // 4 folders of 255 + separators = 1023; a 1-char child makes 1025.
        var parent = Folder(null, new string('a', 255));
        parent = Folder(parent.Id, new string('b', 255));
        parent = Folder(parent.Id, new string('c', 255));
        parent = Folder(parent.Id, new string('d', 254));
        Assert.Equal(1022, _tree.PathOf(parent.Id).Length);

        Assert.Null(_tree.Create(parent.Id, ItemKind.Folder, "x", UserId, Now, out _, out _)); // 1024
        Assert.Equal(FileErrors.PathTooLong, _tree.Create(parent.Id, ItemKind.Folder, "xy", UserId, Now, out _, out _)); // 1025
    }

    [Fact]
    public void A_workspace_holds_at_most_5000_items()
    {
        var tree = new ProjectTree(WorkspaceId, []);
        for (var i = 0; i < ProjectLimits.MaxItemsPerWorkspace; i++)
        {
            Assert.Null(tree.Create(null, ItemKind.Folder, $"f{i}", UserId, Now, out _, out _));
        }

        Assert.Equal(FileErrors.TooManyItems, tree.Create(null, ItemKind.Folder, "one-more", UserId, Now, out _, out _));
        Assert.Equal(ProjectLimits.MaxItemsPerWorkspace, tree.Items.Count);
    }

    // ---------- rename ----------

    [Fact]
    public void Rename_keeps_identity_and_children()
    {
        var src = Folder(null, "src");
        var file = File(src.Id, "a.ts", out _);
        var later = Now.AddMinutes(5);

        Assert.Null(_tree.Rename(src.Id, "lib", later));

        Assert.Equal("lib", src.Name);
        Assert.Equal("LIB", src.NormalizedName);
        Assert.Equal(later, src.UpdatedAt);
        Assert.Equal("lib/a.ts", _tree.PathOf(file.Id));
        AssertInvariants();
    }

    [Fact]
    public void Rename_to_another_supported_extension_is_allowed_but_not_to_an_unsupported_one()
    {
        var file = File(null, "a.ts", out _);

        Assert.Null(_tree.Rename(file.Id, "a.py", Now));
        Assert.Equal(FileErrors.UnsupportedExtension, _tree.Rename(file.Id, "a.txt", Now));
        Assert.Equal("a.py", file.Name);
    }

    [Fact]
    public void Case_only_rename_of_an_item_is_allowed()
    {
        var file = File(null, "app.ts", out _);

        Assert.Null(_tree.Rename(file.Id, "App.ts", Now));
        Assert.Equal("App.ts", file.Name);
    }

    [Fact]
    public void Rename_onto_a_sibling_name_is_refused_ignoring_case()
    {
        File(null, "a.ts", out _);
        var b = File(null, "b.ts", out _);

        Assert.Equal(FileErrors.NameTaken, _tree.Rename(b.Id, "A.TS", Now));
        Assert.Equal("b.ts", b.Name);
    }

    [Fact]
    public void Rename_that_makes_a_descendant_path_too_long_is_refused()
    {
        var root = Folder(null, "r");
        var parent = Folder(root.Id, new string('b', 255));
        parent = Folder(parent.Id, new string('c', 255));
        parent = Folder(parent.Id, new string('d', 255));
        Folder(parent.Id, new string('e', 254)); // path = 1 + 1 + 255*3 + 3 + 254 = 1024

        Assert.Equal(FileErrors.PathTooLong, _tree.Rename(root.Id, "rr", Now));
        Assert.Equal("r", root.Name);
    }

    [Fact]
    public void Rename_rejects_invalid_names_and_missing_items()
    {
        var file = File(null, "a.ts", out _);

        Assert.Equal("invalid_name", _tree.Rename(file.Id, "../a.ts", Now)?.Code);
        Assert.Equal(FileErrors.ItemNotFound, _tree.Rename(Guid.CreateVersion7(), "b.ts", Now));
    }

    // ---------- move ----------

    [Fact]
    public void A_file_moves_to_another_folder_and_to_the_top_level()
    {
        var src = Folder(null, "src");
        var lib = Folder(null, "lib");
        var file = File(src.Id, "a.ts", out _);

        Assert.Null(_tree.Move(file.Id, lib.Id, Now));
        Assert.Equal("lib/a.ts", _tree.PathOf(file.Id));

        Assert.Null(_tree.Move(file.Id, null, Now));
        Assert.Null(file.ParentId);
        AssertInvariants();
    }

    [Fact]
    public void A_folder_moves_with_its_subtree()
    {
        var src = Folder(null, "src");
        var api = Folder(src.Id, "api");
        var v1 = Folder(api.Id, "v1");
        var file = File(v1.Id, "routes.ts", out _);
        var lib = Folder(null, "lib");

        Assert.Null(_tree.Move(api.Id, lib.Id, Now));

        Assert.Equal("lib/api/v1/routes.ts", _tree.PathOf(file.Id));
        AssertInvariants();
    }

    [Fact]
    public void Moving_into_itself_or_a_descendant_is_refused()
    {
        var src = Folder(null, "src");
        var api = Folder(src.Id, "api");
        var deep = Folder(api.Id, "deep");

        Assert.Equal(FileErrors.InvalidMove, _tree.Move(src.Id, src.Id, Now));
        Assert.Equal(FileErrors.InvalidMove, _tree.Move(src.Id, api.Id, Now));
        Assert.Equal(FileErrors.InvalidMove, _tree.Move(src.Id, deep.Id, Now));
        Assert.Null(src.ParentId);
        AssertInvariants();
    }

    [Fact]
    public void Moving_to_the_current_parent_is_a_successful_no_op()
    {
        var src = Folder(null, "src");
        var file = File(src.Id, "a.ts", out _);
        var before = file.UpdatedAt;

        Assert.Null(_tree.Move(file.Id, src.Id, Now.AddHours(1)));

        Assert.Equal(src.Id, file.ParentId);
        Assert.Equal(before, file.UpdatedAt);
    }

    [Fact]
    public void Moving_into_a_file_or_a_missing_folder_is_refused()
    {
        var target = File(null, "t.ts", out _);
        var file = File(null, "a.ts", out _);

        Assert.Equal(FileErrors.ParentNotFound, _tree.Move(file.Id, target.Id, Now));
        Assert.Equal(FileErrors.ParentNotFound, _tree.Move(file.Id, Guid.CreateVersion7(), Now));
        Assert.Equal(FileErrors.ItemNotFound, _tree.Move(Guid.CreateVersion7(), null, Now));
    }

    [Fact]
    public void Moving_onto_an_existing_name_in_the_target_is_refused()
    {
        var lib = Folder(null, "lib");
        File(lib.Id, "A.ts", out _);
        var file = File(null, "a.ts", out _);

        Assert.Equal(FileErrors.NameTaken, _tree.Move(file.Id, lib.Id, Now));
        Assert.Null(file.ParentId);
    }

    [Fact]
    public void A_move_that_pushes_the_subtree_past_depth_20_is_refused()
    {
        // A chain d0..d10 (height 10) moved under a folder at depth 10 → deepest item at depth 21.
        var chain = Folder(null, "d0");
        var top = chain;
        for (var depth = 1; depth <= 10; depth++)
        {
            chain = Folder(chain.Id, $"d{depth}");
        }

        var target = Folder(null, "t0");
        for (var depth = 1; depth <= 10; depth++)
        {
            target = Folder(target.Id, $"t{depth}");
        }

        Assert.Equal(FileErrors.TooDeep, _tree.Move(top.Id, target.Id, Now));

        var shallower = _tree.Items.Single(i => i.Name == "t9");
        Assert.Null(_tree.Move(top.Id, shallower.Id, Now)); // deepest item lands at exactly 20
        AssertInvariants();
    }

    [Fact]
    public void A_move_that_makes_a_descendant_path_too_long_is_refused()
    {
        var moving = Folder(null, "m");
        Folder(moving.Id, new string('x', 255));
        var target = Folder(null, new string('a', 255));
        target = Folder(target.Id, new string('b', 255));
        target = Folder(target.Id, new string('c', 255));

        Assert.Equal(FileErrors.PathTooLong, _tree.Move(moving.Id, target.Id, Now));
        Assert.Null(moving.ParentId);
    }

    // ---------- delete ----------

    [Fact]
    public void Deleting_a_folder_removes_its_whole_subtree()
    {
        var src = Folder(null, "src");
        var api = Folder(src.Id, "api");
        var file = File(api.Id, "server.ts", out _);
        var other = File(null, "keep.md", out _);

        Assert.Null(_tree.Delete(src.Id, out var removed));

        Assert.Equal(new[] { src.Id, api.Id, file.Id }.Order(), removed.Order());
        Assert.Equal(other.Id, Assert.Single(_tree.Items).Id);
        AssertInvariants();
    }

    [Fact]
    public void Deleting_a_missing_item_is_refused()
    {
        Assert.Equal(FileErrors.ItemNotFound, _tree.Delete(Guid.CreateVersion7(), out var removed));
        Assert.Empty(removed);
    }

    // ---------- loading existing items ----------

    [Fact]
    public void A_tree_can_be_rebuilt_from_existing_items()
    {
        var src = Folder(null, "src");
        var file = File(src.Id, "a.ts", out _);

        var reloaded = new ProjectTree(WorkspaceId, _tree.Items);

        Assert.Equal("src/a.ts", reloaded.PathOf(file.Id));
        Assert.Equal(FileErrors.NameTaken, reloaded.Create(src.Id, ItemKind.File, "A.TS", UserId, Now, out _, out _));
    }

    // ---------- helpers ----------

    private WorkspaceItem Folder(Guid? parentId, string name)
    {
        Assert.Null(_tree.Create(parentId, ItemKind.Folder, name, UserId, Now, out var item, out _));
        return item!;
    }

    private WorkspaceItem File(Guid? parentId, string name, out FileContent content)
    {
        Assert.Null(_tree.Create(parentId, ItemKind.File, name, UserId, Now, out var item, out var created));
        content = created!;
        return item!;
    }

    /// <summary>No cycles; every parent is a folder in the same workspace; siblings unique ignoring case; limits hold.</summary>
    private void AssertInvariants()
    {
        var byId = _tree.Items.ToDictionary(i => i.Id);
        foreach (var item in _tree.Items)
        {
            Assert.Equal(WorkspaceId, item.WorkspaceId);
            var seen = new HashSet<Guid> { item.Id };
            var depth = 0;
            for (var parentId = item.ParentId; parentId is not null; parentId = byId[parentId.Value].ParentId)
            {
                Assert.True(byId.ContainsKey(parentId.Value));
                Assert.Equal(ItemKind.Folder, byId[parentId.Value].Kind);
                Assert.True(seen.Add(parentId.Value), "cycle");
                depth++;
            }

            Assert.True(depth <= ProjectLimits.MaxDepth);
            Assert.True(_tree.PathOf(item.Id).Length <= ProjectLimits.MaxPathLength);
        }

        Assert.DoesNotContain(_tree.Items.GroupBy(i => (i.ParentId, i.NormalizedName)), g => g.Count() > 1);
        Assert.True(_tree.Items.Count <= ProjectLimits.MaxItemsPerWorkspace);
    }
}
