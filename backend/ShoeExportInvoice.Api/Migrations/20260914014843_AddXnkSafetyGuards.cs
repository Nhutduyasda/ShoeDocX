using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddXnkSafetyGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomsSettlementItems_SettlementPeriodId_ProductCode",
                table: "CustomsSettlementItems");

            migrationBuilder.AddColumn<string>(
                name: "SizeBreakdownJson",
                table: "ShipmentOrderItems",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessType",
                table: "CustomsSettlementItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ShipmentUnlockAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShipmentOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UnlockedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    UnlockedByUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PreviousStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentUnlockAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentUnlockAudits_ShipmentOrders_ShipmentOrderId",
                        column: x => x.ShipmentOrderId,
                        principalTable: "ShipmentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomsSettlementItems_SettlementPeriodId_ProductCode_ProcessType",
                table: "CustomsSettlementItems",
                columns: new[] { "SettlementPeriodId", "ProductCode", "ProcessType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentUnlockAudits_ShipmentOrderId_UnlockedAt",
                table: "ShipmentUnlockAudits",
                columns: new[] { "ShipmentOrderId", "UnlockedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentUnlockAudits");

            migrationBuilder.DropIndex(
                name: "IX_CustomsSettlementItems_SettlementPeriodId_ProductCode_ProcessType",
                table: "CustomsSettlementItems");

            migrationBuilder.DropColumn(
                name: "SizeBreakdownJson",
                table: "ShipmentOrderItems");

            migrationBuilder.DropColumn(
                name: "ProcessType",
                table: "CustomsSettlementItems");

            migrationBuilder.CreateIndex(
                name: "IX_CustomsSettlementItems_SettlementPeriodId_ProductCode",
                table: "CustomsSettlementItems",
                columns: new[] { "SettlementPeriodId", "ProductCode" },
                unique: true);
        }
    }
}
