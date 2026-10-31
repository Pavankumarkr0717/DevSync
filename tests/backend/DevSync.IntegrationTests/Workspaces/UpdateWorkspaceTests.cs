using System.Net;
using System.Net.Http.Json;
using DevSync.Domain.Workspaces;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Workspaces;

/// <summary>US5: Owner and Editor edit name/description with the 001 validation rules.</summary>
public sealed class UpdateWorkspaceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private DevSyncApiFactory _factory = null!;
    private HttpClient _owner = null!;
    private HttpClient _editor = null!;
    private Guid _workspaceId;

    public async ValueTask InitializeAsync()
    {
        _factory = await DevSyncApiFactory.CreateAsync(postgres);
        _owner = await _factory.ClientForAsync(await _factory.CreateAccountAsync("Owner"));
        var editor = await _factory.CreateAccountAsync("Editor");
        _editor = await _factory.ClientForAsync(editor);
        _workspaceId = (await _owner.CreateWorkspaceAsync("Before", "Old notes")).GetProperty("id").GetGuid();
        await _factory.AddMemberAsync(_workspaceId, editor, WorkspaceRole.Editor);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task An_editor_saves_trimmed_values_and_updated_at_changes()
    {
        var before = await (await _owner.GetAsync($"/api/workspaces/{_workspaceId}")).ReadJsonAsync();
        await Task.Delay(10);

        var response = await _editor.PutAsJsonAsync($"/api/workspaces/{_workspaceId}", new { name = "  After  ", description = "   " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("After", body.GetProperty("name").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("description").ValueKind);
        Assert.Equal("editor", body.GetProperty("myRole").GetString());
        Assert.True(body.GetProperty("updatedAt").GetDateTimeOffset() > before.GetProperty("updatedAt").GetDateTimeOffset());
        Assert.Equal(before.GetProperty("createdAt").GetDateTimeOffset(), body.GetProperty("createdAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Invalid_values_are_rejected_and_nothing_changes()
    {
        var response = await _owner.PutAsJsonAsync($"/api/workspaces/{_workspaceId}", new { name = "  ", description = new string('d', 501) });

        var problem = await response.AssertProblemAsync(400, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("name", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("description", out _));
        var after = await (await _owner.GetAsync($"/api/workspaces/{_workspaceId}")).ReadJsonAsync();
        Assert.Equal("Before", after.GetProperty("name").GetString());
        Assert.Equal("Old notes", after.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Malformed_body_is_a_malformed_request()
    {
        var response = await _owner.PutAsync($"/api/workspaces/{_workspaceId}",
            new StringContent("{not json", System.Text.Encoding.UTF8, "application/json"));

        await response.AssertProblemAsync(400, "malformed_request");
    }
}
