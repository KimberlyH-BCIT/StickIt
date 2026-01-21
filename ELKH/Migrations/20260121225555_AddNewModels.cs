using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELKH.Migrations
{
    /// <inheritdoc />
    public partial class AddNewModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    DeliveryId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeliveryStatus = table.Column<string>(type: "TEXT", nullable: false),
                    FkOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    FkAddressId = table.Column<int>(type: "INTEGER", nullable: false),
                    AddressPkAddressId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.DeliveryId);
                    table.ForeignKey(
                        name: "FK_Deliveries_Addresses_AddressPkAddressId",
                        column: x => x.AddressPkAddressId,
                        principalTable: "Addresses",
                        principalColumn: "PkAddressId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Deliveries_Orders_FkOrderId",
                        column: x => x.FkOrderId,
                        principalTable: "Orders",
                        principalColumn: "PkOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductRatings",
                columns: table => new
                {
                    PkRatingId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    RatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductsPkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    FkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisteredUsersPkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRatings", x => x.PkRatingId);
                    table.ForeignKey(
                        name: "FK_ProductRatings_Products_ProductsPkProductId",
                        column: x => x.ProductsPkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductRatings_RegisteredUsers_RegisteredUsersPkRegisteredUserId",
                        column: x => x.RegisteredUsersPkRegisteredUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_AddressPkAddressId",
                table: "Deliveries",
                column: "AddressPkAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_FkOrderId",
                table: "Deliveries",
                column: "FkOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatings_ProductsPkProductId",
                table: "ProductRatings",
                column: "ProductsPkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatings_RegisteredUsersPkRegisteredUserId",
                table: "ProductRatings",
                column: "RegisteredUsersPkRegisteredUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "ProductRatings");
        }
    }
}
