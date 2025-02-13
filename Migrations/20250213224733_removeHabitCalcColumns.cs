using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTrackerBackend.Migrations
{
    /// <inheritdoc />
    public partial class removeHabitCalcColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentValue",
                table: "habits");

            migrationBuilder.DropColumn(
                name: "Streak",
                table: "habits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentValue",
                table: "habits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Streak",
                table: "habits",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
