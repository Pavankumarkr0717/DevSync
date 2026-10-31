using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Spec 002 FR-030: every 001 user is a password-less development user, so all 001 data is
            // removed before users become real accounts. MigrationRunner logs the counts beforehand.
            migrationBuilder.Sql("DELETE FROM workspace_memberships;");
            migrationBuilder.Sql("DELETE FROM workspaces;");
            migrationBuilder.Sql("DELETE FROM users;");
            migrationBuilder.DropCheckConstraint(
                name: "ck_workspace_memberships_role",
                table: "workspace_memberships");

            migrationBuilder.AddColumn<int>(
                name: "access_failed_count",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "concurrency_stamp",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "email_confirmed",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "lockout_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lockout_end",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_email",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "normalized_user_name",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "security_stamp",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "user_name",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    idle_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.CheckConstraint("ck_sessions_revoked_reason", "revoked_reason IN ('logout','logout_all','refresh_token_reuse')");
                    table.ForeignKey(
                        name: "fk_sessions_user_id_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_invitations", x => x.id);
                    table.CheckConstraint("ck_workspace_invitations_expiry", "expires_at > created_at");
                    table.CheckConstraint("ck_workspace_invitations_role", "role IN ('editor','viewer')");
                    table.CheckConstraint("ck_workspace_invitations_status", "status IN ('pending','accepted','declined','revoked','expired')");
                    table.ForeignKey(
                        name: "fk_workspace_invitations_accepted_user_id_users",
                        column: x => x.accepted_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_workspace_invitations_invited_by_user_id_users",
                        column: x => x.invited_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workspace_invitations_workspace_id_workspaces",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.CheckConstraint("ck_refresh_tokens_token_hash_length", "octet_length(token_hash) = 32");
                    table.ForeignKey(
                        name: "fk_refresh_tokens_replaced_by_id_refresh_tokens",
                        column: x => x.replaced_by_id,
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_session_id_sessions",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_workspace_memberships_one_owner",
                table: "workspace_memberships",
                column: "workspace_id",
                unique: true,
                filter: "role = 'owner'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_workspace_memberships_role",
                table: "workspace_memberships",
                sql: "role IN ('owner','editor','viewer')");

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_user_name",
                table: "users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_replaced_by_id",
                table: "refresh_tokens",
                column: "replaced_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_session_id",
                table: "refresh_tokens",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_id_active",
                table: "sessions",
                column: "user_id",
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_invitations_accepted_user_id",
                table: "workspace_invitations",
                column: "accepted_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_invitations_invited_by_user_id",
                table: "workspace_invitations",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_invitations_pending_email",
                table: "workspace_invitations",
                column: "normalized_email",
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_invitations_workspace_id",
                table: "workspace_invitations",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ux_workspace_invitations_pending",
                table: "workspace_invitations",
                columns: new[] { "workspace_id", "normalized_email" },
                unique: true,
                filter: "status = 'pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "workspace_invitations");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropIndex(
                name: "ux_workspace_memberships_one_owner",
                table: "workspace_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_workspace_memberships_role",
                table: "workspace_memberships");

            migrationBuilder.DropIndex(
                name: "ux_users_normalized_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ux_users_normalized_user_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "access_failed_count",
                table: "users");

            migrationBuilder.DropColumn(
                name: "concurrency_stamp",
                table: "users");

            migrationBuilder.DropColumn(
                name: "email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "email_confirmed",
                table: "users");

            migrationBuilder.DropColumn(
                name: "lockout_enabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "lockout_end",
                table: "users");

            migrationBuilder.DropColumn(
                name: "normalized_email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "normalized_user_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                table: "users");

            migrationBuilder.DropColumn(
                name: "user_name",
                table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_workspace_memberships_role",
                table: "workspace_memberships",
                sql: "role IN ('owner')");
        }
    }
}
