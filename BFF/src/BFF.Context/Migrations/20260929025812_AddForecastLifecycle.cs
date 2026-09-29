using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BFF.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "alarm_ended_at",
                table: "predictions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "label",
                table: "prediction_factors",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "value_text",
                table: "prediction_evidence",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "alarm_ended_at",
                table: "predictions");

            migrationBuilder.DropColumn(
                name: "label",
                table: "prediction_factors");

            migrationBuilder.DropColumn(
                name: "value_text",
                table: "prediction_evidence");
        }
    }
}
