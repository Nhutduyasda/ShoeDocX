using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarehouseBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchName = table.Column<string>(type: "TEXT", nullable: false),
                    BatchNumber = table.Column<string>(type: "TEXT", nullable: false),
                    ExportDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ContractNote = table.Column<string>(type: "TEXT", nullable: false),
                    ContractFolderId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ShipmentOrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseBatches_MasterDataFolders_ContractFolderId",
                        column: x => x.ContractFolderId,
                        principalTable: "MasterDataFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseBatchItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WarehouseBatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    StyleCode = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPendingReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseBatchItems_WarehouseBatches_WarehouseBatchId",
                        column: x => x.WarehouseBatchId,
                        principalTable: "WarehouseBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_BatchNumber",
                table: "WarehouseBatches",
                column: "BatchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ContractFolderId",
                table: "WarehouseBatches",
                column: "ContractFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ExportDate",
                table: "WarehouseBatches",
                column: "ExportDate");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatchItems_WarehouseBatchId_StyleCode",
                table: "WarehouseBatchItems",
                columns: new[] { "WarehouseBatchId", "StyleCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarehouseBatchItems");

            migrationBuilder.DropTable(
                name: "WarehouseBatches");
        }
    }
}
