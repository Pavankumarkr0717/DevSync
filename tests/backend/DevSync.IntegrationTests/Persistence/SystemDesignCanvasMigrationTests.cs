using DevSync.Infrastructure.Persistence;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace DevSync.IntegrationTests.Persistence;

/// <summary>The SystemDesignCanvas migration is additive (spec 004 research R14, FR-037).</summary>
public sealed class SystemDesignCanvasMigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Upgrading_from_003_keeps_workspaces_and_files_and_starts_with_no_diagrams()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<DevSyncDbContext>().UseNpgsql(connectionString).Options;
        await using (var db = new DevSyncDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("ProjectFiles");
        }

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO users (id, display_name, created_at, user_name, normalized_user_name, email, normalized_email,
                                   email_confirmed, password_hash, security_stamp, concurrency_stamp, lockout_enabled, access_failed_count)
                VALUES ('0192f5a0-0000-7000-8000-0000000000bb', 'Ana', now(), 'a@e.com', 'A@E.COM', 'a@e.com', 'A@E.COM',
                        false, 'h', 's', 'c', false, 0);
                INSERT INTO workspaces (id, name, owner_id, created_at, updated_at)
                VALUES ('0193aaaa-0000-7000-8000-0000000000bb', 'Kept', '0192f5a0-0000-7000-8000-0000000000bb', now(), now());
                INSERT INTO workspace_memberships (workspace_id, user_id, role, joined_at)
                VALUES ('0193aaaa-0000-7000-8000-0000000000bb', '0192f5a0-0000-7000-8000-0000000000bb', 'owner', now());
                INSERT INTO workspace_items (id, workspace_id, parent_id, kind, name, normalized_name, created_at, updated_at)
                VALUES ('0194aaaa-0000-7000-8000-0000000000bb', '0193aaaa-0000-7000-8000-0000000000bb', NULL, 'file', 'a.ts', 'A.TS', now(), now());
                INSERT INTO file_contents (item_id, content, size_bytes, revision, updated_at)
                VALUES ('0194aaaa-0000-7000-8000-0000000000bb', 'kept', 4, 3, now());
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using var factory = new DevSyncApiFactory(connectionString);
        _ = factory.Server; // runs MigrationRunner

        await using var scope = factory.CreateDbScope();
        Assert.Empty(await scope.Db.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await scope.Db.Workspaces.CountAsync());
        var content = await scope.Db.FileContents.AsNoTracking().SingleAsync();
        Assert.Equal(("kept", 3L), (content.Content, content.Revision));
        Assert.Equal(0, await scope.Db.Diagrams.CountAsync());
    }
}
