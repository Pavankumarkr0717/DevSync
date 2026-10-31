using System.Net;
using System.Text.Json;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevSync.IntegrationTests.Auth;

/// <summary>US1 registration: success, cookie, duplicates and validation (FR-001–FR-003, FR-006, FR-007).</summary>
public sealed class RegisterTests(PostgresFixture postgres) : IAsyncLifetime
{
    private DevSyncApiFactory _factory = null!;

    public async ValueTask InitializeAsync() => _factory = await DevSyncApiFactory.CreateAsync(postgres);

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Registration_creates_the_account_and_signs_in()
    {
        var client = AuthClient.Create(_factory);

        var response = await client.RegisterAsync("ana@example.com", displayName: " Ana ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.True(body.GetProperty("accessTokenExpiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
        var user = body.GetProperty("user");
        Assert.Equal("Ana", user.GetProperty("displayName").GetString());
        Assert.Equal("ana@example.com", user.GetProperty("email").GetString());
        Assert.Equal(["accessToken", "accessTokenExpiresAt", "user"], body.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "displayName", "email", "createdAt"], user.EnumerateObject().Select(p => p.Name));

        // The new access token works immediately.
        Assert.True((await client.BearerClient(_factory).GetAsync("/api/workspaces")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Registration_sets_a_secure_http_only_strict_refresh_cookie_scoped_to_auth()
    {
        var response = await AuthClient.Create(_factory).RegisterAsync("cookie@example.com");

        var cookie = AuthClient.SetCookie(response)!.Value;
        var attributes = cookie.Raw.ToLowerInvariant();
        Assert.True(cookie.Value.Length >= 40);
        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=strict", attributes);
        Assert.Contains("path=/api/auth", attributes);
        Assert.Contains("max-age=", attributes);
    }

    [Theory]
    [InlineData("ana@example.com")]
    [InlineData(" ANA@Example.com ")]
    public async Task Duplicate_registration_is_rejected_with_email_taken(string secondEmail)
    {
        await AuthClient.Create(_factory).RegisterAsync("ana@example.com");

        var response = await AuthClient.Create(_factory).RegisterAsync(secondEmail, "another password!!");

        await response.AssertProblemAsync(409, "email_taken");
        Assert.Null(AuthClient.SetCookie(response));
        await using var scope = _factory.CreateDbScope();
        Assert.Equal(1, await scope.Db.Users.CountAsync());
    }

    [Fact]
    public async Task Concurrent_registrations_of_one_email_create_exactly_one_account()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => AuthClient.Create(_factory).RegisterAsync("race@example.com")));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
        await using var scope = _factory.CreateDbScope();
        Assert.Equal(1, await scope.Db.Users.CountAsync());
    }

    [Theory]
    [InlineData("", "valid@example.com", "correct horse battery", "displayName")]
    [InlineData("101", "valid@example.com", "correct horse battery", "displayName")]
    [InlineData("Ana", "not-an-email", "correct horse battery", "email")]
    [InlineData("Ana", "255", "correct horse battery", "email")]
    [InlineData("Ana", "valid@example.com", "elevenchars", "password")]
    [InlineData("Ana", "valid@example.com", "129", "password")]
    [InlineData("Ana", "valid@example.com", "valid@example.com", "password")]
    public async Task Invalid_registrations_are_rejected_field_by_field(string name, string email, string password, string field)
    {
        name = name == "101" ? new string('n', 101) : name;
        email = email == "255" ? new string('a', 243) + "@example.com" : email;
        password = password == "129" ? new string('p', 129) : password;

        var response = await AuthClient.Create(_factory).RegisterAsync(email, password, name);

        var problem = await response.AssertProblemAsync(400, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
        await using var scope = _factory.CreateDbScope();
        Assert.Equal(0, await scope.Db.Users.CountAsync());
    }

    [Fact]
    public async Task Registration_without_the_client_header_is_refused()
    {
        var client = AuthClient.Create(_factory);

        var response = await client.SendAsync(HttpMethod.Post, "/api/auth/register",
            new { displayName = "Ana", email = "csrf@example.com", password = TestData.DefaultPassword }, clientHeader: false);

        await response.AssertProblemAsync(403, "csrf_check_failed");
    }

    [Fact]
    public async Task Response_never_contains_password_or_hash()
    {
        var client = AuthClient.Create(_factory);

        await client.RegisterAsync("nohash@example.com", "unique-password-xyz-123");

        var body = client.ResponseBodies.Single();
        Assert.DoesNotContain("unique-password-xyz-123", body);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AQAAAA", body); // Identity v3 hash prefix
        _ = JsonDocument.Parse(body);
    }
}
