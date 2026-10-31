using System.Net.Http.Json;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Security;

/// <summary>Security-relevant actions are logged as structured events with ids, never emails or passwords (NFR-002).</summary>
public sealed class SecurityEventTests(PostgresFixture postgres)
{
    private const string Password = "Event-Password-Distinct-42";

    [Fact]
    public async Task Every_security_event_is_logged_once_with_correlation_and_actor()
    {
        var logs = new CapturingLoggerProvider();
        var clock = DevSyncApiFactory.NewClock();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, loggerProvider: logs, time: clock);

        var ana = AuthClient.Create(factory);
        await ana.RegisterAsync("ana@example.com", Password, "Ana");
        var anaId = (await (await ana.GetAsync("/api/me")).ReadJsonAsync()).GetProperty("id").GetGuid();
        await AuthClient.Create(factory).LoginAsync("ana@example.com", "wrong password value");
        await ana.LoginAsync("ana@example.com", Password);

        var api = ana.BearerClient(factory);
        var workspaceId = (await api.CreateWorkspaceAsync("Events")).GetProperty("id").GetGuid();
        var invitations = $"/api/workspaces/{workspaceId}/invitations";
        var benInvite = (await (await api.PostAsJsonAsync(invitations, new { email = "ben@example.com", role = "editor" })).ReadJsonAsync()).GetProperty("id").GetGuid();
        var cyInvite = (await (await api.PostAsJsonAsync(invitations, new { email = "cy@example.com", role = "viewer" })).ReadJsonAsync()).GetProperty("id").GetGuid();
        var dmInvite = (await (await api.PostAsJsonAsync(invitations, new { email = "dm@example.com", role = "viewer" })).ReadJsonAsync()).GetProperty("id").GetGuid();
        await api.DeleteAsync($"{invitations}/{dmInvite}");

        var ben = AuthClient.Create(factory);
        await ben.RegisterAsync("ben@example.com", Password, "Ben");
        var benId = (await (await ben.GetAsync("/api/me")).ReadJsonAsync()).GetProperty("id").GetGuid();
        await ben.BearerClient(factory).PostAsync($"/api/invitations/{benInvite}/accept", null);
        var cy = AuthClient.Create(factory);
        await cy.RegisterAsync("cy@example.com", Password, "Cy");
        await cy.BearerClient(factory).PostAsync($"/api/invitations/{cyInvite}/decline", null);

        await api.PatchAsJsonAsync($"/api/workspaces/{workspaceId}/members/{benId}", new { role = "viewer" });
        await api.DeleteAsync($"/api/workspaces/{workspaceId}/members/{benId}");

        var stolen = ben.RefreshToken!;
        await ben.RefreshAsync();
        clock.Advance(TimeSpan.FromSeconds(11));
        await AuthClient.Create(factory).RefreshAsync(stolen);

        await cy.LogoutAsync();
        await ana.LogoutAllAsync();

        var events = new Dictionary<string, int>
        {
            ["auth.register"] = 3,
            ["auth.login.failed"] = 1,
            ["auth.login.succeeded"] = 1,
            ["auth.refresh.reuse_detected"] = 1,
            ["auth.logout"] = 1,
            ["auth.logout_all"] = 1,
            ["membership.invited"] = 3,
            ["membership.invitation_revoked"] = 1,
            ["membership.joined"] = 1,
            ["membership.declined"] = 1,
            ["membership.role_changed"] = 1,
            ["membership.removed"] = 1,
        };

        foreach (var (name, count) in events)
        {
            var lines = logs.Messages.Where(m =>
                m.StartsWith($"Security event {name} ", StringComparison.Ordinal)
                || m.StartsWith($"Security event {name}:", StringComparison.Ordinal)).ToList();
            Assert.True(count == lines.Count, $"{name}: expected {count}, got {lines.Count}");
            Assert.All(lines, line =>
            {
                Assert.DoesNotContain("@example.com", line, StringComparison.Ordinal);
                Assert.DoesNotContain(Password, line, StringComparison.Ordinal);
            });

            // Events raised inside a request carry its correlation id.
            Assert.All(lines, line => Assert.Contains("correlationId=", line, StringComparison.Ordinal));
        }

        Assert.Contains(logs.Messages, m => m.StartsWith("Security event membership.removed", StringComparison.Ordinal)
            && m.Contains(workspaceId.ToString(), StringComparison.Ordinal)
            && m.Contains(benId.ToString(), StringComparison.Ordinal)
            && m.Contains(anaId.ToString(), StringComparison.Ordinal));
        Assert.Contains(logs.Messages, m => m.StartsWith("Security event auth.login.succeeded", StringComparison.Ordinal)
            && m.Contains(anaId.ToString(), StringComparison.Ordinal));
    }
}
