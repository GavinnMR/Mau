using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mau.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDishMainProtein : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MainProteinId",
                table: "Dishes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dishes_MainProteinId",
                table: "Dishes",
                column: "MainProteinId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dishes_Ingredients_MainProteinId",
                table: "Dishes",
                column: "MainProteinId",
                principalTable: "Ingredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Existing dishes: the main protein is the first required protein ingredient.
            migrationBuilder.Sql(
                """
                UPDATE "Dishes"
                SET "MainProteinId" = (
                    SELECT "di"."IngredientId"
                    FROM "DishIngredients" AS "di"
                    INNER JOIN "Ingredients" AS "i" ON "i"."Id" = "di"."IngredientId"
                    WHERE "di"."DishId" = "Dishes"."Id"
                      AND "di"."IsOptional" = 0
                      AND "i"."Category" = 'Protein'
                    ORDER BY "di"."Position"
                    LIMIT 1)
                WHERE "MainProteinId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dishes_Ingredients_MainProteinId",
                table: "Dishes");

            migrationBuilder.DropIndex(
                name: "IX_Dishes_MainProteinId",
                table: "Dishes");

            migrationBuilder.DropColumn(
                name: "MainProteinId",
                table: "Dishes");
        }
    }
}
