using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBomAndMaterialCalculator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BomMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StyleCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    Version = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MaterialCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    MaterialName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    MaterialType = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentStock = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                    table.CheckConstraint("CK_Materials_CurrentStock_NonNegative", "CurrentStock >= 0");
                    table.CheckConstraint("CK_Materials_MaterialType", "MaterialType IN (0, 1)");
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OrderNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    StyleCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    TotalQuantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrders", x => x.Id);
                    table.CheckConstraint("CK_ProductionOrders_TotalQuantity_NonNegative", "TotalQuantity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "BomItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BomMasterId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false),
                    NetConsumption = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false),
                    WastageRatePercent = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomItems", x => x.Id);
                    table.CheckConstraint("CK_BomItems_NetConsumption_NonNegative", "NetConsumption >= 0");
                    table.CheckConstraint("CK_BomItems_WastageRate", "WastageRatePercent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_BomItems_BomMasters_BomMasterId",
                        column: x => x.BomMasterId,
                        principalTable: "BomMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BomItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderSizeRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSizeRuns", x => x.Id);
                    table.CheckConstraint("CK_OrderSizeRuns_Quantity_Positive", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_OrderSizeRuns_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BomItems_BomMasterId_MaterialId",
                table: "BomItems",
                columns: new[] { "BomMasterId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BomItems_MaterialId",
                table: "BomItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_BomMasters_TenantId_StyleCode_CreatedAt",
                table: "BomMasters",
                columns: new[] { "TenantId", "StyleCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BomMasters_TenantId_StyleCode_Version",
                table: "BomMasters",
                columns: new[] { "TenantId", "StyleCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_TenantId_MaterialCode",
                table: "Materials",
                columns: new[] { "TenantId", "MaterialCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderSizeRuns_ProductionOrderId_SizeName",
                table: "OrderSizeRuns",
                columns: new[] { "ProductionOrderId", "SizeName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_TenantId_OrderNo",
                table: "ProductionOrders",
                columns: new[] { "TenantId", "OrderNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_TenantId_StyleCode",
                table: "ProductionOrders",
                columns: new[] { "TenantId", "StyleCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BomItems");

            migrationBuilder.DropTable(
                name: "OrderSizeRuns");

            migrationBuilder.DropTable(
                name: "BomMasters");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "ProductionOrders");
        }
    }
}
