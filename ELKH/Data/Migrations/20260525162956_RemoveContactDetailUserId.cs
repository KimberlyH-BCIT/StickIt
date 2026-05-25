using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELKH.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContactDetailUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ContactDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ContactDetails",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
