using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.LearningProfiles.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "learning_profiles");

            migrationBuilder.CreateTable(
                name: "placement_attempts",
                schema: "learning_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_questions = table.Column<int>(type: "integer", nullable: false),
                    current_question = table.Column<string>(type: "jsonb", nullable: true),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    answers = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "placement_questions",
                schema: "learning_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    passage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    options = table.Column<List<string>>(type: "text[]", nullable: false),
                    correct_option = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_questions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skill_levels",
                schema: "learning_profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    assessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill_levels", x => new { x.user_id, x.skill });
                });

            migrationBuilder.CreateIndex(
                name: "ix_placement_attempts_user_id_started_at",
                schema: "learning_profiles",
                table: "placement_attempts",
                columns: new[] { "user_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_placement_questions_skill_level_is_active",
                schema: "learning_profiles",
                table: "placement_questions",
                columns: new[] { "skill", "level", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "placement_attempts",
                schema: "learning_profiles");

            migrationBuilder.DropTable(
                name: "placement_questions",
                schema: "learning_profiles");

            migrationBuilder.DropTable(
                name: "skill_levels",
                schema: "learning_profiles");
        }
    }
}
