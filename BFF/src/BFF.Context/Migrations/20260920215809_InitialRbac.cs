using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BFF.Context.Migrations
{
    /// <inheritdoc />
    public partial class InitialRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rbac_version",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rbac_version", x => x.id);
                    table.CheckConstraint("ck_rbac_version_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "resources",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auth_user_id = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    middle_name = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_closure",
                columns: table => new
                {
                    ancestor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descendant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_closure", x => new { x.ancestor_id, x.descendant_id });
                    table.ForeignKey(
                        name: "fk_group_closure_ancestor",
                        column: x => x.ancestor_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_closure_descendant",
                        column: x => x.descendant_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                columns: table => new
                {
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_type = table.Column<short>(type: "smallint", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_members", x => new { x.group_id, x.member_type, x.member_id });
                    table.CheckConstraint("ck_group_members_member_type", "member_type IN (1,2)");
                    table.CheckConstraint("ck_group_members_no_self_loop", "member_type <> 2 OR member_id <> group_id");
                    table.ForeignKey(
                        name: "fk_group_members_groups",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "access_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_type = table.Column<short>(type: "smallint", nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    permission_mask = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_grants", x => x.id);
                    table.CheckConstraint("ck_access_grants_principal_type", "principal_type IN (1,2)");
                    table.ForeignKey(
                        name: "fk_access_grants_resources",
                        column: x => x.resource_id,
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_grants_resource_id",
                table: "access_grants",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_grants_principal",
                table: "access_grants",
                columns: new[] { "principal_type", "principal_id" })
                .Annotation("Npgsql:IndexInclude", new[] { "resource_id", "permission_mask" });

            migrationBuilder.CreateIndex(
                name: "ux_access_grants_principal_resource",
                table: "access_grants",
                columns: new[] { "principal_type", "principal_id", "resource_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_closure_descendant_ancestor",
                table: "group_closure",
                columns: new[] { "descendant_id", "ancestor_id" });

            migrationBuilder.CreateIndex(
                name: "ix_group_members_member",
                table: "group_members",
                columns: new[] { "member_type", "member_id" })
                .Annotation("Npgsql:IndexInclude", new[] { "group_id" });

            migrationBuilder.CreateIndex(
                name: "ux_groups_code",
                table: "groups",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_resources_code",
                table: "resources",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_auth_user_id",
                table: "users",
                column: "auth_user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_grants");

            migrationBuilder.DropTable(
                name: "group_closure");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "rbac_version");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "resources");

            migrationBuilder.DropTable(
                name: "groups");
        }
    }
}
