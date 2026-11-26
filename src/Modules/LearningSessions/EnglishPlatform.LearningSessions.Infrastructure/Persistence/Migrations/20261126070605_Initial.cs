using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.LearningSessions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "learning_sessions");

            migrationBuilder.CreateTable(
                name: "learning_sessions",
                schema: "learning_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    current_task = table.Column<string>(type: "jsonb", nullable: true),
                    current_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    answered = table.Column<int>(type: "integer", nullable: false),
                    correct = table.Column<int>(type: "integer", nullable: false),
                    almost = table.Column<int>(type: "integer", nullable: false),
                    wrong = table.Column<int>(type: "integer", nullable: false),
                    new_words = table.Column<int>(type: "integer", nullable: false),
                    reviews = table.Column<int>(type: "integer", nullable: false),
                    queue = table.Column<string>(type: "jsonb", nullable: false),
                    introduced_attempts = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learning_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_learning_sessions_user_id_started_at",
                schema: "learning_sessions",
                table: "learning_sessions",
                columns: new[] { "user_id", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "learning_sessions",
                schema: "learning_sessions");
        }
    }
}
