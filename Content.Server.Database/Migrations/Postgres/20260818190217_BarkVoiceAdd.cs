using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class BarkVoiceAdd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bark_voice",
                table: "profile",
                type: "TEXT",
                nullable: false,
                defaultValue: "Papyrus");

            migrationBuilder.AddColumn<float>(
                name: "bark_voice_pitch",
                table: "profile",
                type: "REAL",
                nullable: false,
                defaultValue: 1f);

            migrationBuilder.AddColumn<string>(
                name: "voice",
                table: "profile",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bark_voice",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "bark_voice_pitch",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "voice",
                table: "profile");
        }
    }
}
