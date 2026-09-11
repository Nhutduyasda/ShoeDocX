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
