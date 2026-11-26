using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Vocabulary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vocabulary");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "vocabulary",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lexemes",
                schema: "vocabulary",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    normalized_text = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    part_of_speech = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    transcription = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    audio_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    category_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    examples = table.Column<string>(type: "jsonb", nullable: true),
                    translations = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lexemes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_words",
                schema: "vocabulary",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lexeme_id = table.Column<Guid>(type: "uuid", nullable: true),
                    text = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    normalized_text = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    transcription = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    translations = table.Column<List<string>>(type: "text[]", nullable: false),
                    example = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    example_translation = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_edited = table.Column<bool>(type: "boolean", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_words", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_categories_slug",
                schema: "vocabulary",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lexemes_category_ids",
                schema: "vocabulary",
                table: "lexemes",
                column: "category_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_lexemes_level",
                schema: "vocabulary",
                table: "lexemes",
                column: "level");

            migrationBuilder.CreateIndex(
                name: "ix_lexemes_normalized_text_part_of_speech",
                schema: "vocabulary",
                table: "lexemes",
                columns: new[] { "normalized_text", "part_of_speech" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_user_words_lexeme_id",
                schema: "vocabulary",
                table: "user_words",
                column: "lexeme_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_words_user_id_added_at",
                schema: "vocabulary",
                table: "user_words",
                columns: new[] { "user_id", "added_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_words_user_id_lexeme_id",
                schema: "vocabulary",
                table: "user_words",
                columns: new[] { "user_id", "lexeme_id" },
                unique: true,
                filter: "lexeme_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_words_user_id_normalized_text",
                schema: "vocabulary",
                table: "user_words",
                columns: new[] { "user_id", "normalized_text" },
                unique: true,
                filter: "lexeme_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categories",
                schema: "vocabulary");

            migrationBuilder.DropTable(
                name: "lexemes",
                schema: "vocabulary");

            migrationBuilder.DropTable(
                name: "user_words",
                schema: "vocabulary");
        }
    }
}
