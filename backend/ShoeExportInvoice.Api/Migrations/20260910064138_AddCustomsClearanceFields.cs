using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomsClearanceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClearanceDate",
                table: "ShipmentOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomsAttachmentFileName",
                table: "ShipmentOrders",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomsChannel",
                table: "ShipmentOrders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomsDeclarationType",
                table: "ShipmentOrders",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomsGrossWeight",
                table: "ShipmentOrders",
                type: "decimal(18, 4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomsOffice",
                table: "ShipmentOrders",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomsPackageQty",
                table: "ShipmentOrders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomsTotalCmt",
                table: "ShipmentOrders",
                type: "decimal(18, 4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomsTotalDap",
                table: "ShipmentOrders",
                type: "decimal(18, 4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclarationNo",
                table: "ShipmentOrders",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ShipmentOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOrders_DeclarationNo",
                table: "ShipmentOrders",
                column: "DeclarationNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShipmentOrders_DeclarationNo",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "ClearanceDate",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "CustomsAttachmentFileName",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "CustomsChannel",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "CustomsDeclarationType",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "CustomsGrossWeight",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "CustomsOffice",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "CustomsPackageQty",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "CustomsTotalCmt",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "CustomsTotalDap",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "DeclarationNo",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ShipmentOrders");
        }
    }
}
