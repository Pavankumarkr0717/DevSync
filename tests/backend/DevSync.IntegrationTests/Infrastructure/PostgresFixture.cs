using DevSync.IntegrationTests.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace DevSync.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL 18 container for the whole test run. Each test host gets its own fresh
/// database, so tests never see each other's data.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Many test hosts run in parallel, each with its own connection pool: allow plenty of connections.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithCommand("-c", "max_connections=500")
        .Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Creates an empty database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"devsync_test_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{name}\"";
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = name,
            MaxPoolSize = 10,
        }.ConnectionString;
    }

    /// <summary>A connection string to a port where nothing listens, to simulate a database outage.</summary>
    public static string UnreachableConnectionString =>
        "Host=127.0.0.1;Port=1;Database=unreachable;Username=devsync;Password=unused;Timeout=3";
}
