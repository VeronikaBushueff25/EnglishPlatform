using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Vocabulary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DictionarySource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "needs_review",
                schema: "vocabulary",
                table: "lexemes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "vocabulary",
                table: "lexemes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.CreateTable(
                name: "dictionary_cache",
                schema: "vocabulary",
                columns: table => new
                {
                    word = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    found = table.Column<bool>(type: "boolean", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_cache", x => x.word);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lexemes_needs_review",
                schema: "vocabulary",
                table: "lexemes",
                column: "needs_review",
                filter: "needs_review");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dictionary_cache",
                schema: "vocabulary");

            migrationBuilder.DropIndex(
                name: "ix_lexemes_needs_review",
                schema: "vocabulary",
                table: "lexemes");

            migrationBuilder.DropColumn(
                name: "needs_review",
                schema: "vocabulary",
                table: "lexemes");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "vocabulary",
                table: "lexemes");
        }
    }
}
