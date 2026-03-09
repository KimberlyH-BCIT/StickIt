using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELKH.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate20260304005757 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    PkCategoryId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.PkCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "RegisteredUsers",
                columns: table => new
                {
                    PkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteredUsers", x => x.PkRegisteredUserId);
                });

            migrationBuilder.CreateTable(
                name: "UserLogs",
                columns: table => new
                {
                    PkLogId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FkEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    LogInTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LogOutTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Abandoned = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogs", x => x.PkLogId);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    PkEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.PkEmail);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactDetails",
                columns: table => new
                {
                    PkContactId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Street = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    Province = table.Column<string>(type: "TEXT", nullable: false),
                    PostCode = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    FkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RegisteredUserPkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactDetails", x => x.PkContactId);
                    table.ForeignKey(
                        name: "FK_ContactDetails_RegisteredUsers_RegisteredUserPkRegisteredUserId",
                        column: x => x.RegisteredUserPkRegisteredUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId");
                });

            migrationBuilder.CreateTable(
                name: "WishLists",
                columns: table => new
                {
                    PkWishListId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FkUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishLists", x => x.PkWishListId);
                    table.ForeignKey(
                        name: "FK_WishLists_RegisteredUsers_FkUserId",
                        column: x => x.FkUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    PkOrderId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderStatus = table.Column<string>(type: "TEXT", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeliveryStatus = table.Column<string>(type: "TEXT", nullable: false),
                    FkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisteredUserPkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    FkContactId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContactDetailPkContactId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.PkOrderId);
                    table.ForeignKey(
                        name: "FK_Orders_ContactDetails_ContactDetailPkContactId",
                        column: x => x.ContactDetailPkContactId,
                        principalTable: "ContactDetails",
                        principalColumn: "PkContactId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_RegisteredUsers_RegisteredUserPkRegisteredUserId",
                        column: x => x.RegisteredUserPkRegisteredUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    PkProductId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    StockQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    FkCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    FkWishListId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.PkProductId);
                    table.ForeignKey(
                        name: "FK_Products_Categories_FkCategoryId",
                        column: x => x.FkCategoryId,
                        principalTable: "Categories",
                        principalColumn: "PkCategoryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Products_WishLists_FkWishListId",
                        column: x => x.FkWishListId,
                        principalTable: "WishLists",
                        principalColumn: "PkWishListId");
                });

            migrationBuilder.CreateTable(
                name: "OrderStatuses",
                columns: table => new
                {
                    OrderStatusId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StatusName = table.Column<string>(type: "TEXT", nullable: false),
                    FkOrderId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatuses", x => x.OrderStatusId);
                    table.ForeignKey(
                        name: "FK_OrderStatuses_Orders_FkOrderId",
                        column: x => x.FkOrderId,
                        principalTable: "Orders",
                        principalColumn: "PkOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    PkTransactionId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeliberyFee = table.Column<decimal>(type: "TEXT", nullable: false),
                    FkOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    FkContactId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContactDetailPkContactId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.PkTransactionId);
                    table.ForeignKey(
                        name: "FK_Transactions_ContactDetails_ContactDetailPkContactId",
                        column: x => x.ContactDetailPkContactId,
                        principalTable: "ContactDetails",
                        principalColumn: "PkContactId");
                    table.ForeignKey(
                        name: "FK_Transactions_Orders_FkOrderId",
                        column: x => x.FkOrderId,
                        principalTable: "Orders",
                        principalColumn: "PkOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Carts",
                columns: table => new
                {
                    PkCartId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    FkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RegisteredUserPkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    FkProductID = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductPkProductId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carts", x => x.PkCartId);
                    table.ForeignKey(
                        name: "FK_Carts_Products_ProductPkProductId",
                        column: x => x.ProductPkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId");
                    table.ForeignKey(
                        name: "FK_Carts_RegisteredUsers_RegisteredUserPkRegisteredUserId",
                        column: x => x.RegisteredUserPkRegisteredUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId");
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    PkOrderItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    FkOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderPkOrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    FkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductPkProductId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.PkOrderItemId);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderPkOrderId",
                        column: x => x.OrderPkOrderId,
                        principalTable: "Orders",
                        principalColumn: "PkOrderId");
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductPkProductId",
                        column: x => x.ProductPkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId");
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    PkProductImageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductImageURL = table.Column<string>(type: "TEXT", nullable: false),
                    FkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductPkProductId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.PkProductImageId);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductPkProductId",
                        column: x => x.ProductPkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId");
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
                    ProductsPkProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    FkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RegisteredUsersPkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRatings", x => x.PkRatingId);
                    table.ForeignKey(
                        name: "FK_ProductRatings_Products_ProductsPkProductId",
                        column: x => x.ProductsPkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId");
                    table.ForeignKey(
                        name: "FK_ProductRatings_RegisteredUsers_RegisteredUsersPkRegisteredUserId",
                        column: x => x.RegisteredUsersPkRegisteredUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Carts_ProductPkProductId",
                table: "Carts",
                column: "ProductPkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_RegisteredUserPkRegisteredUserId",
                table: "Carts",
                column: "RegisteredUserPkRegisteredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactDetails_RegisteredUserPkRegisteredUserId",
                table: "ContactDetails",
                column: "RegisteredUserPkRegisteredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderPkOrderId",
                table: "OrderItems",
                column: "OrderPkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductPkProductId",
                table: "OrderItems",
                column: "ProductPkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ContactDetailPkContactId",
                table: "Orders",
                column: "ContactDetailPkContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RegisteredUserPkRegisteredUserId",
                table: "Orders",
                column: "RegisteredUserPkRegisteredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatuses_FkOrderId",
                table: "OrderStatuses",
                column: "FkOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductPkProductId",
                table: "ProductImages",
                column: "ProductPkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatings_ProductsPkProductId",
                table: "ProductRatings",
                column: "ProductsPkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatings_RegisteredUsersPkRegisteredUserId",
                table: "ProductRatings",
                column: "RegisteredUsersPkRegisteredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_FkCategoryId",
                table: "Products",
                column: "FkCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_FkWishListId",
                table: "Products",
                column: "FkWishListId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ContactDetailPkContactId",
                table: "Transactions",
                column: "ContactDetailPkContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_FkOrderId",
                table: "Transactions",
                column: "FkOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WishLists_FkUserId",
                table: "WishLists",
                column: "FkUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Carts");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "OrderStatuses");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "ProductRatings");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "UserLogs");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "WishLists");

            migrationBuilder.DropTable(
                name: "ContactDetails");

            migrationBuilder.DropTable(
                name: "RegisteredUsers");
        }
    }
}
