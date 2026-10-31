using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevSync.Application.Auth;
using DevSync.Domain.Identity;
using DevSync.Domain.Workspaces;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevSync.IntegrationTests.Infrastructure;

public static class TestData
{
    public const string DefaultPassword = "correct horse battery";

    /// <summary>Creates an account directly through Identity and returns its id.</summary>
    public static async Task<Guid> CreateAccountAsync(
        this DevSyncApiFactory factory, string displayName = "Test User", string? email = null, string password = DefaultPassword)
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";
        await using var scope = factory.CreateDbScope();
        var users = scope.Services.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var result = await users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return user.Id;
    }

    public static async Task<string> EmailOfAsync(this DevSyncApiFactory factory, Guid userId)
    {
        await using var scope = factory.CreateDbScope();
        return (await scope.Db.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).Email!;
    }

    /// <summary>Starts a real session for <paramref name="userId"/> without going through the login endpoint.</summary>
    public static async Task<(string AccessToken, Guid SessionId)> StartSessionAsync(this DevSyncApiFactory factory, Guid userId)
    {
        await using var scope = factory.CreateDbScope();
        var sessions = scope.Services.GetRequiredService<ISessionRepository>();
        var settings = scope.Services.GetRequiredService<SessionSettings>();
        var now = scope.Services.GetRequiredService<TimeProvider>().GetUtcNow();

        var session = Session.Start(userId, now, settings.IdleLifetime, settings.AbsoluteLifetime);
        sessions.Add(session, RefreshToken.Issue(session.Id, RefreshTokens.Hash(RefreshTokens.Generate()), now));
        await sessions.SaveChangesAsync(CancellationToken.None);

        var token = scope.Services.GetRequiredService<IAccessTokenIssuer>().Issue(userId, session.Id);
        return (token.Token, session.Id);
    }

    /// <summary>A client signed in as <paramref name="userId"/> with a bearer access token.</summary>
    public static async Task<HttpClient> ClientForAsync(this DevSyncApiFactory factory, Guid userId) =>
        factory.ClientWithToken((await factory.StartSessionAsync(userId)).AccessToken);

    public static HttpClient ClientWithToken(this DevSyncApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>A validly signed token for a session that need not exist (used when the database is unreachable).</summary>
    public static HttpClient ClientWithUnverifiableSession(this DevSyncApiFactory factory)
    {
        var issuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();
        return factory.ClientWithToken(issuer.Issue(Guid.CreateVersion7(), Guid.CreateVersion7()).Token);
    }

    /// <summary>Gives <paramref name="userId"/> a role in a workspace directly (bypassing invitations).</summary>
    public static async Task AddMemberAsync(this DevSyncApiFactory factory, Guid workspaceId, Guid userId, WorkspaceRole role)
    {
        await using var scope = factory.CreateDbScope();
        await scope.Db.Database.ExecuteSqlAsync($"""
            INSERT INTO workspace_memberships (workspace_id, user_id, role, joined_at)
            VALUES ({workspaceId}, {userId}, {RoleNames.ToDb(role)}, now())
            """);
    }

    /// <summary>Creates a workspace through the API and returns its JSON body.</summary>
    public static async Task<JsonElement> CreateWorkspaceAsync(this HttpClient client, string name, string? description = null)
    {
        var response = await client.PostAsJsonAsync("/api/workspaces", new { name, description });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
