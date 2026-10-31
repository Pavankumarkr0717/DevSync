using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SystemDesignCanvas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "diagrams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    document = table.Column<string>(type: "jsonb", nullable: false),
                    node_count = table.Column<int>(type: "integer", nullable: false),
                    edge_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_diagrams", x => x.id);
                    table.CheckConstraint("ck_diagrams_document", "jsonb_typeof(document) = 'object'");
                    table.CheckConstraint("ck_diagrams_edge_count", "edge_count BETWEEN 0 AND 1000");
                    table.CheckConstraint("ck_diagrams_name", "char_length(btrim(name)) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_diagrams_node_count", "node_count BETWEEN 0 AND 500");
                    table.CheckConstraint("ck_diagrams_revision", "revision >= 1");
                    table.CheckConstraint("ck_diagrams_schema_version", "schema_version >= 1");
                    table.ForeignKey(
                        name: "fk_diagrams_created_by_user_id_users",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_diagrams_updated_by_user_id_users",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_diagrams_workspace_id_workspaces",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_diagrams_created_by_user_id",
                table: "diagrams",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_diagrams_updated_by_user_id",
                table: "diagrams",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_diagrams_workspace_name",
                table: "diagrams",
                columns: new[] { "workspace_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diagrams");
        }
    }
}
