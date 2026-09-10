using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomsSettlementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomsSettlementPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    FromDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ToDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ContractNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TaxCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomsSettlementPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomsSettlementItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SettlementPeriodId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18, 2)", precision: 18, scale: 2, nullable: false),
                    InPeriodProduction = table.Column<decimal>(type: "decimal(18, 2)", precision: 18, scale: 2, nullable: false),
                    InPeriodExport = table.Column<decimal>(type: "decimal(18, 2)", precision: 18, scale: 2, nullable: false),
                    OtherExport = table.Column<decimal>(type: "decimal(18, 2)", precision: 18, scale: 2, nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18, 2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomsSettlementItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomsSettlementItems_CustomsSettlementPeriods_SettlementPeriodId",
                        column: x => x.SettlementPeriodId,
                        principalTable: "CustomsSettlementPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomsSettlementItems_SettlementPeriodId",
                table: "CustomsSettlementItems",
                column: "SettlementPeriodId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomsSettlementItems");

            migrationBuilder.DropTable(
                name: "CustomsSettlementPeriods");
        }
    }
}
