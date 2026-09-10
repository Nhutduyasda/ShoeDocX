using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StyleCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    UnitPriceCMT = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceDAP = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false),
                    HsCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    PairPerCarton = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PoSuffix = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContractNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DeliveryTerms = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PaymentTerms = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShipmentOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    StyleCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FullItemCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessType = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPriceCMT = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceDAP = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentOrderItems_ShipmentOrders_ShipmentOrderId",
                        column: x => x.ShipmentOrderId,
                        principalTable: "ShipmentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMasters_StyleCode",
                table: "ProductMasters",
                column: "StyleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOrderItems_ShipmentOrderId",
                table: "ShipmentOrderItems",
                column: "ShipmentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOrders_InvoiceNo",
                table: "ShipmentOrders",
                column: "InvoiceNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductMasters");

            migrationBuilder.DropTable(
                name: "ShipmentOrderItems");

            migrationBuilder.DropTable(
                name: "ShipmentOrders");
        }
    }
}
