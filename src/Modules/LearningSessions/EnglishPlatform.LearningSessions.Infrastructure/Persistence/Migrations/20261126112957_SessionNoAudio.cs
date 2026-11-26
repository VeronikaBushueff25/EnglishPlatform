using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishPlatform.LearningSessions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionNoAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "no_audio_until",
                schema: "learning_sessions",
                table: "learning_sessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "no_audio_until",
                schema: "learning_sessions",
                table: "learning_sessions");
        }
    }
}
