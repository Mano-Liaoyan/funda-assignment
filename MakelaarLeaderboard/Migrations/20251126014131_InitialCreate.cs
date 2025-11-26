using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakelaarLeaderboard.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "House",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MakelaarId = table.Column<int>(type: "INTEGER", nullable: false),
                    Woonplaats = table.Column<string>(type: "TEXT", nullable: true),
                    HasTuin = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_House", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "House");
        }
    }
}
