using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BFF.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionMutedUntil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "muted_until",
                table: "prediction_decisions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "muted_until",
                table: "prediction_decisions");
        }
    }
}
