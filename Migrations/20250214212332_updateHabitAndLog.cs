using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTrackerBackend.Migrations
{
    /// <inheritdoc />
    public partial class updateHabitAndLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HabitLogs_habits_HabitId",
                table: "HabitLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_HabitLogs",
                table: "HabitLogs");

            migrationBuilder.RenameTable(
                name: "HabitLogs",
                newName: "habit_logs");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "habit_logs",
                newName: "Timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_HabitLogs_HabitId",
                table: "habit_logs",
                newName: "IX_habit_logs_HabitId");

            migrationBuilder.AddColumn<int>(
                name: "PeriodKey",
                table: "habit_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_habit_logs",
                table: "habit_logs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_habit_logs_habits_HabitId",
                table: "habit_logs",
                column: "HabitId",
                principalTable: "habits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_habit_logs_habits_HabitId",
                table: "habit_logs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_habit_logs",
                table: "habit_logs");

            migrationBuilder.DropColumn(
                name: "PeriodKey",
                table: "habit_logs");

            migrationBuilder.RenameTable(
                name: "habit_logs",
                newName: "HabitLogs");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "HabitLogs",
                newName: "Date");

            migrationBuilder.RenameIndex(
                name: "IX_habit_logs_HabitId",
                table: "HabitLogs",
                newName: "IX_HabitLogs_HabitId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_HabitLogs",
                table: "HabitLogs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_HabitLogs_habits_HabitId",
                table: "HabitLogs",
                column: "HabitId",
                principalTable: "habits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
