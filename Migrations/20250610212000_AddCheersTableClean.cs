using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTrackerBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCheersTableClean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cheers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    habit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emoji = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cheers", x => x.id);
                    table.ForeignKey(
                        name: "FK_cheers_habits_habit_id",
                        column: x => x.habit_id,
                        principalTable: "habits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cheers_users_from_user_id",
                        column: x => x.from_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cheers_users_to_user_id",
                        column: x => x.to_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cheers_from_user_id",
                table: "cheers",
                column: "from_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_cheers_habit_id",
                table: "cheers",
                column: "habit_id");

            migrationBuilder.CreateIndex(
                name: "IX_cheers_to_user_id",
                table: "cheers",
                column: "to_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cheers");
        }
    }
}
