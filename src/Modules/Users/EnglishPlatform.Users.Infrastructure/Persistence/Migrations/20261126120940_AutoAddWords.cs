using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutoAddWords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "auto_add_words",
                schema: "users",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "auto_add_words",
                schema: "users",
                table: "user_profiles");
        }
    }
}
