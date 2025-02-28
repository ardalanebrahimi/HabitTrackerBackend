using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTrackerBackend.Migrations
{
    /// <inheritdoc />
    public partial class modifyHabitStreak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AllowedGaps",
                table: "habits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "habits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "habits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "habits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StreakTarget",
                table: "habits",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedGaps",
                table: "habits");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "habits");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "habits");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "habits");

            migrationBuilder.DropColumn(
                name: "StreakTarget",
                table: "habits");
        }
    }
}
