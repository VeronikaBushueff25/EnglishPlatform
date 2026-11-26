using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Vocabulary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReadingTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reading_texts",
                schema: "vocabulary",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    topic = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    word_count = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reading_texts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reading_texts_is_published_level",
                schema: "vocabulary",
                table: "reading_texts",
                columns: new[] { "is_published", "level" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reading_texts",
                schema: "vocabulary");
        }
    }
}
