using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELKH.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialApplicationSchema : Migration
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
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    AffectedKeysCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CachedFuzzyKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CacheKey = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedFuzzyKeys", x => x.Id);
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
                name: "Coupons",
                columns: table => new
                {
                    PkCouponId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DiscountType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MinimumOrderValue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsageLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentUsageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.PkCouponId);
                });

            migrationBuilder.CreateTable(
                name: "RegisteredUsers",
                columns: table => new
                {
                    PkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteredUsers", x => x.PkRegisteredUserId);
                });

            migrationBuilder.CreateTable(
                name: "ShippingMethods",
                columns: table => new
                {
                    PkShippingMethodId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DeliveryDaysMin = table.Column<int>(type: "INTEGER", nullable: false),
                    DeliveryDaysMax = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingMethods", x => x.PkShippingMethodId);
                });

            migrationBuilder.CreateTable(
                name: "StaffMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    SentBy = table.Column<string>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    PkEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AvatarData = table.Column<byte[]>(type: "BLOB", nullable: true),
                    AvatarMimeType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
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
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
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
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
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
                name: "Products",
                columns: table => new
                {
                    PkProductId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NameNormalized = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    StockQuantity = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsTrending = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBestSeller = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastNotificationSent = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FkCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryPkCategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.PkProductId);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryPkCategoryId",
                        column: x => x.CategoryPkCategoryId,
                        principalTable: "Categories",
                        principalColumn: "PkCategoryId");
                });

            migrationBuilder.CreateTable(
                name: "ContactDetails",
                columns: table => new
                {
                    PkContactId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Street = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Province = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PostCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    FkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RegisteredUserPkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
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
                name: "StoreReviews",
                columns: table => new
                {
                    PkStoreReviewId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastEditedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Approved = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFlagged = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsVerifiedBuyer = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModeratorNote = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisteredUserPkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreReviews", x => x.PkStoreReviewId);
                    table.ForeignKey(
                        name: "FK_StoreReviews_RegisteredUsers_RegisteredUserPkRegisteredUserId",
                        column: x => x.RegisteredUserPkRegisteredUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId",
                        onDelete: ReferentialAction.Cascade);
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
                    Abandoned = table.Column<bool>(type: "INTEGER", nullable: false),
                    ActivityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ActivityDetail = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogs", x => x.PkLogId);
                    table.ForeignKey(
                        name: "FK_UserLogs_RegisteredUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WishLists",
                columns: table => new
                {
                    PkWishListId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FkUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishLists", x => x.PkWishListId);
                    table.ForeignKey(
                        name: "FK_WishLists_RegisteredUsers_FkUserId",
                        column: x => x.FkUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageReplies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplyText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RepliedBy = table.Column<string>(type: "TEXT", nullable: false),
                    RepliedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageReplies_StaffMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "StaffMessages",
                        principalColumn: "Id",
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
                name: "FuzzySuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NameNormalized = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Thumbnail = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuzzySuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuzzySuggestions_Products_PkProductId",
                        column: x => x.PkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductImage",
                columns: table => new
                {
                    PkProductImageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductImageURL = table.Column<string>(type: "TEXT", nullable: false),
                    FkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductPkProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImage", x => x.PkProductImageId);
                    table.ForeignKey(
                        name: "FK_ProductImage_Products_ProductPkProductId",
                        column: x => x.ProductPkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId",
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
                    Approved = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFlagged = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModeratorNote = table.Column<string>(type: "TEXT", nullable: false),
                    FkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductsPkProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    FkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisteredUserPkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    FkOrderItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    LastEditedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
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
                        name: "FK_ProductRatings_RegisteredUsers_RegisteredUserPkRegisteredUserId",
                        column: x => x.RegisteredUserPkRegisteredUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockNotifications",
                columns: table => new
                {
                    PkStockNotificationId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductPkProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    FkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisteredUserPkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NotificationSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsCancelled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockNotifications", x => x.PkStockNotificationId);
                    table.ForeignKey(
                        name: "FK_StockNotifications_Products_ProductPkProductId",
                        column: x => x.ProductPkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId");
                    table.ForeignKey(
                        name: "FK_StockNotifications_RegisteredUsers_RegisteredUserPkRegisteredUserId",
                        column: x => x.RegisteredUserPkRegisteredUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    PkOrderId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderStatus = table.Column<int>(type: "INTEGER", maxLength: 50, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeliveryStatus = table.Column<int>(type: "INTEGER", maxLength: 50, nullable: false),
                    ShippingMethodName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ShippingCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    CouponDiscount = table.Column<decimal>(type: "TEXT", nullable: false),
                    GuestAccessTokenHash = table.Column<string>(type: "TEXT", maxLength: 88, nullable: true),
                    FkRegisteredUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    FkShippingMethodId = table.Column<int>(type: "INTEGER", nullable: true),
                    ShippingMethodPkShippingMethodId = table.Column<int>(type: "INTEGER", nullable: true),
                    FkContactId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContactDetailPkContactId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.PkOrderId);
                    table.ForeignKey(
                        name: "FK_Orders_ContactDetails_ContactDetailPkContactId",
                        column: x => x.ContactDetailPkContactId,
                        principalTable: "ContactDetails",
                        principalColumn: "PkContactId");
                    table.ForeignKey(
                        name: "FK_Orders_RegisteredUsers_FkRegisteredUserId",
                        column: x => x.FkRegisteredUserId,
                        principalTable: "RegisteredUsers",
                        principalColumn: "PkRegisteredUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_ShippingMethods_ShippingMethodPkShippingMethodId",
                        column: x => x.ShippingMethodPkShippingMethodId,
                        principalTable: "ShippingMethods",
                        principalColumn: "PkShippingMethodId");
                });

            migrationBuilder.CreateTable(
                name: "WishListItems",
                columns: table => new
                {
                    PkWishListItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FkWishListId = table.Column<int>(type: "INTEGER", nullable: false),
                    FkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishListItems", x => x.PkWishListItemId);
                    table.ForeignKey(
                        name: "FK_WishListItems_Products_FkProductId",
                        column: x => x.FkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WishListItems_WishLists_FkWishListId",
                        column: x => x.FkWishListId,
                        principalTable: "WishLists",
                        principalColumn: "PkWishListId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderCoupons",
                columns: table => new
                {
                    PkOrderCouponId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FkOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    FkCouponId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CouponCodeUsed = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderCoupons", x => x.PkOrderCouponId);
                    table.ForeignKey(
                        name: "FK_OrderCoupons_Coupons_FkCouponId",
                        column: x => x.FkCouponId,
                        principalTable: "Coupons",
                        principalColumn: "PkCouponId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderCoupons_Orders_FkOrderId",
                        column: x => x.FkOrderId,
                        principalTable: "Orders",
                        principalColumn: "PkOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    PkOrderItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    FkOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderPkOrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    FkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductModelPkProductId = table.Column<int>(type: "INTEGER", nullable: true)
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
                        name: "FK_OrderItems_Products_FkProductId",
                        column: x => x.FkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductModelPkProductId",
                        column: x => x.ProductModelPkProductId,
                        principalTable: "Products",
                        principalColumn: "PkProductId");
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
                    PaymentOrderId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PaymentTransactionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PaymentProvider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    PaymentCapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PayerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PayerEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    VerificationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "TEXT", nullable: false),
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
                name: "IX_Coupons_Code_Unique",
                table: "Coupons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FuzzySuggestions_NameNormalized",
                table: "FuzzySuggestions",
                column: "NameNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_FuzzySuggestions_PkProductId",
                table: "FuzzySuggestions",
                column: "PkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReplies_MessageId",
                table: "MessageReplies",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderCoupons_FkCouponId",
                table: "OrderCoupons",
                column: "FkCouponId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderCoupons_FkOrderId",
                table: "OrderCoupons",
                column: "FkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_FkProductId",
                table: "OrderItems",
                column: "FkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderPkOrderId",
                table: "OrderItems",
                column: "OrderPkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductModelPkProductId",
                table: "OrderItems",
                column: "ProductModelPkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ContactDetailPkContactId",
                table: "Orders",
                column: "ContactDetailPkContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_FkRegisteredUserId",
                table: "Orders",
                column: "FkRegisteredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_GuestAccessTokenHash",
                table: "Orders",
                column: "GuestAccessTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ShippingMethodPkShippingMethodId",
                table: "Orders",
                column: "ShippingMethodPkShippingMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImage_ProductPkProductId",
                table: "ProductImage",
                column: "ProductPkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatings_ProductsPkProductId",
                table: "ProductRatings",
                column: "ProductsPkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatings_RegisteredUserPkRegisteredUserId",
                table: "ProductRatings",
                column: "RegisteredUserPkRegisteredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryPkCategoryId",
                table: "Products",
                column: "CategoryPkCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_NameNormalized",
                table: "Products",
                column: "NameNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_StockNotifications_ProductPkProductId",
                table: "StockNotifications",
                column: "ProductPkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockNotifications_RegisteredUserPkRegisteredUserId",
                table: "StockNotifications",
                column: "RegisteredUserPkRegisteredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreReviews_RegisteredUserPkRegisteredUserId",
                table: "StoreReviews",
                column: "RegisteredUserPkRegisteredUserId");

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
                name: "IX_Transactions_PaymentOrderId_Unique",
                table: "Transactions",
                column: "PaymentOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PaymentTransactionId_Unique",
                table: "Transactions",
                column: "PaymentTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLogs_UserId",
                table: "UserLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WishListItems_FkProductId",
                table: "WishListItems",
                column: "FkProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WishListItems_FkWishListId",
                table: "WishListItems",
                column: "FkWishListId");

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
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "CachedFuzzyKeys");

            migrationBuilder.DropTable(
                name: "Carts");

            migrationBuilder.DropTable(
                name: "FuzzySuggestions");

            migrationBuilder.DropTable(
                name: "MessageReplies");

            migrationBuilder.DropTable(
                name: "OrderCoupons");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "ProductImage");

            migrationBuilder.DropTable(
                name: "ProductRatings");

            migrationBuilder.DropTable(
                name: "StockNotifications");

            migrationBuilder.DropTable(
                name: "StoreReviews");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "UserLogs");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "WishListItems");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "StaffMessages");

            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "WishLists");

            migrationBuilder.DropTable(
                name: "ContactDetails");

            migrationBuilder.DropTable(
                name: "ShippingMethods");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "RegisteredUsers");
        }
    }
}
