using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancyAndAiCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ShipmentOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProductMasters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MasterDataFolders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CompanyTemplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantWorkspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    TaxCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AiCredits = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantWorkspaces", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantWorkspaces");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ShipmentOrders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProductMasters");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MasterDataFolders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CompanyTemplates");
        }
    }
}
