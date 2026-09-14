using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class BusinessHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS IX_MasterDataFolders_NormalizedSiblingName ON MasterDataFolders(IFNULL(ParentId, 0), lower(trim(Name))); ");
            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentUnlockAudits_ShipmentOrders_ShipmentOrderId",
                table: "ShipmentUnlockAudits");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseBatches_ShipmentOrderId",
                table: "WarehouseBatches");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "WarehouseBatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "CustomsSettlementPeriods",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "BusinessAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    ActorUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ActorRole = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PreviousStateJson = table.Column<string>(type: "TEXT", nullable: true),
                    NewStateJson = table.Column<string>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TraceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevokedJwts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Jti = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevokedJwts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ShipmentOrderId",
                table: "WarehouseBatches",
                column: "ShipmentOrderId",
                unique: true,
                filter: "\"ShipmentOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_CreatedAt",
                table: "BusinessAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_ResourceType_ResourceId_CreatedAt",
                table: "BusinessAuditLogs",
                columns: new[] { "ResourceType", "ResourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RevokedJwts_ExpiresAt",
                table: "RevokedJwts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RevokedJwts_Jti",
                table: "RevokedJwts",
                column: "Jti",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO BusinessAuditLogs
                    (ActorUserId, ActorUserName, ActorRole, Action, ResourceType, ResourceId,
                     PreviousStateJson, NewStateJson, Reason, TraceId, CreatedAt)
                SELECT UnlockedByUserId, UnlockedByUserName, 'Unknown', 'Shipment.Unlock',
                       'ShipmentOrder', CAST(ShipmentOrderId AS TEXT),
                       json_object('status', PreviousStatus, 'isLocked', 1),
                       json_object('status', 1, 'isLocked', 0), Reason, NULL, UnlockedAt
                FROM ShipmentUnlockAudits;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentUnlockAudits_ShipmentOrders_ShipmentOrderId",
                table: "ShipmentUnlockAudits",
                column: "ShipmentOrderId",
                principalTable: "ShipmentOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_MasterDataFolders_NormalizedSiblingName;");
            migrationBuilder.DropForeignKey(
                name: "FK_ShipmentUnlockAudits_ShipmentOrders_ShipmentOrderId",
                table: "ShipmentUnlockAudits");

            migrationBuilder.DropTable(
                name: "BusinessAuditLogs");

            migrationBuilder.DropTable(
                name: "RevokedJwts");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseBatches_ShipmentOrderId",
                table: "WarehouseBatches");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "WarehouseBatches");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CustomsSettlementPeriods");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ShipmentOrderId",
                table: "WarehouseBatches",
                column: "ShipmentOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShipmentUnlockAudits_ShipmentOrders_ShipmentOrderId",
                table: "ShipmentUnlockAudits",
                column: "ShipmentOrderId",
                principalTable: "ShipmentOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
