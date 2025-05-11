using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace market_api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    DeliveryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    DeliveryMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeliveryType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecipientFullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CityRef = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    CityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WarehouseRef = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    WarehouseAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeliveryAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TrackingNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeliveryCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveryStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeliveryData = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.DeliveryId);
                    table.ForeignKey(
                        name: "FK_Deliveries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_OrderId",
                table: "Deliveries",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliveries");
        }
    }
}
