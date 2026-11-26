using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Grammar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "grammar");

            migrationBuilder.CreateTable(
                name: "grammar_topics",
                schema: "grammar",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    summary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    examples = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grammar_topics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "error_tags",
                schema: "grammar",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_error_tags", x => x.code);
                    table.ForeignKey(
                        name: "fk_error_tags_topics_topic_id",
                        column: x => x.topic_id,
                        principalSchema: "grammar",
                        principalTable: "grammar_topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_error_tags_topic_id",
                schema: "grammar",
                table: "error_tags",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "ix_grammar_topics_slug",
                schema: "grammar",
                table: "grammar_topics",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "error_tags",
                schema: "grammar");

            migrationBuilder.DropTable(
                name: "grammar_topics",
                schema: "grammar");
        }
    }
}
