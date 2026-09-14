using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerDocumentPatterns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentSequenceNumber",
                table: "MasterDataFolders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "FileNamePattern",
                table: "MasterDataFolders",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                defaultValue: "KM3-26-DH{SEQ}.xlsx");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNoPattern",
                table: "MasterDataFolders",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                defaultValue: "KMHD-NEW2026-{SEQ:4}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentSequenceNumber",
                table: "MasterDataFolders");

            migrationBuilder.DropColumn(
                name: "FileNamePattern",
                table: "MasterDataFolders");

            migrationBuilder.DropColumn(
                name: "InvoiceNoPattern",
                table: "MasterDataFolders");
        }
    }
}
