using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTrackerBackend.Migrations
{
    /// <inheritdoc />
    public partial class modifyHabitAddTargetType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "habits",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "habits");
        }
    }
}
