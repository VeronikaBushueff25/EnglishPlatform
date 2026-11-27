using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Pets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pets");

            migrationBuilder.CreateTable(
                name: "pets",
                schema: "pets",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    species = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    palette = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    accessory = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    satiety = table.Column<double>(type: "double precision", nullable: false),
                    affection = table.Column<double>(type: "double precision", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    treats = table.Column<int>(type: "integer", nullable: false),
                    xp = table.Column<int>(type: "integer", nullable: false),
                    last_studied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lessons_completed = table.Column<int>(type: "integer", nullable: false),
                    treats_eaten = table.Column<int>(type: "integer", nullable: false),
                    action_day = table.Column<DateOnly>(type: "date", nullable: false),
                    pats_today = table.Column<int>(type: "integer", nullable: false),
                    plays_today = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pets", x => x.user_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pets",
                schema: "pets");
        }
    }
}
