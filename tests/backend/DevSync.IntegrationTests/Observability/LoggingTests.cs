using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Observability;

/// <summary>Request logs carry correlation and user ids but never request content or credentials (Principle VIII, NFR-001).</summary>
public sealed class LoggingTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Request_logs_include_correlation_and_user_but_never_request_content_or_tokens()
    {
        var logs = new CapturingLoggerProvider();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, loggerProvider: logs);
        var user = await factory.CreateAccountAsync();
        var client = await factory.ClientForAsync(user);
        var accessToken = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "logging-test-1");

        await client.CreateWorkspaceAsync("SecretProjectName", "Confidential description text");
        await client.GetAsync("/api/workspaces");
        await client.GetAsync($"/api/workspaces/{Guid.CreateVersion7()}");

        var requestLines = logs.Messages.Where(m => m.StartsWith("HTTP ", StringComparison.Ordinal)).ToList();
        var mine = requestLines.Where(m => m.Contains("logging-test-1", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, mine.Count);
        Assert.All(mine, m => Assert.Contains(user.ToString(), m));
        Assert.Contains(requestLines, m => m.Contains("/api/workspaces/{workspaceId}", StringComparison.Ordinal) && m.Contains("404"));

        Assert.DoesNotContain(logs.Messages, m => m.Contains("SecretProjectName", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, m => m.Contains("Confidential description", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, m => m.Contains(accessToken, StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, m => m.Contains("Bearer ", StringComparison.Ordinal));
    }
}
