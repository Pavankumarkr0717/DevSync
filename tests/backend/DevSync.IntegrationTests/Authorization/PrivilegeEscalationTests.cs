using System.Net;
using System.Net.Http.Json;
using DevSync.Domain.Workspaces;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DevSync.IntegrationTests.Authorization;

/// <summary>Attempts to gain rights one does not have (FR-024, FR-025, FR-027, SC-007).</summary>
public sealed class PrivilegeEscalationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private DevSyncApiFactory _factory = null!;
    private Guid _owner;
    private Guid _editor;
    private Guid _viewer;
    private Guid _workspaceId;

    public async ValueTask InitializeAsync()
    {
        _factory = await DevSyncApiFactory.CreateAsync(postgres);
        _owner = await _factory.CreateAccountAsync("Owner");
        _editor = await _factory.CreateAccountAsync("Editor");
        _viewer = await _factory.CreateAccountAsync("Viewer");
        _workspaceId = (await (await _factory.ClientForAsync(_owner)).CreateWorkspaceAsync("Target")).GetProperty("id").GetGuid();
        await _factory.AddMemberAsync(_workspaceId, _editor, WorkspaceRole.Editor);
        await _factory.AddMemberAsync(_workspaceId, _viewer, WorkspaceRole.Viewer);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private string Path(string suffix = "") => $"/api/workspaces/{_workspaceId}{suffix}";

    [Fact]
    public async Task A_viewer_cannot_edit_or_promote_themselves()
    {
        var viewer = await _factory.ClientForAsync(_viewer);

        await (await viewer.PutAsJsonAsync(Path(), new { name = "Defaced" })).AssertProblemAsync(403, "forbidden");
        await (await viewer.PatchAsJsonAsync(Path($"/members/{_viewer}"), new { role = "editor" })).AssertProblemAsync(403, "forbidden");

        Assert.Equal("Target", await NameAsync());
        Assert.Equal(WorkspaceRole.Viewer, await RoleAsync(_viewer));
    }

    [Fact]
    public async Task An_editor_cannot_manage_membership_or_take_ownership()
    {
        var editor = await _factory.ClientForAsync(_editor);

        await (await editor.PatchAsJsonAsync(Path($"/members/{_editor}"), new { role = "owner" })).AssertProblemAsync(403, "forbidden");
        await (await editor.PatchAsJsonAsync(Path($"/members/{_viewer}"), new { role = "editor" })).AssertProblemAsync(403, "forbidden");
        await (await editor.DeleteAsync(Path($"/members/{_owner}"))).AssertProblemAsync(403, "forbidden");
        await (await editor.PostAsJsonAsync(Path("/invitations"), new { email = "friend@example.com", role = "editor" }))
            .AssertProblemAsync(403, "forbidden");
        await (await editor.GetAsync(Path("/invitations"))).AssertProblemAsync(403, "forbidden");

        Assert.Equal(WorkspaceRole.Owner, await RoleAsync(_owner));
        Assert.Equal(WorkspaceRole.Editor, await RoleAsync(_editor));
        Assert.Equal(WorkspaceRole.Viewer, await RoleAsync(_viewer));
    }

    [Fact]
    public async Task Nobody_can_be_made_owner_even_by_the_owner()
    {
        var owner = await _factory.ClientForAsync(_owner);

        await (await owner.PatchAsJsonAsync(Path($"/members/{_editor}"), new { role = "owner" })).AssertProblemAsync(400, "validation_failed");
        await (await owner.PostAsJsonAsync(Path("/invitations"), new { email = "co-owner@example.com", role = "owner" }))
            .AssertProblemAsync(400, "validation_failed");
    }

    [Fact]
    public async Task Extra_role_claims_in_a_token_grant_nothing()
    {
        var (_, sessionId) = await _factory.StartSessionAsync(_viewer);
        var forged = Token(_viewer, sessionId, new Dictionary<string, object>
        {
            ["role"] = "owner",
            ["workspace_role"] = "owner",
            ["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] = "Admin",
        });

        await (await _factory.ClientWithToken(forged).PutAsJsonAsync(Path(), new { name = "Defaced" })).AssertProblemAsync(403, "forbidden");
        Assert.Equal("Target", await NameAsync());
    }

    [Fact]
    public async Task A_token_pairing_one_users_id_with_another_users_session_is_rejected()
    {
        var (_, viewerSession) = await _factory.StartSessionAsync(_viewer);
        var forged = Token(_owner, viewerSession, []);

        await (await _factory.ClientWithToken(forged).GetAsync(Path())).AssertProblemAsync(401, "session_ended");
    }

    [Fact]
    public async Task A_downgrade_takes_effect_on_the_very_next_request()
    {
        var editor = await _factory.ClientForAsync(_editor);
        Assert.Equal(HttpStatusCode.OK, (await editor.PutAsJsonAsync(Path(), new { name = "First edit" })).StatusCode);

        await (await _factory.ClientForAsync(_owner)).PatchAsJsonAsync(Path($"/members/{_editor}"), new { role = "viewer" });

        await (await editor.PutAsJsonAsync(Path(), new { name = "Second edit" })).AssertProblemAsync(403, "forbidden");
        Assert.Equal("First edit", await NameAsync());
    }

    [Fact]
    public async Task A_removed_member_with_a_stale_page_gets_not_found()
    {
        var viewer = await _factory.ClientForAsync(_viewer);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Path())).StatusCode);

        await (await _factory.ClientForAsync(_owner)).DeleteAsync(Path($"/members/{_viewer}"));

        await (await viewer.GetAsync(Path())).AssertProblemAsync(404, "not_found");
        await (await viewer.PutAsJsonAsync(Path(), new { name = "Late edit" })).AssertProblemAsync(404, "not_found");
    }

    private static string Token(Guid userId, Guid sessionId, Dictionary<string, object> extraClaims)
    {
        var claims = new Dictionary<string, object>(extraClaims)
        {
            ["sub"] = userId.ToString(),
            ["sid"] = sessionId.ToString(),
        };
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "devsync",
            Audience = "devsync",
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(15),
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(DevSyncApiFactory.TestSigningKey)), SecurityAlgorithms.HmacSha256),
        });
    }

    private async Task<string> NameAsync()
    {
        await using var scope = _factory.CreateDbScope();
        return await scope.Db.Workspaces.AsNoTracking().Where(w => w.Id == _workspaceId).Select(w => w.Name).SingleAsync();
    }

    private async Task<WorkspaceRole> RoleAsync(Guid userId)
    {
        await using var scope = _factory.CreateDbScope();
        return await scope.Db.WorkspaceMemberships.AsNoTracking()
            .Where(m => m.WorkspaceId == _workspaceId && m.UserId == userId).Select(m => m.Role).SingleAsync();
    }
}
