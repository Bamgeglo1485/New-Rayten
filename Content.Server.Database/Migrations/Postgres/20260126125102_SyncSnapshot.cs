using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class SyncSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "organ_markings",
                table: "profile",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "voice",
                table: "profile",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "voice_pitch",
                table: "profile",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.CreateTable(
                name: "profile_role_skills",
                columns: table => new
                {
                    profile_role_skills_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_id = table.Column<int>(type: "integer", nullable: false),
                    role_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_role_skills", x => x.profile_role_skills_id);
                    table.ForeignKey(
                        name: "FK_profile_role_skills_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_basic_skill",
                columns: table => new
                {
                    profile_basic_skill_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_role_skills_id = table.Column<int>(type: "integer", nullable: false),
                    skill_id = table.Column<string>(type: "text", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_basic_skill", x => x.profile_basic_skill_id);
                    table.ForeignKey(
                        name: "FK_profile_basic_skill_profile_role_skills_profile_role_skills~",
                        column: x => x.profile_role_skills_id,
                        principalTable: "profile_role_skills",
                        principalColumn: "profile_role_skills_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_easy_skill",
                columns: table => new
                {
                    profile_easy_skill_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_role_skills_id = table.Column<int>(type: "integer", nullable: false),
                    skill_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_easy_skill", x => x.profile_easy_skill_id);
                    table.ForeignKey(
                        name: "FK_profile_easy_skill_profile_role_skills_profile_role_skills_~",
                        column: x => x.profile_role_skills_id,
                        principalTable: "profile_role_skills",
                        principalColumn: "profile_role_skills_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_profile_basic_skill_profile_role_skills_id",
                table: "profile_basic_skill",
                column: "profile_role_skills_id");

            migrationBuilder.CreateIndex(
                name: "IX_profile_easy_skill_profile_role_skills_id",
                table: "profile_easy_skill",
                column: "profile_role_skills_id");

            migrationBuilder.CreateIndex(
                name: "IX_profile_role_skills_profile_id",
                table: "profile_role_skills",
                column: "profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_basic_skill");

            migrationBuilder.DropTable(
                name: "profile_easy_skill");

            migrationBuilder.DropTable(
                name: "profile_role_skills");

            migrationBuilder.DropColumn(
                name: "organ_markings",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "voice",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "voice_pitch",
                table: "profile");
        }
    }
}
