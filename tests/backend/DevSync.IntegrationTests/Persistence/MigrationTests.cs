using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevSync.IntegrationTests.Persistence;

/// <summary>All migrations together create exactly the schema described in the 001–004 data models.</summary>
public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Startup_migrates_an_empty_database_to_the_documented_schema()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        await using var scope = factory.CreateDbScope();
        var db = scope.Db.Database;

        Assert.Empty(await db.GetPendingMigrationsAsync());

        var tables = await db.SqlQuery<string>($"""
            SELECT table_name AS "Value" FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'
            """).ToListAsync();
        Assert.Equal(
            ["diagrams", "file_contents", "refresh_tokens", "sessions", "users", "workspace_invitations", "workspace_items", "workspace_memberships", "workspaces"],
            tables.Order(StringComparer.Ordinal));

        var checks = await db.SqlQuery<string>($"""
            SELECT conname AS "Value" FROM pg_constraint WHERE contype = 'c'
            """).ToListAsync();
        Assert.Equal(
            [
                "ck_diagrams_document",
                "ck_diagrams_edge_count",
                "ck_diagrams_name",
                "ck_diagrams_node_count",
                "ck_diagrams_revision",
                "ck_diagrams_schema_version",
                "ck_file_contents_revision",
                "ck_file_contents_size",
                "ck_refresh_tokens_token_hash_length",
                "ck_sessions_revoked_reason",
                "ck_workspace_invitations_expiry",
                "ck_workspace_invitations_role",
                "ck_workspace_invitations_status",
                "ck_workspace_items_kind",
                "ck_workspace_items_name_length",
                "ck_workspace_memberships_role",
                "ck_workspaces_name_not_blank",
            ],
            checks.Where(c => c.StartsWith("ck_", StringComparison.Ordinal)).Order(StringComparer.Ordinal));

        var foreignKeys = await db.SqlQuery<string>($"""
            SELECT conname || ':' || confdeltype::text AS "Value" FROM pg_constraint WHERE contype = 'f'
            """).ToListAsync();
        // confdeltype: r = RESTRICT, c = CASCADE, n = SET NULL
        Assert.Equal(
            [
                "fk_diagrams_created_by_user_id_users:n",
                "fk_diagrams_updated_by_user_id_users:n",
                "fk_diagrams_workspace_id_workspaces:c",
                "fk_file_contents_item_id_workspace_items:c",
                "fk_file_contents_updated_by_user_id_users:n",
                "fk_refresh_tokens_replaced_by_id_refresh_tokens:n",
                "fk_refresh_tokens_session_id_sessions:c",
                "fk_sessions_user_id_users:c",
                "fk_workspace_invitations_accepted_user_id_users:n",
                "fk_workspace_invitations_invited_by_user_id_users:r",
                "fk_workspace_invitations_workspace_id_workspaces:c",
                "fk_workspace_items_created_by_user_id_users:n",
                "fk_workspace_items_parent_id_workspace_items:c",
                "fk_workspace_items_workspace_id_workspaces:c",
                "fk_workspace_memberships_user_id_users:r",
                "fk_workspace_memberships_workspace_id_workspaces:c",
                "fk_workspaces_owner_id_users:r",
            ],
            foreignKeys.Order(StringComparer.Ordinal));

        var indexes = await db.SqlQuery<string>($"""
            SELECT indexname AS "Value" FROM pg_indexes WHERE schemaname = 'public'
            """).ToListAsync();
        string[] expected =
        [
            "ix_workspace_memberships_user_id", "ix_workspaces_owner_id", "pk_workspace_memberships",
            "ux_workspace_memberships_one_owner", "ux_users_normalized_email", "ux_users_normalized_user_name",
            "ix_sessions_user_id_active", "ux_refresh_tokens_token_hash", "ix_refresh_tokens_session_id",
            "ux_workspace_invitations_pending", "ix_workspace_invitations_pending_email",
            "ux_workspace_items_sibling_name", "ix_workspace_items_parent_id", "ix_workspace_items_created_by_user_id",
            "pk_file_contents", "ix_file_contents_updated_by_user_id",
            "pk_diagrams", "ux_diagrams_workspace_name", "ix_diagrams_created_by_user_id", "ix_diagrams_updated_by_user_id",
        ];
        Assert.All(expected, name => Assert.Contains(name, indexes));
    }

    [Fact]
    public async Task Description_and_name_lengths_are_enforced_by_column_types()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        await using var scope = factory.CreateDbScope();

        var lengths = await scope.Db.Database.SqlQuery<string>($"""
            SELECT column_name || ':' || character_maximum_length AS "Value" FROM information_schema.columns
            WHERE table_name = 'workspaces' AND character_maximum_length IS NOT NULL
            """).ToListAsync();

        Assert.Equal(["description:500", "name:100"], lengths.Order(StringComparer.Ordinal));
    }
}
