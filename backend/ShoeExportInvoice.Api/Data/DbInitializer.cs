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
        await EnsureColumn("CustomsSettlementPeriods", "CustomsOffice", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumn("CustomsSettlementPeriods", "Status", "TEXT NOT NULL DEFAULT 'Draft'");
        await EnsureColumn("CustomsSettlementItems", "HsCode", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumn("MasterDataFolders", "DeliveryAddress", "TEXT NULL");
        await EnsureColumn("MasterDataFolders", "PoSuffix", "TEXT NULL");
        using (var duplicateCmd = connection.CreateCommand())
        {
            duplicateCmd.CommandText = """
                SELECT group_concat(Id, ',') FROM MasterDataFolders
                WHERE (IFNULL(ParentId, 0), lower(trim(Name))) IN (
                    SELECT IFNULL(ParentId, 0), lower(trim(Name)) FROM MasterDataFolders
                    GROUP BY IFNULL(ParentId, 0), lower(trim(Name)) HAVING COUNT(*) > 1);
                """;
            var duplicateIds = await duplicateCmd.ExecuteScalarAsync() as string;
            if (!string.IsNullOrWhiteSpace(duplicateIds))
                throw new InvalidOperationException($"Duplicate sibling folders must be resolved before migration. IDs: {duplicateIds}");
        }
        await context.Database.CloseConnectionAsync();
        await context.Database.MigrateAsync();
        var unresolved = await context.ShipmentOrders.CountAsync(o => o.ContractFolderId == null);
        if (unresolved > 0) logger.LogWarning("{Count} historical orders have no unambiguous contract folder; review before editing.", unresolved);
    }

    public static async Task SeedBootstrapAdminAsync(
        Microsoft.AspNetCore.Identity.UserManager<ShoeExportInvoice.Api.Models.Entities.ApplicationUser> userManager,
        ILogger logger,
        string? username,
        string? password,
        string? fullName)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("BootstrapAdmin is enabled but Username or Password is missing.");

        var existing = await userManager.FindByNameAsync(username.Trim());
        if (existing == null)
        {
            var user = new ShoeExportInvoice.Api.Models.Entities.ApplicationUser
            {
                UserName = username.Trim(),
                FullName = string.IsNullOrWhiteSpace(fullName) ? "System Administrator" : fullName.Trim(),
                Department = ShoeExportInvoice.Api.Models.Entities.Department.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Cannot create bootstrap administrator: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            logger.LogWarning("Bootstrap administrator {Username} was created. Disable BootstrapAdmin immediately after first use.", user.UserName);
        }
    }
}
