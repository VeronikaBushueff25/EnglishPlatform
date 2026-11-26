using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.LearningSessions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "learning_sessions",
                table: "learning_sessions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Daily");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                schema: "learning_sessions",
                table: "learning_sessions");
        }
    }
}
