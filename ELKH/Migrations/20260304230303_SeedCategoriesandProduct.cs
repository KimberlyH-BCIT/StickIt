using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ELKH.Migrations
{
    /// <inheritdoc />
    public partial class SeedCategoriesandProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "PkCategoryId", "CategoryName" },
                values: new object[,]
                {
                    { 1, "Carton" },
                    { 2, "Fake" }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "PkProductId", "Description", "FkCategoryId", "FkWishListId", "IsActive", "Name", "Price", "StockQuantity" },
                values: new object[,]
                {
                    { 1, "Character from anime", 1, null, true, "Pikacu", 2.99m, 10 },
                    { 2, "Random", 1, null, true, "Random", 1.99m, 80 },
                    { 3, "Random2", 2, null, true, "Random2", 4.99m, 80 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "PkProductId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "PkProductId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "PkProductId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "PkCategoryId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "PkCategoryId",
                keyValue: 2);
        }
    }
}
