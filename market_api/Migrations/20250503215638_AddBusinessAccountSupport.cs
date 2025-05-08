using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace market_api.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessAccountSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyAvatar",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyDescription",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBusiness",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BusinessOwnerId",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessOwnerId",
                table: "Products",
                column: "BusinessOwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Users_BusinessOwnerId",
                table: "Products",
                column: "BusinessOwnerId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Users_BusinessOwnerId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_BusinessOwnerId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CompanyAvatar",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompanyDescription",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsBusiness",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BusinessOwnerId",
                table: "Products");
        }
    }
}
