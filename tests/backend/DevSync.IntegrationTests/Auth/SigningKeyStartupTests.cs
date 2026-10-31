using System.Net;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Auth;

/// <summary>Signing keys come from configuration and are mandatory outside Development (NFR-004).</summary>
public sealed class SigningKeyStartupTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Production_without_a_signing_key_refuses_to_start()
    {
        await using var factory = new DevSyncApiFactory(
            await postgres.CreateDatabaseAsync(),
            environment: "Production",
            settings: new Dictionary<string, string?> { ["DevSync:Auth:SigningKey"] = "" });

        var error = Assert.ThrowsAny<Exception>(() => factory.Server);

        Assert.Contains("DevSync__Auth__SigningKey", Flatten(error));
    }

    [Fact]
    public async Task Production_with_a_short_signing_key_refuses_to_start()
    {
        await using var factory = new DevSyncApiFactory(
            await postgres.CreateDatabaseAsync(),
            environment: "Production",
            settings: new Dictionary<string, string?> { ["DevSync:Auth:SigningKey"] = Convert.ToBase64String(new byte[16]) });

        var error = Assert.ThrowsAny<Exception>(() => factory.Server);

        Assert.Contains("at least 32 bytes", Flatten(error));
    }

    [Fact]
    public async Task Development_without_a_signing_key_starts_with_a_temporary_key_and_warns()
    {
        var logs = new CapturingLoggerProvider();
        await using var factory = new DevSyncApiFactory(
            await postgres.CreateDatabaseAsync(),
            settings: new Dictionary<string, string?> { ["DevSync:Auth:SigningKey"] = "" },
            loggerProvider: logs);

        var response = await factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(logs.Messages, m => m.Contains("temporary signing key", StringComparison.Ordinal));
    }

    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }
}
