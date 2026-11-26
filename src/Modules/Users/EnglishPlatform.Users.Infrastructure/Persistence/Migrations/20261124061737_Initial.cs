using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "users");

            migrationBuilder.CreateTable(
                name: "user_profiles",
                schema: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    interface_language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    goals = table.Column<string[]>(type: "text[]", nullable: false),
                    daily_new_words = table.Column<int>(type: "integer", nullable: false),
                    daily_reviews = table.Column<int>(type: "integer", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    plan = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_profiles", x => x.user_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_profiles",
                schema: "users");
        }
    }
}
