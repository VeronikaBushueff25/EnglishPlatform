using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Exercises.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "exercises");

            migrationBuilder.CreateTable(
                name: "exercise_attempts",
                schema: "exercises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_word_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    task_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    answer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    used_hint = table.Column<bool>(type: "boolean", nullable: false),
                    elapsed_ms = table.Column<int>(type: "integer", nullable: false),
                    verdict = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    error_tag = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    correct_answer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exercise_attempts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exercise_attempts_session_id",
                schema: "exercises",
                table: "exercise_attempts",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_exercise_attempts_user_id_answered_at",
                schema: "exercises",
                table: "exercise_attempts",
                columns: new[] { "user_id", "answered_at" });

            migrationBuilder.CreateIndex(
                name: "ix_exercise_attempts_user_id_error_tag",
                schema: "exercises",
                table: "exercise_attempts",
                columns: new[] { "user_id", "error_tag" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exercise_attempts",
                schema: "exercises");
        }
    }
}
