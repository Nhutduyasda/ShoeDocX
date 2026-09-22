using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.MigrationsSqlServer
{
    /// <inheritdoc />
    public partial class AddShipmentDispatchTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShipmentSourceBatches",
                columns: table => new
                {
                    ShipmentOrderId = table.Column<int>(type: "int", nullable: false),
                    SourceBatchId = table.Column<int>(type: "int", nullable: false),
                    RelationType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentSourceBatches", x => new { x.ShipmentOrderId, x.SourceBatchId });
                    table.ForeignKey(
                        name: "FK_ShipmentSourceBatches_ShipmentOrders_ShipmentOrderId",
                        column: x => x.ShipmentOrderId,
                        principalTable: "ShipmentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShipmentSourceBatches_WarehouseBatches_SourceBatchId",
                        column: x => x.SourceBatchId,
                        principalTable: "WarehouseBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentSourceBatches_SourceBatchId_RelationType",
                table: "ShipmentSourceBatches",
                columns: new[] { "SourceBatchId", "RelationType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentSourceBatches");
        }
    }
}
