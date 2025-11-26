using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakelaarLeaderboard.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Makelaar",
                table: "Makelaar");

            migrationBuilder.DropPrimaryKey(
                name: "PK_House",
                table: "House");

            migrationBuilder.RenameTable(
                name: "Makelaar",
                newName: "Makelaars");

            migrationBuilder.RenameTable(
                name: "House",
                newName: "Houses");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Makelaars",
                table: "Makelaars",
                column: "MakelaarId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Houses",
                table: "Houses",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_MakelaarId",
                table: "Houses",
                column: "MakelaarId");

            migrationBuilder.AddForeignKey(
                name: "FK_Houses_Makelaars_MakelaarId",
                table: "Houses",
                column: "MakelaarId",
                principalTable: "Makelaars",
                principalColumn: "MakelaarId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Houses_Makelaars_MakelaarId",
                table: "Houses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Makelaars",
                table: "Makelaars");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Houses",
                table: "Houses");

            migrationBuilder.DropIndex(
                name: "IX_Houses_MakelaarId",
                table: "Houses");

            migrationBuilder.RenameTable(
                name: "Makelaars",
                newName: "Makelaar");

            migrationBuilder.RenameTable(
                name: "Houses",
                newName: "House");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Makelaar",
                table: "Makelaar",
                column: "MakelaarId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_House",
                table: "House",
                column: "Id");
        }
    }
}
