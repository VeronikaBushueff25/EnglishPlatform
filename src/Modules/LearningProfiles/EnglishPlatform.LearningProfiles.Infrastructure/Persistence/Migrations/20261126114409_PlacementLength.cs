using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.LearningProfiles.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlacementLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "length",
                schema: "learning_profiles",
                table: "placement_attempts",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Short");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "length",
                schema: "learning_profiles",
                table: "placement_attempts");
        }
    }
}
