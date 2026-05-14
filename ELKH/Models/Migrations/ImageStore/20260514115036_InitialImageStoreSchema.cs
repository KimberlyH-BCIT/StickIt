using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELKH.Models.Migrations.ImageStore
{
    /// <inheritdoc />
    public partial class InitialImageStoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Image",
                columns: table => new
                {
                    imageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    fileName = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    fileType = table.Column<string>(type: "TEXT", nullable: false),
                    imageData = table.Column<byte[]>(type: "BLOB", nullable: false),
                    FkProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductImageURL = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Image", x => x.imageId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Image");
        }
    }
}
