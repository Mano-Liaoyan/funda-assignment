using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakelaarLeaderboard.Migrations
{
    /// <inheritdoc />
    public partial class Makelaar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Makelaar",
                columns: table => new
                {
                    MakelaarId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MakelaarNaam = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Makelaar", x => x.MakelaarId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Makelaar");
        }
    }
}
