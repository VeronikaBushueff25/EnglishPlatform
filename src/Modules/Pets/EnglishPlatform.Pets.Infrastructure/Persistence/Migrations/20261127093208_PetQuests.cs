using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Pets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PetQuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Счётчик игр с мячиком больше не нужен (игра стала вопросами о словах) — это не переименование.
            migrationBuilder.DropColumn(
                name: "plays_today",
                schema: "pets",
                table: "pets");

            migrationBuilder.AddColumn<int>(
                name: "words_added",
                schema: "pets",
                table: "pets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "best_streak",
                schema: "pets",
                table: "pets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "games_played",
                schema: "pets",
                table: "pets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "last_study_day",
                schema: "pets",
                table: "pets",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "quest_day",
                schema: "pets",
                table: "pets",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "quests",
                schema: "pets",
                table: "pets",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quests_completed",
                schema: "pets",
                table: "pets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "quiz_correct",
                schema: "pets",
                table: "pets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "quiz_today",
                schema: "pets",
                table: "pets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "study_streak",
                schema: "pets",
                table: "pets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "texts_read",
                schema: "pets",
                table: "pets",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "best_streak",
                schema: "pets",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "games_played",
                schema: "pets",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "last_study_day",
                schema: "pets",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "quest_day",
                schema: "pets",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "quests",
                schema: "pets",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "quests_completed",
                schema: "pets",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "quiz_correct",
                schema: "pets",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "quiz_today",
                schema: "pets",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "study_streak",
                schema: "pets",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "texts_read",
                schema: "pets",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "words_added",
                schema: "pets",
                table: "pets");

            migrationBuilder.AddColumn<int>(
                name: "plays_today",
                schema: "pets",
                table: "pets",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
