using DevSync.IntegrationTests.Infrastructure;
using Npgsql;

namespace DevSync.IntegrationTests.Errors;

/// <summary>
/// Regression: when PostgreSQL shuts down, pooled connections fail with 57P01
/// ("terminating connection due to administrator command"). This must be reported as 503, not 500.
/// Found during quickstart V10 (docker compose stop db) on 2026-10-29.
/// </summary>
public sealed class DatabaseOutageTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Connections_terminated_by_the_server_are_reported_as_service_unavailable()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = await factory.ClientForAsync(await factory.CreateAccountAsync());
        Assert.True((await client.GetAsync("/api/workspaces")).IsSuccessStatusCode); // warm the connection pool

        await TerminateOtherConnectionsAsync(factory.ConnectionString);
        var response = await client.GetAsync("/api/workspaces");

        await response.AssertProblemAsync(503, "service_unavailable");
    }

    private static async Task TerminateOtherConnectionsAsync(string connectionString)
    {
        // Pooling off so this admin connection is not itself reused by anything.
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT pg_terminate_backend(pid) FROM pg_stat_activity
            WHERE datname = current_database() AND pid <> pg_backend_pid()
            """;
        await command.ExecuteNonQueryAsync();
    }
}
