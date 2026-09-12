using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class ProductionIntegrityGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Historical rows created before ProcessType became a 1-based enum used 0
            // for the standard process. Normalize that legacy representation before
            // SQLite rebuilds the table to add the CHECK constraint.
            migrationBuilder.Sql(
                "UPDATE ShipmentOrderItems SET ProcessType = 1 WHERE ProcessType = 0;");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentOrders_DeclarationNo",
                table: "ShipmentOrders");

            migrationBuilder.DropIndex(
                name: "IX_CustomsSettlementItems_SettlementPeriodId_ProductCode",
                table: "CustomsSettlementItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WarehouseBatchItems_Quantity_Positive",
                table: "WarehouseBatchItems",
                sql: "Quantity > 0");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ShipmentOrderId",
                table: "WarehouseBatches",
                column: "ShipmentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOrders_DeclarationNo",
                table: "ShipmentOrders",
                column: "DeclarationNo",
                unique: true,
                filter: "\"DeclarationNo\" IS NOT NULL AND \"DeclarationNo\" <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShipmentOrderItems_PairPerCarton",
                table: "ShipmentOrderItems",
                sql: "PairPerCarton BETWEEN 1 AND 1000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShipmentOrderItems_Prices_NonNegative",
                table: "ShipmentOrderItems",
                sql: "UnitPriceCMT >= 0 AND UnitPriceDAP >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShipmentOrderItems_ProcessType",
                table: "ShipmentOrderItems",
                sql: "ProcessType IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShipmentOrderItems_Quantity_Positive",
                table: "ShipmentOrderItems",
                sql: "Quantity > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductMasters_PairPerCarton",
                table: "ProductMasters",
                sql: "PairPerCarton BETWEEN 1 AND 1000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductMasters_Prices_NonNegative",
                table: "ProductMasters",
                sql: "UnitPriceCMT >= 0 AND UnitPriceDAP >= 0 AND (UnitPriceCMT_Go IS NULL OR UnitPriceCMT_Go >= 0) AND (UnitPriceDAP_Go IS NULL OR UnitPriceDAP_Go >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomsSettlementPeriods_Status",
                table: "CustomsSettlementPeriods",
                sql: "Status IN ('Draft', 'Finalized')");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsSettlementItems_SettlementPeriodId_ProductCode",
                table: "CustomsSettlementItems",
                columns: new[] { "SettlementPeriodId", "ProductCode" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomsSettlementItems_Inputs_NonNegative",
                table: "CustomsSettlementItems",
                sql: "OpeningBalance >= 0 AND InPeriodProduction >= 0 AND InPeriodExport >= 0 AND OtherExport >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseBatches_ShipmentOrders_ShipmentOrderId",
                table: "WarehouseBatches",
                column: "ShipmentOrderId",
                principalTable: "ShipmentOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseBatches_ShipmentOrders_ShipmentOrderId",
                table: "WarehouseBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WarehouseBatchItems_Quantity_Positive",
                table: "WarehouseBatchItems");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseBatches_ShipmentOrderId",
                table: "WarehouseBatches");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentOrders_DeclarationNo",
                table: "ShipmentOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShipmentOrderItems_PairPerCarton",
                table: "ShipmentOrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShipmentOrderItems_Prices_NonNegative",
                table: "ShipmentOrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShipmentOrderItems_ProcessType",
                table: "ShipmentOrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShipmentOrderItems_Quantity_Positive",
                table: "ShipmentOrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductMasters_PairPerCarton",
                table: "ProductMasters");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductMasters_Prices_NonNegative",
                table: "ProductMasters");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomsSettlementPeriods_Status",
                table: "CustomsSettlementPeriods");

            migrationBuilder.DropIndex(
                name: "IX_CustomsSettlementItems_SettlementPeriodId_ProductCode",
                table: "CustomsSettlementItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomsSettlementItems_Inputs_NonNegative",
                table: "CustomsSettlementItems");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOrders_DeclarationNo",
                table: "ShipmentOrders",
                column: "DeclarationNo");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsSettlementItems_SettlementPeriodId_ProductCode",
                table: "CustomsSettlementItems",
                columns: new[] { "SettlementPeriodId", "ProductCode" });
        }
    }
}
