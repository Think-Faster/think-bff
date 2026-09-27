using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BFF.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedSchemaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "work_schedule_work_id_seq");

            migrationBuilder.AddColumn<short>(
                name: "shift_hours",
                table: "schedule_entries",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "shift_start",
                table: "schedule_entries",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "leader_id",
                table: "brigades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit",
                table: "brigades",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "engineer_permits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    level = table.Column<short>(type: "smallint", nullable: true),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    document_no = table.Column<string>(type: "text", nullable: true),
                    checked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    checked_at = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_engineer_permits", x => x.id);
                    table.CheckConstraint("ck_engineer_permits_kind", "kind IN (1,2,3)");
                    table.CheckConstraint("ck_engineer_permits_level", "(kind = 1 AND level BETWEEN 1 AND 3) OR (kind = 2 AND level IS NULL) OR (kind = 3 AND level BETWEEN 2 AND 5)");
                });

            migrationBuilder.CreateTable(
                name: "work_schedule",
                columns: table => new
                {
                    work_id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('work_schedule_work_id_seq')"),
                    version = table.Column<int>(type: "integer", nullable: false),
                    object_id = table.Column<int>(type: "integer", nullable: true),
                    work_kind = table.Column<string>(type: "text", nullable: false),
                    incident_types = table.Column<string[]>(type: "text[]", nullable: false),
                    removed_sensor = table.Column<string>(type: "text", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<short>(type: "smallint", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_schedule", x => new { x.work_id, x.version });
                    table.CheckConstraint("ck_work_schedule_range", "ends_at > starts_at");
                    table.CheckConstraint("ck_work_schedule_source", "source IN (1,2,3)");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_schedule_entries_shift_hours",
                table: "schedule_entries",
                sql: "shift_hours IS NULL OR shift_hours BETWEEN 1 AND 24");

            migrationBuilder.CreateIndex(
                name: "ix_engineer_permits_user_kind",
                table: "engineer_permits",
                columns: new[] { "user_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_work_schedule_object_starts",
                table: "work_schedule",
                columns: new[] { "object_id", "starts_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "engineer_permits");

            migrationBuilder.DropTable(
                name: "work_schedule");

            migrationBuilder.DropCheckConstraint(
                name: "ck_schedule_entries_shift_hours",
                table: "schedule_entries");

            migrationBuilder.DropColumn(
                name: "shift_hours",
                table: "schedule_entries");

            migrationBuilder.DropColumn(
                name: "shift_start",
                table: "schedule_entries");

            migrationBuilder.DropColumn(
                name: "leader_id",
                table: "brigades");

            migrationBuilder.DropColumn(
                name: "unit",
                table: "brigades");

            migrationBuilder.DropSequence(
                name: "work_schedule_work_id_seq");
        }
    }
}
