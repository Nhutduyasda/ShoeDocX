using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeExportInvoice.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialPlanningAndIssuance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcessType",
                table: "ProductionOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ProductionOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MaterialRequirementPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    BomMasterId = table.Column<int>(type: "INTEGER", nullable: false),
                    BomVersion = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialRequirementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialRequirementPlans_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaterialRequirementPlanItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MaterialRequirementPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    MaterialType = table.Column<int>(type: "INTEGER", nullable: false),
                    NetConsumption = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false),
                    WastageRatePercent = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false),
                    RequiredQuantity = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false),
                    StockAtCalculation = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialRequirementPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialRequirementPlanItems_MaterialRequirementPlans_MaterialRequirementPlanId",
                        column: x => x.MaterialRequirementPlanId,
                        principalTable: "MaterialRequirementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaterialRequirementPlanItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialRequirementPlanSizes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MaterialRequirementPlanItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    OrderQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiredQuantity = table.Column<decimal>(type: "decimal(18, 4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialRequirementPlanSizes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialRequirementPlanSizes_MaterialRequirementPlanItems_MaterialRequirementPlanItemId",
                        column: x => x.MaterialRequirementPlanItemId,
                        principalTable: "MaterialRequirementPlanItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductionOrders_ProcessType",
                table: "ProductionOrders",
                sql: "ProcessType IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductionOrders_Status",
                table: "ProductionOrders",
                sql: "Status IN (0, 1, 2, 3, 4)");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequirementPlanItems_MaterialId",
                table: "MaterialRequirementPlanItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequirementPlanItems_MaterialRequirementPlanId_MaterialId",
                table: "MaterialRequirementPlanItems",
                columns: new[] { "MaterialRequirementPlanId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequirementPlans_ProductionOrderId",
                table: "MaterialRequirementPlans",
                column: "ProductionOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequirementPlanSizes_MaterialRequirementPlanItemId_SizeName",
                table: "MaterialRequirementPlanSizes",
                columns: new[] { "MaterialRequirementPlanItemId", "SizeName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialRequirementPlanSizes");

            migrationBuilder.DropTable(
                name: "MaterialRequirementPlanItems");

            migrationBuilder.DropTable(
                name: "MaterialRequirementPlans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductionOrders_ProcessType",
                table: "ProductionOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductionOrders_Status",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ProcessType",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProductionOrders");
        }
    }
}
