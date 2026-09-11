using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            // Apply any pending migrations or create database
            await context.Database.MigrateAsync();

            // Ensure columns exist on SQLite table ShipmentOrders
            try
            {
                using var conn = context.Database.GetDbConnection();
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(ShipmentOrders);";
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        columns.Add(reader.GetString(1));
                    }
                }

                if (!columns.Contains("IsLocked"))
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE ShipmentOrders ADD COLUMN IsLocked INTEGER NOT NULL DEFAULT 0;";
                    await alterCmd.ExecuteNonQueryAsync();
                    logger.LogInformation("Added column IsLocked to ShipmentOrders table.");
                }

                if (!columns.Contains("CustomsAttachmentFilePath"))
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE ShipmentOrders ADD COLUMN CustomsAttachmentFilePath TEXT NULL;";
                    await alterCmd.ExecuteNonQueryAsync();
                    logger.LogInformation("Added column CustomsAttachmentFilePath to ShipmentOrders table.");
                }

                // Check CustomsSettlementPeriods columns
                using var cmdPeriod = conn.CreateCommand();
                cmdPeriod.CommandText = "PRAGMA table_info(CustomsSettlementPeriods);";
                var periodCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await cmdPeriod.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        periodCols.Add(reader.GetString(1));
                    }
                }

                if (!periodCols.Contains("CustomsOffice"))
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE CustomsSettlementPeriods ADD COLUMN CustomsOffice TEXT NOT NULL DEFAULT 'Chi cục Hải quan Quản lý Hàng gia công';";
                    await alterCmd.ExecuteNonQueryAsync();
                    logger.LogInformation("Added column CustomsOffice to CustomsSettlementPeriods table.");
                }

                if (!periodCols.Contains("Status"))
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE CustomsSettlementPeriods ADD COLUMN Status TEXT NOT NULL DEFAULT 'Draft';";
                    await alterCmd.ExecuteNonQueryAsync();
                    logger.LogInformation("Added column Status to CustomsSettlementPeriods table.");
                }

                // Check CustomsSettlementItems columns
                using var cmdItem = conn.CreateCommand();
                cmdItem.CommandText = "PRAGMA table_info(CustomsSettlementItems);";
                var itemCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await cmdItem.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        itemCols.Add(reader.GetString(1));
                    }
                }

                if (!itemCols.Contains("HsCode"))
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE CustomsSettlementItems ADD COLUMN HsCode TEXT NOT NULL DEFAULT '64041990';";
                    await alterCmd.ExecuteNonQueryAsync();
                    logger.LogInformation("Added column HsCode to CustomsSettlementItems table.");
                }

                // Ensure MasterDataFolders table exists
                using var createFolderCmd = conn.CreateCommand();
                createFolderCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS MasterDataFolders (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        ParentId INTEGER NULL,
                        CustomerName TEXT NULL,
                        DeliveryAddress TEXT NULL,
                        ContractNo TEXT NULL,
                        PoSuffix TEXT NULL,
                        DefaultPairsPerCarton INTEGER NOT NULL DEFAULT 12,
                        DefaultUnit TEXT NOT NULL DEFAULT 'đôi',
                        DisplayOrder INTEGER NOT NULL DEFAULT 0,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NULL,
                        FOREIGN KEY (ParentId) REFERENCES MasterDataFolders(Id) ON DELETE RESTRICT
                    );";
                await createFolderCmd.ExecuteNonQueryAsync();

                // Ensure DeliveryAddress and PoSuffix columns exist in MasterDataFolders
                cmd.CommandText = "PRAGMA table_info(MasterDataFolders);";
                var folderCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        folderCols.Add(reader.GetString(1));
                    }
                }

                if (!folderCols.Contains("DeliveryAddress"))
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE MasterDataFolders ADD COLUMN DeliveryAddress TEXT NULL;";
                    await alterCmd.ExecuteNonQueryAsync();
                    logger.LogInformation("Added column DeliveryAddress to MasterDataFolders table.");
                }

                if (!folderCols.Contains("PoSuffix"))
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE MasterDataFolders ADD COLUMN PoSuffix TEXT NULL;";
                    await alterCmd.ExecuteNonQueryAsync();
                    logger.LogInformation("Added column PoSuffix to MasterDataFolders table.");
                }

                // Ensure FolderId column exists in ProductMasters
                cmd.CommandText = "PRAGMA table_info(ProductMasters);";
                var pmCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        pmCols.Add(reader.GetString(1));
                    }
                }

                if (!pmCols.Contains("FolderId"))
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE ProductMasters ADD COLUMN FolderId INTEGER NULL REFERENCES MasterDataFolders(Id);";
                    await alterCmd.ExecuteNonQueryAsync();
                    logger.LogInformation("Added column FolderId to ProductMasters table.");
                }
            }
            catch (Exception colEx)
            {
                logger.LogWarning(colEx, "Warning verifying table columns.");
            }

            // Đảm bảo luôn có ít nhất 1 thư mục mặc định Kingmaker III
            MasterDataFolder? defaultFolder = await context.MasterDataFolders.FirstOrDefaultAsync(f => f.ParentId == null);
            if (defaultFolder == null)
            {
                defaultFolder = new MasterDataFolder
                {
                    Name = "Kingmaker III",
                    CustomerName = "CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR",
                    DeliveryAddress = "SỐ 1, ĐƯỜNG 4A, KCN VIỆT NAM SINGAPORE, XÃ THỌ PHONG, TỈNH QUẢNG NGÃI, VIỆT NAM",
                    ContractNo = "KM-HANEW/01-2025",
                    PoSuffix = "(KM3.PO5.26)",
                    DefaultPairsPerCarton = 12,
                    DefaultUnit = "đôi",
                    DisplayOrder = 0,
                    CreatedAt = DateTime.UtcNow
                };
                context.MasterDataFolders.Add(defaultFolder);
                await context.SaveChangesAsync();
                logger.LogInformation("Created default MasterDataFolder: Kingmaker III (12 pairs/ctn).");
            }
            else
            {
                bool modified = false;
                if (string.IsNullOrWhiteSpace(defaultFolder.CustomerName) || defaultFolder.CustomerName == "Kingmaker III")
                {
                    defaultFolder.CustomerName = "CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR";
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(defaultFolder.DeliveryAddress))
                {
                    defaultFolder.DeliveryAddress = "SỐ 1, ĐƯỜNG 4A, KCN VIỆT NAM SINGAPORE, XÃ THỌ PHONG, TỈNH QUẢNG NGÃI, VIỆT NAM";
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(defaultFolder.ContractNo) || defaultFolder.ContractNo == "KM3-2026")
                {
                    defaultFolder.ContractNo = "KM-HANEW/01-2025";
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(defaultFolder.PoSuffix))
                {
                    defaultFolder.PoSuffix = "(KM3.PO5.26)";
                    modified = true;
                }
                if (modified)
                {
                    await context.SaveChangesAsync();
                }
            }

            // Đảm bảo có thư mục Đối tác mẫu thứ 2 (24 đôi/thùng) để chuyển đổi đa đối tác
            var partnerB = await context.MasterDataFolders.FirstOrDefaultAsync(f => f.Name == "Đối tác B - ABC Footwear");
            if (partnerB == null)
            {
                partnerB = new MasterDataFolder
                {
                    Name = "Đối tác B - ABC Footwear",
                    CustomerName = "CÔNG TY TNHH ABC FOOTWEAR (VIỆT NAM)",
                    DeliveryAddress = "LÔ B, KCN VSIP, TỈNH BÌNH DƯƠNG, VIỆT NAM",
                    ContractNo = "ABC-2026/01",
                    PoSuffix = "(ABC.PO1.26)",
                    DefaultPairsPerCarton = 24,
                    DefaultUnit = "đôi",
                    DisplayOrder = 1,
                    CreatedAt = DateTime.UtcNow
                };
                context.MasterDataFolders.Add(partnerB);
                await context.SaveChangesAsync();
                logger.LogInformation("Created sample MasterDataFolder: Đối tác B - ABC Footwear (24 pairs/ctn).");
            }

            // Đảm bảo có các mã mẫu thuộc Đối tác B (như YL3564-100, BM5879-464) để hỗ trợ tính năng chuyển đổi đối tác thông minh
            var sampleB1 = await context.ProductMasters.FirstOrDefaultAsync(p => p.StyleCode == "YL3564-100");
            if (sampleB1 == null)
            {
                context.ProductMasters.Add(new ProductMaster
                {
                    StyleCode = "YL3564-100",
                    Description = "Giày thể thao ABC Running Pro",
                    UnitPriceCMT = 3.2000m,
                    UnitPriceDAP = 25.0000m,
                    UnitPriceCMT_Go = 2.9000m,
                    UnitPriceDAP_Go = 23.5000m,
                    HsCode = "64041990",
                    Unit = "đôi",
                    PairPerCarton = 24,
                    FolderId = partnerB.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (sampleB1.FolderId != partnerB.Id)
            {
                sampleB1.FolderId = partnerB.Id;
                sampleB1.PairPerCarton = 24;
            }

            var sampleB2 = await context.ProductMasters.FirstOrDefaultAsync(p => p.StyleCode == "BM5879-464");
            if (sampleB2 == null)
            {
                context.ProductMasters.Add(new ProductMaster
                {
                    StyleCode = "BM5879-464",
                    Description = "Giày slip-on ABC Comfort Walker",
                    UnitPriceCMT = 2.9000m,
                    UnitPriceDAP = 22.5000m,
                    UnitPriceCMT_Go = 2.6000m,
                    UnitPriceDAP_Go = 20.8000m,
                    HsCode = "64041990",
                    Unit = "đôi",
                    PairPerCarton = 24,
                    FolderId = partnerB.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (sampleB2.FolderId != partnerB.Id)
            {
                sampleB2.FolderId = partnerB.Id;
                sampleB2.PairPerCarton = 24;
            }
            await context.SaveChangesAsync();

            // Gán các sản phẩm cũ chưa có FolderId vào thư mục mặc định
            var unassignedProducts = await context.ProductMasters.Where(p => p.FolderId == null).ToListAsync();
            if (unassignedProducts.Count > 0)
            {
                foreach (var p in unassignedProducts)
                {
                    p.FolderId = defaultFolder.Id;
                }
                await context.SaveChangesAsync();
                logger.LogInformation("Assigned {Count} legacy products to default folder '{FolderName}'.", unassignedProducts.Count, defaultFolder.Name);
            }

            // Chỉ nạp dữ liệu mẫu khi có cấu hình biến môi trường SEED_SAMPLE_DATA=true (mặc định để trống để người dùng nạp dữ liệu thật)
            var shouldSeed = Environment.GetEnvironmentVariable("SEED_SAMPLE_DATA")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            if (shouldSeed && !await context.ProductMasters.AnyAsync())
            {
                logger.LogInformation("Seeding initial ProductMaster data...");

                var seedProducts = new List<ProductMaster>
                {
                    new()
                    {
                        StyleCode = "42072-030",
                        Description = "Giày thể thao nữ buộc dây đế cao su (Women's Athletic Shoes Rubber Sole)",
                        UnitPriceCMT = 2.4500m,
                        UnitPriceDAP = 18.5000m,
                        HsCode = "64041990",
                        Unit = "đôi",
                        PairPerCarton = 12,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        StyleCode = "45428-2LX",
                        Description = "Giày chạy bộ nam cổ thấp vải dệt (Men's Low-top Mesh Running Shoes)",
                        UnitPriceCMT = 2.8000m,
                        UnitPriceDAP = 21.0000m,
                        HsCode = "64041990",
                        Unit = "đôi",
                        PairPerCarton = 12,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        StyleCode = "51200-1BK",
                        Description = "Giày búp bê nữ có quai dán (Women's Mary Jane Casual Shoes)",
                        UnitPriceCMT = 1.9500m,
                        UnitPriceDAP = 15.2000m,
                        HsCode = "64041990",
                        Unit = "đôi",
                        PairPerCarton = 12,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        StyleCode = "33109-08A",
                        Description = "Giày lười nam da tổng hợp (Men's Slip-on PU Leather Shoes)",
                        UnitPriceCMT = 3.1000m,
                        UnitPriceDAP = 24.5000m,
                        HsCode = "64029990",
                        Unit = "đôi",
                        PairPerCarton = 12,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        StyleCode = "68001-9TR",
                        Description = "Giày leo núi cổ lửng chống nước (Outdoor Hiking Mid-cut Waterproof Shoes)",
                        UnitPriceCMT = 4.2000m,
                        UnitPriceDAP = 32.0000m,
                        HsCode = "64039190",
                        Unit = "đôi",
                        PairPerCarton = 10,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await context.ProductMasters.AddRangeAsync(seedProducts);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} initial products successfully.", seedProducts.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi xảy ra trong quá trình khởi tạo dữ liệu ban đầu.");
            throw;
        }
    }
}
