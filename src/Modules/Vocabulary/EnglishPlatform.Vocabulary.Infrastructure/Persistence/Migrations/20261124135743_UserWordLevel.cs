using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Vocabulary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserWordLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "level",
                schema: "vocabulary",
                table: "user_words",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "part_of_speech",
                schema: "vocabulary",
                table: "user_words",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            // Уровень и часть речи раньше брались из общей базы на лету; теперь это поля слова — переносим их.
            migrationBuilder.Sql(
                """
                UPDATE vocabulary.user_words AS w
                SET level = l.level, part_of_speech = l.part_of_speech
                FROM vocabulary.lexemes AS l
                WHERE w.lexeme_id = l.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "level",
                schema: "vocabulary",
                table: "user_words");

            migrationBuilder.DropColumn(
                name: "part_of_speech",
                schema: "vocabulary",
                table: "user_words");
        }
    }
}
