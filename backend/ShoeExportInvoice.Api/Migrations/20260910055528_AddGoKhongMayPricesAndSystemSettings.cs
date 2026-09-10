using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGoKhongMayPricesAndSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitPriceCMT_Go",
                table: "ProductMasters",
                type: "decimal(18, 4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPriceDAP_Go",
                table: "ProductMasters",
                type: "decimal(18, 4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "UnitPriceCMT_Go",
                table: "ProductMasters");

            migrationBuilder.DropColumn(
                name: "UnitPriceDAP_Go",
                table: "ProductMasters");
        }
    }
}
