using System.Net;
using System.Net.Http.Json;
using DevSync.Domain.Workspaces;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Members;

/// <summary>US4: the owner changes roles and removes members; changes apply on the next request (FR-022–FR-025, SC-007).</summary>
public sealed class MemberManagementTests(PostgresFixture postgres) : IAsyncLifetime
{
    private DevSyncApiFactory _factory = null!;
    private Guid _owner;
    private Guid _member;
    private HttpClient _ownerClient = null!;
    private HttpClient _memberClient = null!;
    private Guid _workspaceId;

    public async ValueTask InitializeAsync()
    {
        _factory = await DevSyncApiFactory.CreateAsync(postgres);
        _owner = await _factory.CreateAccountAsync("Owner");
        _member = await _factory.CreateAccountAsync("Member", "member@example.com");
        _ownerClient = await _factory.ClientForAsync(_owner);
        _memberClient = await _factory.ClientForAsync(_member);
        _workspaceId = (await _ownerClient.CreateWorkspaceAsync("Team")).GetProperty("id").GetGuid();
        await _factory.AddMemberAsync(_workspaceId, _member, WorkspaceRole.Editor);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private string MemberPath(Guid userId) => $"/api/workspaces/{_workspaceId}/members/{userId}";

    [Fact]
    public async Task Owner_downgrades_an_editor_and_the_next_edit_is_forbidden()
    {
        Assert.Equal(HttpStatusCode.OK, (await _memberClient.PutAsJsonAsync($"/api/workspaces/{_workspaceId}", new { name = "Edited" })).StatusCode);

        var response = await _ownerClient.PatchAsJsonAsync(MemberPath(_member), new { role = "viewer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("viewer", body.GetProperty("role").GetString());
        Assert.Equal("member@example.com", body.GetProperty("email").GetString());
        await (await _memberClient.PutAsJsonAsync($"/api/workspaces/{_workspaceId}", new { name = "Again" }))
            .AssertProblemAsync(403, "forbidden");
    }

    [Fact]
    public async Task Owner_upgrades_a_viewer_and_the_next_edit_is_allowed()
    {
        await _ownerClient.PatchAsJsonAsync(MemberPath(_member), new { role = "viewer" });

        await _ownerClient.PatchAsJsonAsync(MemberPath(_member), new { role = "Editor" });

        Assert.Equal(HttpStatusCode.OK, (await _memberClient.PutAsJsonAsync($"/api/workspaces/{_workspaceId}", new { name = "Edited" })).StatusCode);
    }

    [Fact]
    public async Task Owner_removes_a_member_who_then_loses_access_immediately()
    {
        var response = await _ownerClient.DeleteAsync(MemberPath(_member));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await (await _memberClient.GetAsync($"/api/workspaces/{_workspaceId}")).AssertProblemAsync(404, "not_found");
        var list = await (await _memberClient.GetAsync("/api/workspaces")).ReadJsonAsync();
        Assert.Empty(list.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task The_owner_cannot_change_or_remove_themselves()
    {
        await (await _ownerClient.PatchAsJsonAsync(MemberPath(_owner), new { role = "viewer" })).AssertProblemAsync(400, "cannot_change_owner");
        await (await _ownerClient.DeleteAsync(MemberPath(_owner))).AssertProblemAsync(400, "cannot_remove_owner");

        var details = await (await _ownerClient.GetAsync($"/api/workspaces/{_workspaceId}")).ReadJsonAsync();
        Assert.Equal("owner", details.GetProperty("myRole").GetString());
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("")]
    public async Task Only_editor_or_viewer_can_be_assigned(string role)
    {
        var response = await _ownerClient.PatchAsJsonAsync(MemberPath(_member), new { role });

        var problem = await response.AssertProblemAsync(400, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("role", out _));
    }

    [Fact]
    public async Task Unknown_or_malformed_member_ids_are_member_not_found()
    {
        var stranger = await _factory.CreateAccountAsync("Stranger");

        await (await _ownerClient.PatchAsJsonAsync(MemberPath(stranger), new { role = "viewer" })).AssertProblemAsync(404, "member_not_found");
        await (await _ownerClient.DeleteAsync(MemberPath(Guid.CreateVersion7()))).AssertProblemAsync(404, "member_not_found");
        await (await _ownerClient.DeleteAsync($"/api/workspaces/{_workspaceId}/members/not-a-guid")).AssertProblemAsync(404, "member_not_found");
    }
}
