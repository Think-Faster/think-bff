using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BFF.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTelegram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "telegram",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Telegram был только у инженера (engineer_profiles.telegram, свободный текст). Переносим то, что
            // похоже на имя пользователя Telegram, — без @ и в нижнем регистре (TelegramUsername); chat_id и
            // прочее не переносится, старая колонка остаётся как есть.
            migrationBuilder.Sql(@"
UPDATE users u
SET telegram = lower(substring(btrim(p.telegram) from '^@?([A-Za-z][A-Za-z0-9_]{4,31})$'))
FROM engineer_profiles p
WHERE p.user_id = u.id
  AND u.telegram IS NULL
  AND btrim(p.telegram) ~ '^@?[A-Za-z][A-Za-z0-9_]{4,31}$';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "telegram",
                table: "users");
        }
    }
}
