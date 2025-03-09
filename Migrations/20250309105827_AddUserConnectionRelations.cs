using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitTrackerBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserConnectionRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_connections_ConnectedUserId",
                table: "connections",
                column: "ConnectedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_connections_UserId",
                table: "connections",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_connections_users_ConnectedUserId",
                table: "connections",
                column: "ConnectedUserId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_connections_users_UserId",
                table: "connections",
                column: "UserId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_connections_users_ConnectedUserId",
                table: "connections");

            migrationBuilder.DropForeignKey(
                name: "FK_connections_users_UserId",
                table: "connections");

            migrationBuilder.DropIndex(
                name: "IX_connections_ConnectedUserId",
                table: "connections");

            migrationBuilder.DropIndex(
                name: "IX_connections_UserId",
                table: "connections");
        }
    }
}
