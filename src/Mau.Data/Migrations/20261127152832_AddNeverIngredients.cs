using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mau.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNeverIngredients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NeverIngredients",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    IngredientId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeverIngredients", x => new { x.UserId, x.IngredientId });
                    table.ForeignKey(
                        name: "FK_NeverIngredients_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NeverIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NeverIngredients_IngredientId",
                table: "NeverIngredients",
                column: "IngredientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NeverIngredients");
        }
    }
}
