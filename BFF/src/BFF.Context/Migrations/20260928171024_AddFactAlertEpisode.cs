using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BFF.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddFactAlertEpisode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "details",
                table: "fact_alerts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_at",
                table: "fact_alerts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Записи до обновлений эпизода: последнее известное время — объявление.
            migrationBuilder.Sql("UPDATE fact_alerts SET last_at = announced_at;");

            migrationBuilder.AddColumn<string>(
                name: "route",
                table: "fact_alerts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_fact_alerts_last_at",
                table: "fact_alerts",
                column: "last_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fact_alerts_last_at",
                table: "fact_alerts");

            migrationBuilder.DropColumn(
                name: "details",
                table: "fact_alerts");

            migrationBuilder.DropColumn(
                name: "last_at",
                table: "fact_alerts");

            migrationBuilder.DropColumn(
                name: "route",
                table: "fact_alerts");
        }
    }
}
