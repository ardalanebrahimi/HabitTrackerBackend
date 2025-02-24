using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTrackerBackend.Migrations
{
    /// <inheritdoc />
    public partial class modifyHabitLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PeriodKey",
                table: "habit_logs",
                newName: "WeeklyKey");

            migrationBuilder.AddColumn<int>(
                name: "DailyKey",
                table: "habit_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyKey",
                table: "habit_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Target",
                table: "habit_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyKey",
                table: "habit_logs");

            migrationBuilder.DropColumn(
                name: "MonthlyKey",
                table: "habit_logs");

            migrationBuilder.DropColumn(
                name: "Target",
                table: "habit_logs");

            migrationBuilder.RenameColumn(
                name: "WeeklyKey",
                table: "habit_logs",
                newName: "PeriodKey");
        }
    }
}
