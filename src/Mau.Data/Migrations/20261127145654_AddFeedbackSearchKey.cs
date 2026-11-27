using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mau.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackSearchKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DishFeedback_UserId_DishId",
                table: "DishFeedback");

            migrationBuilder.AddColumn<string>(
                name: "SearchKey",
                table: "DishFeedback",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DishFeedback_UserId_DishId_SearchKey",
                table: "DishFeedback",
                columns: new[] { "UserId", "DishId", "SearchKey" },
                unique: true,
                filter: "\"SearchKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DishFeedback_UserId_DishId_SearchKey",
                table: "DishFeedback");

            migrationBuilder.DropColumn(
                name: "SearchKey",
                table: "DishFeedback");

            migrationBuilder.CreateIndex(
                name: "IX_DishFeedback_UserId_DishId",
                table: "DishFeedback",
                columns: new[] { "UserId", "DishId" });
        }
    }
}
