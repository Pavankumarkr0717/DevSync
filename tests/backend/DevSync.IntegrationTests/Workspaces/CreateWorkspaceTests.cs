using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevSync.IntegrationTests.Workspaces;

/// <summary>US1 acceptance scenarios and input validation over HTTP.</summary>
public sealed class CreateWorkspaceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private DevSyncApiFactory _factory = null!;
    private Guid _user;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await DevSyncApiFactory.CreateAsync(postgres);
        _user = await _factory.CreateAccountAsync("Creator");
        _client = await _factory.ClientForAsync(_user);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task AS1_valid_request_creates_workspace_owned_by_caller()
    {
        var response = await _client.PostAsJsonAsync("/api/workspaces", new { name = "Interview Prep", description = "Mock interviews" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/workspaces/{id}", response.Headers.Location?.OriginalString);

        Assert.Equal("Interview Prep", body.GetProperty("name").GetString());
        Assert.Equal("Mock interviews", body.GetProperty("description").GetString());
        Assert.Equal(_user, body.GetProperty("owner").GetProperty("id").GetGuid());
        Assert.Equal("Creator", body.GetProperty("owner").GetProperty("displayName").GetString());
        Assert.Equal(body.GetProperty("createdAt").GetDateTimeOffset(), body.GetProperty("updatedAt").GetDateTimeOffset());

        var member = Assert.Single(body.GetProperty("members").EnumerateArray());
        Assert.Equal(_user, member.GetProperty("userId").GetGuid());
        Assert.Equal("owner", member.GetProperty("role").GetString());
        Assert.True(member.TryGetProperty("joinedAt", out _));
    }

    [Fact]
    public async Task AS2_name_and_description_are_trimmed_and_blank_description_is_null()
    {
        var body = await _client.CreateWorkspaceAsync("  Spaced Out  ", "   ");

        Assert.Equal("Spaced Out", body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("description").ValueKind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\u0000name")]
    public async Task AS3_blank_or_invalid_name_is_rejected_and_nothing_is_stored(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/workspaces", new { name });

        var problem = await response.AssertProblemAsync(400, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("name", out _));
        await AssertNoWorkspacesStoredAsync();
    }

    [Fact]
    public async Task AS4_too_long_name_is_rejected_with_a_name_error()
    {
        var response = await _client.PostAsJsonAsync("/api/workspaces", new { name = new string('n', 101) });

        var problem = await response.AssertProblemAsync(400, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("name", out _));
        await AssertNoWorkspacesStoredAsync();
    }

    [Fact]
    public async Task AS4_too_long_description_is_rejected_with_a_description_error()
    {
        var response = await _client.PostAsJsonAsync("/api/workspaces", new { name = "ok", description = new string('d', 501) });

        var problem = await response.AssertProblemAsync(400, "validation_failed");
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("description", out _));
        Assert.False(errors.TryGetProperty("name", out _));
        await AssertNoWorkspacesStoredAsync();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"name":null}""")]
    [InlineData("""{"description":"only"}""")]
    public async Task Missing_or_null_name_is_a_validation_error(string json)
    {
        var response = await PostRawAsync(json);

        await response.AssertProblemAsync(400, "validation_failed");
        await AssertNoWorkspacesStoredAsync();
    }

    [Theory]
    [InlineData("""{"name":123}""")]
    [InlineData("""{"name":"unterminated""")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task Malformed_body_is_rejected_as_malformed_request(string json)
    {
        var response = await PostRawAsync(json);

        await response.AssertProblemAsync(400, "malformed_request");
        await AssertNoWorkspacesStoredAsync();
    }

    [Fact]
    public async Task AS5_request_without_identity_is_rejected_and_nothing_is_stored()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/workspaces", new { name = "Anonymous" });

        await response.AssertProblemAsync(401, "unauthenticated");
        await AssertNoWorkspacesStoredAsync();
    }

    [Fact]
    public async Task Duplicate_names_are_allowed_and_get_distinct_ids()
    {
        var first = await _client.CreateWorkspaceAsync("Same Name");
        var second = await _client.CreateWorkspaceAsync("Same Name");

        Assert.NotEqual(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Client_supplied_identifiers_and_unknown_fields_are_ignored()
    {
        var suppliedId = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();

        var response = await PostRawAsync(
            $$"""{"id":"{{suppliedId}}","ownerId":"{{someoneElse}}","name":"Mine","extra":true}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.NotEqual(suppliedId, body.GetProperty("id").GetGuid());
        Assert.Equal(_user, body.GetProperty("owner").GetProperty("id").GetGuid());
        Assert.False(body.TryGetProperty("extra", out _));
    }

    private Task<HttpResponseMessage> PostRawAsync(string json) =>
        _client.PostAsync("/api/workspaces", new StringContent(json, Encoding.UTF8, "application/json"));

    private async Task AssertNoWorkspacesStoredAsync()
    {
        await using var scope = _factory.CreateDbScope();
        Assert.Equal(0, await scope.Db.Workspaces.CountAsync());
        Assert.Equal(0, await scope.Db.WorkspaceMemberships.CountAsync());
    }
}
