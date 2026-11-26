using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.SpacedRepetition.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spaced_repetition");

            migrationBuilder.CreateTable(
                name: "review_cards",
                schema: "spaced_repetition",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_word_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    step = table.Column<int>(type: "integer", nullable: false),
                    stability = table.Column<double>(type: "double precision", nullable: false),
                    difficulty = table.Column<double>(type: "double precision", nullable: false),
                    due = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_review = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reps = table.Column<int>(type: "integer", nullable: false),
                    lapses = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_cards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "review_logs",
                schema: "spaced_repetition",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_word_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    rating = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    elapsed_ms = table.Column<int>(type: "integer", nullable: false),
                    state_before = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    state_after = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    stability_before = table.Column<double>(type: "double precision", nullable: false),
                    stability_after = table.Column<double>(type: "double precision", nullable: false),
                    difficulty_after = table.Column<double>(type: "double precision", nullable: false),
                    elapsed_days = table.Column<double>(type: "double precision", nullable: false),
                    scheduled_days = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_review_cards_user_id_due",
                schema: "spaced_repetition",
                table: "review_cards",
                columns: new[] { "user_id", "due" });

            migrationBuilder.CreateIndex(
                name: "ix_review_cards_user_id_state_created_at",
                schema: "spaced_repetition",
                table: "review_cards",
                columns: new[] { "user_id", "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_review_cards_user_word_id_mode",
                schema: "spaced_repetition",
                table: "review_cards",
                columns: new[] { "user_word_id", "mode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_logs_card_id",
                schema: "spaced_repetition",
                table: "review_logs",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_logs_user_id_reviewed_at",
                schema: "spaced_repetition",
                table: "review_logs",
                columns: new[] { "user_id", "reviewed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_cards",
                schema: "spaced_repetition");

            migrationBuilder.DropTable(
                name: "review_logs",
                schema: "spaced_repetition");
        }
    }
}
