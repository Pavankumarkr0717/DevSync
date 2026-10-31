using DevSync.Application.Common;
using DevSync.Application.Workspaces;

namespace DevSync.UnitTests.Application;

public sealed class ListMyWorkspacesHandlerTests
{
    private static readonly Guid User = Guid.CreateVersion7();

    [Fact]
    public void Page_request_defaults_to_first_page_of_20()
    {
        var page = PageRequest.Create(null, null).Value;

        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(0, page.Offset);
    }

    [Theory]
    [InlineData(0, 20, "page")]
    [InlineData(-1, 20, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, 101, "pageSize")]
    public void Out_of_range_paging_is_rejected(int page, int pageSize, string field)
    {
        var result = PageRequest.Create(page, pageSize);

        Assert.False(result.IsSuccess);
        Assert.Equal(field, Assert.Single(result.Errors).Field);
    }

    [Fact]
    public void Max_page_size_is_allowed()
    {
        Assert.True(PageRequest.Create(3, 100).IsSuccess);
        Assert.Equal(200, PageRequest.Create(3, 100).Value.Offset);
    }

    [Fact]
    public async Task Handler_passes_the_current_user_and_paging_to_the_repository()
    {
        var repository = new FakeWorkspaceRepository();
        var handler = new ListMyWorkspacesHandler(repository, new FakeCurrentUser(User));

        var result = await handler.HandleAsync(2, 5, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(User, repository.LastListCall!.Value.UserId);
        Assert.Equal(2, repository.LastListCall.Value.Page.Page);
        Assert.Equal(5, repository.LastListCall.Value.Page.PageSize);
    }

    [Fact]
    public async Task Handler_does_not_query_when_paging_is_invalid()
    {
        var repository = new FakeWorkspaceRepository();
        var handler = new ListMyWorkspacesHandler(repository, new FakeCurrentUser(User));

        var result = await handler.HandleAsync(0, 500, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Errors.Count);
        Assert.Null(repository.LastListCall);
    }
}
