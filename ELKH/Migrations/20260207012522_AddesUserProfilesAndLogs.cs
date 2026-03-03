using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELKH.Migrations
{
    /// <inheritdoc />
    public partial class AddesUserProfilesAndLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoriePkCategoryId",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "CategoriePkCategoryId",
                table: "Products",
                newName: "CategoryPkCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_CategoriePkCategoryId",
                table: "Products",
                newName: "IX_Products_CategoryPkCategoryId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryPkCategoryId",
                table: "Products",
                column: "CategoryPkCategoryId",
                principalTable: "Categories",
                principalColumn: "PkCategoryId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryPkCategoryId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "UserLogs");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.RenameColumn(
                name: "CategoryPkCategoryId",
                table: "Products",
                newName: "CategoriePkCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_CategoryPkCategoryId",
                table: "Products",
                newName: "IX_Products_CategoriePkCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoriePkCategoryId",
                table: "Products",
                column: "CategoriePkCategoryId",
                principalTable: "Categories",
                principalColumn: "PkCategoryId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
