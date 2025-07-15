using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class BackgroundsToRoleSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_profile_basic_skill_profile_role_background_profile_role_background_id",
                table: "profile_basic_skill");

            migrationBuilder.DropForeignKey(
                name: "FK_profile_easy_skill_profile_role_background_profile_role_background_id",
                table: "profile_easy_skill");

            migrationBuilder.DropTable(
                name: "profile_role_background");

            migrationBuilder.RenameColumn(
                name: "profile_role_background_id",
                table: "profile_easy_skill",
                newName: "profile_role_skills_id");

            migrationBuilder.RenameIndex(
                name: "IX_profile_easy_skill_profile_role_background_id",
                table: "profile_easy_skill",
                newName: "IX_profile_easy_skill_profile_role_skills_id");

            migrationBuilder.RenameColumn(
                name: "profile_role_background_id",
                table: "profile_basic_skill",
                newName: "profile_role_skills_id");

            migrationBuilder.RenameIndex(
                name: "IX_profile_basic_skill_profile_role_background_id",
                table: "profile_basic_skill",
                newName: "IX_profile_basic_skill_profile_role_skills_id");

            migrationBuilder.CreateTable(
                name: "profile_role_skills",
                columns: table => new
                {
                    profile_role_skills_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    role_name = table.Column<string>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_profile_role_skills_profile_id",
                table: "profile_role_skills",
                column: "profile_id");

            migrationBuilder.AddForeignKey(
                name: "FK_profile_basic_skill_profile_role_skills_profile_role_skills_id",
                table: "profile_basic_skill",
                column: "profile_role_skills_id",
                principalTable: "profile_role_skills",
                principalColumn: "profile_role_skills_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_profile_easy_skill_profile_role_skills_profile_role_skills_id",
                table: "profile_easy_skill",
                column: "profile_role_skills_id",
                principalTable: "profile_role_skills",
                principalColumn: "profile_role_skills_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_profile_basic_skill_profile_role_skills_profile_role_skills_id",
                table: "profile_basic_skill");

            migrationBuilder.DropForeignKey(
                name: "FK_profile_easy_skill_profile_role_skills_profile_role_skills_id",
                table: "profile_easy_skill");

            migrationBuilder.DropTable(
                name: "profile_role_skills");

            migrationBuilder.RenameColumn(
                name: "profile_role_skills_id",
                table: "profile_easy_skill",
                newName: "profile_role_background_id");

            migrationBuilder.RenameIndex(
                name: "IX_profile_easy_skill_profile_role_skills_id",
                table: "profile_easy_skill",
                newName: "IX_profile_easy_skill_profile_role_background_id");

            migrationBuilder.RenameColumn(
                name: "profile_role_skills_id",
                table: "profile_basic_skill",
                newName: "profile_role_background_id");

            migrationBuilder.RenameIndex(
                name: "IX_profile_basic_skill_profile_role_skills_id",
                table: "profile_basic_skill",
                newName: "IX_profile_basic_skill_profile_role_background_id");

            migrationBuilder.CreateTable(
                name: "profile_role_background",
                columns: table => new
                {
                    profile_role_background_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    role_name = table.Column<string>(type: "TEXT", nullable: false),
                    selected_adult_background = table.Column<string>(type: "TEXT", nullable: true),
                    selected_baby_background = table.Column<string>(type: "TEXT", nullable: true),
                    selected_general_background = table.Column<string>(type: "TEXT", nullable: true),
                    skillpoint_credit = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_role_background", x => x.profile_role_background_id);
                    table.ForeignKey(
                        name: "FK_profile_role_background_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_profile_role_background_profile_id",
                table: "profile_role_background",
                column: "profile_id");

            migrationBuilder.AddForeignKey(
                name: "FK_profile_basic_skill_profile_role_background_profile_role_background_id",
                table: "profile_basic_skill",
                column: "profile_role_background_id",
                principalTable: "profile_role_background",
                principalColumn: "profile_role_background_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_profile_easy_skill_profile_role_background_profile_role_background_id",
                table: "profile_easy_skill",
                column: "profile_role_background_id",
                principalTable: "profile_role_background",
                principalColumn: "profile_role_background_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
