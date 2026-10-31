using DevSync.Infrastructure.Persistence;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace DevSync.IntegrationTests.Persistence;

/// <summary>The ProjectFiles migration is additive (spec 003 research R15) and its constraints back the domain rules.</summary>
public sealed class ProjectFilesMigrationTests(PostgresFixture postgres)
{
    private const string UserId = "0192f5a0-0000-7000-8000-0000000000aa";
    private const string WorkspaceId = "0193aaaa-0000-7000-8000-0000000000aa";

    [Fact]
    public async Task Upgrading_from_002_keeps_every_row_and_adds_the_file_tables()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAs002Async(connectionString);

        await using var factory = new DevSyncApiFactory(connectionString);
        _ = factory.Server; // runs MigrationRunner

        await using var scope = factory.CreateDbScope();
        Assert.Empty(await scope.Db.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await scope.Db.Users.CountAsync());
        Assert.Equal(1, await scope.Db.Workspaces.CountAsync());
        Assert.Equal(1, await scope.Db.WorkspaceMemberships.CountAsync());
        Assert.Equal(1, await scope.Db.WorkspaceInvitations.CountAsync());
        Assert.Equal(0, await scope.Db.WorkspaceItems.CountAsync());

        var indexes = await scope.Db.Database.SqlQuery<string>($"""
            SELECT indexname AS "Value" FROM pg_indexes WHERE tablename IN ('workspace_items', 'file_contents')
            """).ToListAsync();
        Assert.Contains("ux_workspace_items_sibling_name", indexes);
        Assert.Contains("ix_workspace_items_parent_id", indexes);
        Assert.Contains("ix_workspace_items_created_by_user_id", indexes);
        Assert.Contains("ix_file_contents_updated_by_user_id", indexes);

        var checks = await scope.Db.Database.SqlQuery<string>($"""
            SELECT conname AS "Value" FROM pg_constraint WHERE contype = 'c' AND conrelid IN ('workspace_items'::regclass, 'file_contents'::regclass)
            """).ToListAsync();
        Assert.Equal(
            ["ck_file_contents_revision", "ck_file_contents_size", "ck_workspace_items_kind", "ck_workspace_items_name_length"],
            checks.Order());
    }

    [Fact]
    public async Task Sibling_names_are_unique_ignoring_case_even_at_the_top_level()
    {
        await using var factory = await MigratedWithSeedAsync();
        await using var scope = factory.CreateDbScope();

        await InsertItemAsync(scope, "0194aaaa-0000-7000-8000-000000000001", null, "folder", "A");
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertItemAsync(scope, "0194aaaa-0000-7000-8000-000000000002", null, "folder", "a"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_workspace_items_sibling_name", error.ConstraintName);
    }

    [Fact]
    public async Task Content_size_must_match_the_stored_text()
    {
        await using var factory = await MigratedWithSeedAsync();
        await using var scope = factory.CreateDbScope();
        await InsertItemAsync(scope, "0194aaaa-0000-7000-8000-000000000003", null, "file", "a.ts");

        var error = await Assert.ThrowsAsync<PostgresException>(() => scope.Db.Database.ExecuteSqlAsync($"""
            INSERT INTO file_contents (item_id, content, size_bytes, revision, updated_at)
            VALUES ('0194aaaa-0000-7000-8000-000000000003', 'héllo', 5, 1, now())
            """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_file_contents_size", error.ConstraintName);
    }

    [Fact]
    public async Task Deleting_a_folder_or_the_workspace_cascades_to_items_and_contents()
    {
        await using var factory = await MigratedWithSeedAsync();
        await using var scope = factory.CreateDbScope();
        await InsertItemAsync(scope, "0194aaaa-0000-7000-8000-000000000010", null, "folder", "src");
        await InsertItemAsync(scope, "0194aaaa-0000-7000-8000-000000000011", "0194aaaa-0000-7000-8000-000000000010", "folder", "api");
        await InsertItemAsync(scope, "0194aaaa-0000-7000-8000-000000000012", "0194aaaa-0000-7000-8000-000000000011", "file", "a.ts");
        await scope.Db.Database.ExecuteSqlAsync($"""
            INSERT INTO file_contents (item_id, content, size_bytes, revision, updated_at)
            VALUES ('0194aaaa-0000-7000-8000-000000000012', 'x', 1, 1, now())
            """);
        await InsertItemAsync(scope, "0194aaaa-0000-7000-8000-000000000013", null, "file", "keep.md");

        await scope.Db.Database.ExecuteSqlAsync($"DELETE FROM workspace_items WHERE id = '0194aaaa-0000-7000-8000-000000000010'");

        Assert.Equal(1, await scope.Db.WorkspaceItems.CountAsync());
        Assert.Equal(0, await scope.Db.FileContents.CountAsync());

        await scope.Db.Database.ExecuteSqlAsync($"DELETE FROM workspaces WHERE id = {Guid.Parse(WorkspaceId)}");
        Assert.Equal(0, await scope.Db.WorkspaceItems.CountAsync());
    }

    private async Task<DevSyncApiFactory> MigratedWithSeedAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAs002Async(connectionString);
        var factory = new DevSyncApiFactory(connectionString);
        _ = factory.Server;
        return factory;
    }

    private static Task InsertItemAsync(AsyncServiceScopeWithDb scope, string id, string? parentId, string kind, string name) =>
        scope.Db.Database.ExecuteSqlAsync($"""
            INSERT INTO workspace_items (id, workspace_id, parent_id, kind, name, normalized_name, created_at, updated_at)
            VALUES ({Guid.Parse(id)}, {Guid.Parse(WorkspaceId)}, {(parentId is null ? (Guid?)null : Guid.Parse(parentId))}, {kind}, {name}, {name.ToUpperInvariant()}, now(), now())
            """);

    /// <summary>Applies migrations up to 002 and inserts one account, workspace, membership and invitation.</summary>
    private static async Task SeedAs002Async(string connectionString)
    {
        var options = new DbContextOptionsBuilder<DevSyncDbContext>().UseNpgsql(connectionString).Options;
        await using (var db = new DevSyncDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("IdentityAndPermissions");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO users (id, display_name, created_at, user_name, normalized_user_name, email, normalized_email,
                               email_confirmed, password_hash, security_stamp, concurrency_stamp, lockout_enabled, access_failed_count)
            VALUES ('{UserId}', 'Ana', now(), 'ana@example.com', 'ANA@EXAMPLE.COM', 'ana@example.com', 'ANA@EXAMPLE.COM',
                    false, 'hash', 'stamp', 'concurrency', false, 0);
            INSERT INTO workspaces (id, name, owner_id, created_at, updated_at) VALUES ('{WorkspaceId}', 'Kept', '{UserId}', now(), now());
            INSERT INTO workspace_memberships (workspace_id, user_id, role, joined_at) VALUES ('{WorkspaceId}', '{UserId}', 'owner', now());
            INSERT INTO workspace_invitations (id, workspace_id, email, normalized_email, role, invited_by_user_id, status, created_at, expires_at)
            VALUES ('0195aaaa-0000-7000-8000-000000000001', '{WorkspaceId}', 'ben@example.com', 'BEN@EXAMPLE.COM', 'editor', '{UserId}', 'pending', now(), now() + interval '7 days');
            """;
        await command.ExecuteNonQueryAsync();
    }
}
