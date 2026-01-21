using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELKH.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDatabaseRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "PRAGMA foreign_keys = OFF;",
                suppressTransaction: true
            );

            migrationBuilder.DropColumn(
                name: "Price",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "OrderItems");

            migrationBuilder.AddColumn<int>(
                name: "FkProductId",
                table: "OrderItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProductsPkProductId",
                table: "OrderItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductsPkProductId",
                table: "OrderItems",
                column: "ProductsPkProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductsPkProductId",
                table: "OrderItems",
                column: "ProductsPkProductId",
                principalTable: "Products",
                principalColumn: "PkProductId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(
                "PRAGMA foreign_keys = ON;",
                suppressTransaction: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductsPkProductId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ProductsPkProductId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "FkProductId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ProductsPkProductId",
                table: "OrderItems");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "OrderItems",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "OrderItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
