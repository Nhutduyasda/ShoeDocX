using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShoeExportInvoice.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context, ILogger logger)
    {
        const string baseline = "20260910074757_AddCustomsSettlementTables";
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        if (!applied.Contains(baseline))
            await context.GetService<IMigrator>().MigrateAsync(baseline);
        // Earlier releases added these fields at startup without a migration.
        // Adopt those fields without dropping or overwriting existing values.
        await context.Database.OpenConnectionAsync();
        var connection = context.Database.GetDbConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS MasterDataFolders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, ParentId INTEGER NULL,
                    CustomerName TEXT NULL, DeliveryAddress TEXT NULL, ContractNo TEXT NULL, PoSuffix TEXT NULL,
                    DefaultPairsPerCarton INTEGER NOT NULL DEFAULT 12, DefaultUnit TEXT NOT NULL DEFAULT 'đôi',
                    DisplayOrder INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NULL,
                    FOREIGN KEY (ParentId) REFERENCES MasterDataFolders(Id) ON DELETE RESTRICT);
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        async Task EnsureColumn(string table, string column, string definition)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table});";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = await cmd.ExecuteReaderAsync())
                while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
            if (!columns.Contains(column))
            {
                cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
                await cmd.ExecuteNonQueryAsync();
            }
        }
        await EnsureColumn("ShipmentOrders", "IsLocked", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("ShipmentOrders", "CustomsAttachmentFilePath", "TEXT NULL");
        await EnsureColumn("ProductMasters", "FolderId", "INTEGER NULL REFERENCES MasterDataFolders(Id) ON DELETE RESTRICT");
        await EnsureColumn("CustomsSettlementPeriods", "CustomsOffice", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumn("CustomsSettlementPeriods", "Status", "TEXT NOT NULL DEFAULT 'Draft'");
        await EnsureColumn("CustomsSettlementItems", "HsCode", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumn("MasterDataFolders", "DeliveryAddress", "TEXT NULL");
        await EnsureColumn("MasterDataFolders", "PoSuffix", "TEXT NULL");
        await context.Database.CloseConnectionAsync();
        await context.Database.MigrateAsync();
        var unresolved = await context.ShipmentOrders.CountAsync(o => o.ContractFolderId == null);
        if (unresolved > 0) logger.LogWarning("{Count} historical orders have no unambiguous contract folder; review before editing.", unresolved);
    }

    public static async Task SeedUsersAsync(Microsoft.AspNetCore.Identity.UserManager<ShoeExportInvoice.Api.Models.Entities.ApplicationUser> userManager, ILogger logger)
    {
        var defaultUsers = new List<(string Username, string Password, string FullName, ShoeExportInvoice.Api.Models.Entities.Department Dept)>
        {
            ("admin", "@Admin123", "Ban Giám Đốc (Quản Trị)", ShoeExportInvoice.Api.Models.Entities.Department.Admin),
            ("xnk", "@Xnk123", "Nhân Viên Xuất Nhập Khẩu", ShoeExportInvoice.Api.Models.Entities.Department.Xnk),
            ("kho", "@Kho123", "Thủ Kho Thành Phẩm", ShoeExportInvoice.Api.Models.Entities.Department.Kho),
            ("ketoan", "@KeToan123", "Kế Toán Doanh Thu CMT", ShoeExportInvoice.Api.Models.Entities.Department.KeToan)
        };

        foreach (var (username, password, fullName, dept) in defaultUsers)
        {
            var existing = await userManager.FindByNameAsync(username);
            if (existing == null)
            {
                var user = new ShoeExportInvoice.Api.Models.Entities.ApplicationUser
                {
                    UserName = username,
                    FullName = fullName,
                    Department = dept,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    logger.LogInformation("Đã khởi tạo tài khoản mặc định: {Username} ({FullName})", username, fullName);
                }
                else
                {
                    logger.LogWarning("Không thể tạo tài khoản mặc định {Username}: {Errors}", username, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
