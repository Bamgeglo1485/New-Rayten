using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class BackgroundsPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "profile_role_background",
                columns: table => new
                {
                    profile_role_background_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_id = table.Column<int>(type: "integer", nullable: false),
                    role_name = table.Column<string>(type: "text", nullable: false),
                    selected_baby_background = table.Column<string>(type: "text", nullable: true),
                    selected_adult_background = table.Column<string>(type: "text", nullable: true),
                    selected_general_background = table.Column<string>(type: "text", nullable: true),
                    skillpoint_credit = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "profile_basic_skill",
                columns: table => new
                {
                    profile_basic_skill_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_role_background_id = table.Column<int>(type: "integer", nullable: false),
                    skill_id = table.Column<string>(type: "text", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_basic_skill", x => x.profile_basic_skill_id);
                    table.ForeignKey(
                        name: "FK_profile_basic_skill_profile_role_background_profile_role_ba~",
                        column: x => x.profile_role_background_id,
                        principalTable: "profile_role_background",
                        principalColumn: "profile_role_background_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_easy_skill",
                columns: table => new
                {
                    profile_easy_skill_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_role_background_id = table.Column<int>(type: "integer", nullable: false),
                    skill_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_easy_skill", x => x.profile_easy_skill_id);
                    table.ForeignKey(
                        name: "FK_profile_easy_skill_profile_role_background_profile_role_bac~",
                        column: x => x.profile_role_background_id,
                        principalTable: "profile_role_background",
                        principalColumn: "profile_role_background_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_profile_basic_skill_profile_role_background_id",
                table: "profile_basic_skill",
                column: "profile_role_background_id");

            migrationBuilder.CreateIndex(
                name: "IX_profile_easy_skill_profile_role_background_id",
                table: "profile_easy_skill",
                column: "profile_role_background_id");

            migrationBuilder.CreateIndex(
                name: "IX_profile_role_background_profile_id",
                table: "profile_role_background",
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
                name: "profile_role_background");
        }
    }
}
