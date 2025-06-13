using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTrackerBackend.Migrations
{    /// <inheritdoc />
    public partial class AddPrivateHabits : Migration
    {        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add IsPrivate column to habits table
            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "habits",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop IsPrivate column from habits table
            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "habits");
        }
    }
}
