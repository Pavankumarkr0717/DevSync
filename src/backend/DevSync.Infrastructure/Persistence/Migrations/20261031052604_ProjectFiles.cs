using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspace_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_items", x => x.id);
                    table.CheckConstraint("ck_workspace_items_kind", "kind IN ('folder','file')");
                    table.CheckConstraint("ck_workspace_items_name_length", "char_length(name) BETWEEN 1 AND 255");
                    table.ForeignKey(
                        name: "fk_workspace_items_created_by_user_id_users",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_workspace_items_parent_id_workspace_items",
                        column: x => x.parent_id,
                        principalTable: "workspace_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workspace_items_workspace_id_workspaces",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "file_contents",
                columns: table => new
                {
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_contents", x => x.item_id);
                    table.CheckConstraint("ck_file_contents_revision", "revision >= 1");
                    table.CheckConstraint("ck_file_contents_size", "size_bytes BETWEEN 0 AND 1048576 AND size_bytes = octet_length(content)");
                    table.ForeignKey(
                        name: "fk_file_contents_item_id_workspace_items",
                        column: x => x.item_id,
                        principalTable: "workspace_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_file_contents_updated_by_user_id_users",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_contents_updated_by_user_id",
                table: "file_contents",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_items_created_by_user_id",
                table: "workspace_items",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_items_parent_id",
                table: "workspace_items",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ux_workspace_items_sibling_name",
                table: "workspace_items",
                columns: new[] { "workspace_id", "parent_id", "normalized_name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_contents");

            migrationBuilder.DropTable(
                name: "workspace_items");
        }
    }
}
