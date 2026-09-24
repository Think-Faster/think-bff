using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BFF.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assigned_objects",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_id = table.Column<int>(type: "integer", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assigned_objects", x => new { x.user_id, x.object_id });
                });

            migrationBuilder.CreateTable(
                name: "brigades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brigades", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "coefficients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    share = table.Column<double>(type: "double precision", nullable: false),
                    reject_k = table.Column<double>(type: "double precision", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coefficients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fact_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    announced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    trigger_sensor_ids = table.Column<int[]>(type: "integer[]", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fact_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ignored_ranges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<short>(type: "smallint", nullable: false),
                    object_id = table.Column<int>(type: "integer", nullable: true),
                    sensor_id = table.Column<int>(type: "integer", nullable: true),
                    date_from = table.Column<DateOnly>(type: "date", nullable: false),
                    date_to = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ignored_ranges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prediction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incidents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "map_layers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<short>(type: "smallint", nullable: false),
                    object_id = table.Column<int>(type: "integer", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    geojson = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_layers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "model_versions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    switched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    switched_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_model_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "objects",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<short>(type: "smallint", nullable: false),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    geometry_geojson = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    status_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_objects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pickets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    object_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    geometry_geojson = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pickets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "predictions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    hour_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    horizon_hours = table.Column<short>(type: "smallint", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    threshold = table.Column<double>(type: "double precision", nullable: false),
                    alarm = table.Column<bool>(type: "boolean", nullable: false),
                    probability = table.Column<double>(type: "double precision", nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    since_hours = table.Column<int>(type: "integer", nullable: false),
                    topic = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    classification = table.Column<string>(type: "text", nullable: true),
                    recommendation = table.Column<string>(type: "text", nullable: true),
                    model_version_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    muted_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_predictions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "retrain_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    @params = table.Column<string>(name: "params", type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result_model_version_id = table.Column<string>(type: "text", nullable: true),
                    log_ref = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retrain_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_from = table.Column<DateOnly>(type: "date", nullable: false),
                    date_to = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    source = table.Column<string>(type: "text", nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sensor_links",
                columns: table => new
                {
                    from_sensor_id = table.Column<int>(type: "integer", nullable: false),
                    to_sensor_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sensor_links", x => new { x.from_sensor_id, x.to_sensor_id, x.kind });
                });

            migrationBuilder.CreateTable(
                name: "sensors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    object_id = table.Column<int>(type: "integer", nullable: false),
                    picket_id = table.Column<long>(type: "bigint", nullable: true),
                    system = table.Column<string>(type: "text", nullable: false),
                    stype = table.Column<string>(type: "text", nullable: false),
                    tag = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sensors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "text", nullable: false),
                    source_type = table.Column<short>(type: "smallint", nullable: false),
                    object_id = table.Column<int>(type: "integer", nullable: false),
                    picket_id = table.Column<long>(type: "bigint", nullable: true),
                    topic = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    work_type = table.Column<string>(type: "text", nullable: true),
                    fault_classification = table.Column<string>(type: "text", nullable: true),
                    sensor_ids = table.Column<int[]>(type: "integer[]", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    dispatcher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_activity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_action = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_activity", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "engineer_profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    brigade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    telegram = table.Column<string>(type: "text", nullable: true),
                    specialization = table.Column<string[]>(type: "text[]", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_engineer_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_engineer_profiles_brigades",
                        column: x => x.brigade_id,
                        principalTable: "brigades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "prediction_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<short>(type: "smallint", nullable: false),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prediction_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_prediction_decisions_predictions",
                        column: x => x.prediction_id,
                        principalTable: "predictions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prediction_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sensor_id = table.Column<int>(type: "integer", nullable: false),
                    picket_id = table.Column<long>(type: "bigint", nullable: true),
                    ts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prediction_evidence", x => x.id);
                    table.ForeignKey(
                        name: "fk_prediction_evidence_predictions",
                        column: x => x.prediction_id,
                        principalTable: "predictions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prediction_factors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: false),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    direction = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prediction_factors", x => x.id);
                    table.ForeignKey(
                        name: "fk_prediction_factors_predictions",
                        column: x => x.prediction_id,
                        principalTable: "predictions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    engineer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_assignments_tasks",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_predictions",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attached_by = table.Column<Guid>(type: "uuid", nullable: false),
                    attached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    detached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_predictions", x => new { x.task_id, x.prediction_id });
                    table.ForeignKey(
                        name: "fk_task_predictions_predictions",
                        column: x => x.prediction_id,
                        principalTable: "predictions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_predictions_tasks",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    engineer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actual_state = table.Column<string>(type: "text", nullable: true),
                    works_done = table.Column<string>(type: "text", nullable: true),
                    result_code = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_reports_tasks",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    returned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_returns", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_returns_tasks",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assigned_objects_object_id",
                table: "assigned_objects",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "ux_coefficients_type_version",
                table: "coefficients",
                columns: new[] { "type", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_engineer_profiles_brigade_id",
                table: "engineer_profiles",
                column: "brigade_id");

            migrationBuilder.CreateIndex(
                name: "ix_fact_alerts_object_type",
                table: "fact_alerts",
                columns: new[] { "object_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_ignored_ranges_scope",
                table: "ignored_ranges",
                column: "scope");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_object_id",
                table: "incidents",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_layers_object_level",
                table: "map_layers",
                columns: new[] { "object_id", "level" });

            migrationBuilder.CreateIndex(
                name: "ix_objects_parent_id",
                table: "objects",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ux_pickets_object_code",
                table: "pickets",
                columns: new[] { "object_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prediction_decisions_prediction_id",
                table: "prediction_decisions",
                column: "prediction_id");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_evidence_prediction_id",
                table: "prediction_evidence",
                column: "prediction_id");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_factors_prediction_id",
                table: "prediction_factors",
                column: "prediction_id");

            migrationBuilder.CreateIndex(
                name: "ix_predictions_object_status",
                table: "predictions",
                columns: new[] { "object_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_predictions_dedup",
                table: "predictions",
                columns: new[] { "object_id", "type", "hour_end", "model_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_entries_user_range",
                table: "schedule_entries",
                columns: new[] { "user_id", "date_from", "date_to" });

            migrationBuilder.CreateIndex(
                name: "ix_sensors_object_id",
                table: "sensors",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "ix_sensors_picket_id",
                table: "sensors",
                column: "picket_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_assignments_engineer_id",
                table: "task_assignments",
                column: "engineer_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_assignments_task_id",
                table: "task_assignments",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_predictions_prediction_id",
                table: "task_predictions",
                column: "prediction_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_reports_task_id",
                table: "task_reports",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_returns_task_id",
                table: "task_returns",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_dispatcher_id",
                table: "tasks",
                column: "dispatcher_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_object_status",
                table: "tasks",
                columns: new[] { "object_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_tasks_number",
                table: "tasks",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assigned_objects");

            migrationBuilder.DropTable(
                name: "coefficients");

            migrationBuilder.DropTable(
                name: "engineer_profiles");

            migrationBuilder.DropTable(
                name: "fact_alerts");

            migrationBuilder.DropTable(
                name: "ignored_ranges");

            migrationBuilder.DropTable(
                name: "incidents");

            migrationBuilder.DropTable(
                name: "map_layers");

            migrationBuilder.DropTable(
                name: "model_versions");

            migrationBuilder.DropTable(
                name: "objects");

            migrationBuilder.DropTable(
                name: "pickets");

            migrationBuilder.DropTable(
                name: "prediction_decisions");

            migrationBuilder.DropTable(
                name: "prediction_evidence");

            migrationBuilder.DropTable(
                name: "prediction_factors");

            migrationBuilder.DropTable(
                name: "retrain_jobs");

            migrationBuilder.DropTable(
                name: "schedule_entries");

            migrationBuilder.DropTable(
                name: "sensor_links");

            migrationBuilder.DropTable(
                name: "sensors");

            migrationBuilder.DropTable(
                name: "task_assignments");

            migrationBuilder.DropTable(
                name: "task_predictions");

            migrationBuilder.DropTable(
                name: "task_reports");

            migrationBuilder.DropTable(
                name: "task_returns");

            migrationBuilder.DropTable(
                name: "user_activity");

            migrationBuilder.DropTable(
                name: "brigades");

            migrationBuilder.DropTable(
                name: "predictions");

            migrationBuilder.DropTable(
                name: "tasks");
        }
    }
}
