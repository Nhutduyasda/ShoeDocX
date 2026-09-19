using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBomProcessType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BomMasters_TenantId_StyleCode_Version",
                table: "BomMasters");

            migrationBuilder.AddColumn<int>(
                name: "ProcessType",
                table: "BomMasters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_BomMasters_TenantId_StyleCode_ProcessType_Version",
                table: "BomMasters",
                columns: new[] { "TenantId", "StyleCode", "ProcessType", "Version" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BomMasters_ProcessType",
                table: "BomMasters",
                sql: "ProcessType IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BomMasters_TenantId_StyleCode_ProcessType_Version",
                table: "BomMasters");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BomMasters_ProcessType",
                table: "BomMasters");

            migrationBuilder.DropColumn(
                name: "ProcessType",
                table: "BomMasters");

            migrationBuilder.CreateIndex(
                name: "IX_BomMasters_TenantId_StyleCode_Version",
                table: "BomMasters",
                columns: new[] { "TenantId", "StyleCode", "Version" },
                unique: true);
        }
    }
}
