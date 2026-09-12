using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ShoeExportInvoice.Api.Migrations;

public partial class ContractScopeAndAuditGuards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Run through DbInitializer: it adopts schema fields added by legacy releases.
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS MasterDataFolders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, ParentId INTEGER NULL,
                CustomerName TEXT NULL, DeliveryAddress TEXT NULL, ContractNo TEXT NULL, PoSuffix TEXT NULL,
                DefaultPairsPerCarton INTEGER NOT NULL DEFAULT 12, DefaultUnit TEXT NOT NULL DEFAULT 'đôi',
                DisplayOrder INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NULL,
                FOREIGN KEY (ParentId) REFERENCES MasterDataFolders(Id) ON DELETE RESTRICT);

            ALTER TABLE ProductMasters ADD COLUMN FolderId INTEGER NULL REFERENCES MasterDataFolders(Id) ON DELETE RESTRICT;
            ALTER TABLE ShipmentOrders ADD COLUMN ContractFolderId INTEGER NULL REFERENCES MasterDataFolders(Id) ON DELETE RESTRICT;
            ALTER TABLE CustomsSettlementPeriods ADD COLUMN ContractFolderId INTEGER NULL REFERENCES MasterDataFolders(Id) ON DELETE RESTRICT;
            ALTER TABLE ShipmentOrderItems ADD COLUMN Description TEXT NOT NULL DEFAULT '';
            ALTER TABLE ShipmentOrderItems ADD COLUMN Unit TEXT NOT NULL DEFAULT 'đôi';
            ALTER TABLE ShipmentOrderItems ADD COLUMN PairPerCarton INTEGER NOT NULL DEFAULT 12;

            UPDATE ShipmentOrders SET ContractFolderId =
                (SELECT MIN(f.Id) FROM MasterDataFolders f
                 WHERE TRIM(f.ContractNo) = TRIM(ShipmentOrders.ContractNo)
                   AND TRIM(f.CustomerName) = TRIM(ShipmentOrders.CustomerName))
            WHERE (SELECT COUNT(*) FROM MasterDataFolders f
                 WHERE TRIM(f.ContractNo) = TRIM(ShipmentOrders.ContractNo)
                   AND TRIM(f.CustomerName) = TRIM(ShipmentOrders.CustomerName)) = 1;
            UPDATE CustomsSettlementPeriods SET ContractFolderId =
                (SELECT MIN(f.Id) FROM MasterDataFolders f WHERE TRIM(f.ContractNo) = TRIM(CustomsSettlementPeriods.ContractNo))
            WHERE (SELECT COUNT(*) FROM MasterDataFolders f WHERE TRIM(f.ContractNo) = TRIM(CustomsSettlementPeriods.ContractNo)) = 1;

            UPDATE ShipmentOrderItems SET
                Description = COALESCE((SELECT p.Description FROM ProductMasters p JOIN ShipmentOrders o ON o.Id = ShipmentOrderItems.ShipmentOrderId
                    WHERE p.StyleCode = ShipmentOrderItems.StyleCode COLLATE NOCASE AND p.FolderId IS o.ContractFolderId), ''),
                Unit = COALESCE((SELECT p.Unit FROM ProductMasters p JOIN ShipmentOrders o ON o.Id = ShipmentOrderItems.ShipmentOrderId
                    WHERE p.StyleCode = ShipmentOrderItems.StyleCode COLLATE NOCASE AND p.FolderId IS o.ContractFolderId), 'đôi'),
                PairPerCarton = COALESCE((SELECT p.PairPerCarton FROM ProductMasters p JOIN ShipmentOrders o ON o.Id = ShipmentOrderItems.ShipmentOrderId
                    WHERE p.StyleCode = ShipmentOrderItems.StyleCode COLLATE NOCASE AND p.FolderId IS o.ContractFolderId), 12);

            DROP INDEX IF EXISTS IX_ProductMasters_StyleCode;
            CREATE UNIQUE INDEX IX_ProductMasters_FolderId_StyleCode ON ProductMasters(FolderId, StyleCode COLLATE NOCASE);
            CREATE UNIQUE INDEX IX_ProductMasters_StyleCode ON ProductMasters(StyleCode COLLATE NOCASE) WHERE FolderId IS NULL;
            CREATE INDEX IX_ShipmentOrders_ContractFolderId_Status_CustomsDeclarationType_ClearanceDate ON ShipmentOrders(ContractFolderId, Status, CustomsDeclarationType, ClearanceDate);
            CREATE INDEX IX_CustomsSettlementPeriods_ContractFolderId_ToDate ON CustomsSettlementPeriods(ContractFolderId, ToDate);
            CREATE INDEX IF NOT EXISTS IX_MasterDataFolders_ParentId ON MasterDataFolders(ParentId);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException("Restore a verified database and attachment backup to roll back; contract-scoped duplicate codes cannot be safely collapsed.");
    }
}
