using DevSync.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DevSync.IntegrationTests.Files;

/// <summary>
/// Spec 003 research R6: invalid names and path-traversal attempts. Items are addressed only by id within
/// the route's workspace, so no request can reach another workspace's data (FR-005, FR-027).
/// </summary>
public sealed class PathSecurityTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../etc")]
    [InlineData("..\\etc")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("/etc/passwd")]
    [InlineData("con:")]
    [InlineData("x?.ts")]
    [InlineData(" x.ts")]
    [InlineData("x.ts ")]
    [InlineData("x.ts.")]
    [InlineData("a\u0001b")]
    public async Task Invalid_and_traversal_names_are_rejected(string name)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();

        var body = await (await team.Owner.CreateItemAsync(team.WorkspaceId, null, "folder", name)).AssertProblemAsync(400, "invalid_name");

        Assert.False(string.IsNullOrEmpty(body.GetProperty("detail").GetString()));
        Assert.Empty(await team.Owner.GetItemsAsync(team.WorkspaceId));
    }

    [Theory]
    [InlineData("..%2F..")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Malformed_or_encoded_item_ids_are_not_found(string itemId)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        await team.Owner.NewFileAsync(team.WorkspaceId, null, "keep.ts");

        await (await team.Owner.GetAsync($"{team.Base}/files/{itemId}/content")).AssertProblemAsync(404, "item_not_found");
        await (await team.Owner.DeleteAsync($"{team.Base}/items/{itemId}")).AssertProblemAsync(404, "item_not_found");
        Assert.Single(await team.Owner.GetItemsAsync(team.WorkspaceId));
    }

    [Fact]
    public async Task Dot_segments_in_an_item_id_never_reach_another_route()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        await team.Owner.NewFileAsync(team.WorkspaceId, null, "keep.ts");

        // %2e%2e is "..": the client resolves it before sending, so the request matches no item route at all
        // (404, or 405 where the resolved path is the workspace itself, which has no DELETE).
        foreach (var response in new[]
                 {
                     await team.Owner.GetAsync($"{team.Base}/files/%2e%2e/content"),
                     await team.Owner.DeleteAsync($"{team.Base}/items/%2e%2e"),
                     await team.Owner.DeleteAsync($"{team.Base}/items/%2e%2e/%2e%2e"),
                 })
        {
            Assert.Contains(response.StatusCode, new[] { System.Net.HttpStatusCode.NotFound, System.Net.HttpStatusCode.MethodNotAllowed });
        }

        Assert.Single(await team.Owner.GetItemsAsync(team.WorkspaceId));
        Assert.Equal(System.Net.HttpStatusCode.OK, (await team.Owner.GetAsync(team.Base)).StatusCode); // the workspace is untouched
    }

    [Fact]
    public async Task An_item_of_another_workspace_is_not_found_even_for_a_member_of_both()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var team = await factory.TeamWorkspaceAsync();
        var other = (await team.Owner.CreateWorkspaceAsync("Other")).GetProperty("id").GetGuid();
        var secret = await team.Owner.NewFileAsync(other, null, "secret.ts");
        await team.Owner.SaveContentAsync(other, secret, 1, "the other workspace's code");

        var read = await team.Owner.GetContentResponseAsync(team.WorkspaceId, secret);
        var body = await read.AssertProblemAsync(404, "item_not_found");
        Assert.DoesNotContain("other workspace", body.GetRawText(), StringComparison.Ordinal);

        await (await team.Owner.SaveContentAsync(team.WorkspaceId, secret, 2, "overwrite")).AssertProblemAsync(404, "item_not_found");
        await (await team.Owner.RenameAsync(team.WorkspaceId, secret, "x.ts")).AssertProblemAsync(404, "item_not_found");
        await (await team.Owner.MoveAsync(team.WorkspaceId, secret, null)).AssertProblemAsync(404, "item_not_found");
        await (await team.Owner.DeleteItemAsync(team.WorkspaceId, secret)).AssertProblemAsync(404, "item_not_found");

        var (text, revision) = await team.Owner.GetContentAsync(other, secret);
        Assert.Equal("the other workspace's code", text);
        Assert.Equal(2, revision);
    }

    [Fact]
    public async Task No_file_route_takes_a_path_parameter()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        _ = factory.Server;

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/workspaces/{workspaceId}/", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(routes);
        Assert.All(routes, route => Assert.All(
            route.RoutePattern.Parameters,
            p => Assert.Contains(p.Name, new[] { "workspaceId", "itemId", "userId", "invitationId", "diagramId" })));
    }
}
