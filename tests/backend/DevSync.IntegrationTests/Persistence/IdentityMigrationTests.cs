using DevSync.Infrastructure.Persistence;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace DevSync.IntegrationTests.Persistence;

/// <summary>Upgrading a 001 database removes all 001 development data (spec 002 FR-030) and adds the new constraints.</summary>
public sealed class IdentityMigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Upgrading_from_001_deletes_development_data_logs_counts_and_enforces_one_owner()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAs001Async(connectionString);

        var logs = new CapturingLoggerProvider();
        await using var factory = new DevSyncApiFactory(connectionString, loggerProvider: logs);
        _ = factory.Server; // runs MigrationRunner

        Assert.Contains(logs.Messages, m => m.Contains("Removing 3 development users and 2 workspaces from 001", StringComparison.Ordinal));

        await using var scope = factory.CreateDbScope();
        Assert.Equal(0, await scope.Db.Users.CountAsync());
        Assert.Equal(0, await scope.Db.Workspaces.CountAsync());
        Assert.Equal(0, await scope.Db.WorkspaceMemberships.CountAsync());
        Assert.Empty(await scope.Db.Database.GetPendingMigrationsAsync());

        // A second owner for one workspace is rejected by ux_workspace_memberships_one_owner.
        var owner = await factory.CreateAccountAsync();
        var other = await factory.CreateAccountAsync();
        var workspaceId = (await (await factory.ClientForAsync(owner)).CreateWorkspaceAsync("After upgrade")).GetProperty("id").GetGuid();
        var error = await Assert.ThrowsAsync<PostgresException>(() => scope.Db.Database.ExecuteSqlAsync($"""
            INSERT INTO workspace_memberships (workspace_id, user_id, role, joined_at) VALUES ({workspaceId}, {other}, 'owner', now())
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_workspace_memberships_one_owner", error.ConstraintName);
    }

    /// <summary>Applies only the 001 migration and inserts 001-style data (password-less users).</summary>
    private static async Task SeedAs001Async(string connectionString)
    {
        var options = new DbContextOptionsBuilder<DevSyncDbContext>().UseNpgsql(connectionString).Options;
        await using (var db = new DevSyncDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("InitialWorkspaceFoundation");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (id, display_name, created_at) VALUES
              ('0192f5a0-0000-7000-8000-000000000001', 'Dev User A', now()),
              ('0192f5a0-0000-7000-8000-000000000002', 'Dev User B', now()),
              ('0192f5a0-0000-7000-8000-000000000003', 'Dev User C', now());
            INSERT INTO workspaces (id, name, owner_id, created_at, updated_at) VALUES
              ('0193aaaa-0000-7000-8000-000000000001', 'Old 1', '0192f5a0-0000-7000-8000-000000000001', now(), now()),
              ('0193aaaa-0000-7000-8000-000000000002', 'Old 2', '0192f5a0-0000-7000-8000-000000000002', now(), now());
            INSERT INTO workspace_memberships (workspace_id, user_id, role, joined_at) VALUES
              ('0193aaaa-0000-7000-8000-000000000001', '0192f5a0-0000-7000-8000-000000000001', 'owner', now()),
              ('0193aaaa-0000-7000-8000-000000000002', '0192f5a0-0000-7000-8000-000000000002', 'owner', now());
            """;
        await command.ExecuteNonQueryAsync();
    }
}
