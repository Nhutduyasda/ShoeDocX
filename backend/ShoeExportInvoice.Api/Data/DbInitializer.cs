using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShoeExportInvoice.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context, ILogger logger)
    {
        if (!context.Database.IsSqlServer())
        {
            // SQLite remains available only for isolated tests. Production uses SQL Server.
            await context.Database.EnsureCreatedAsync();
            await EnsureDefaultTenantWorkspaceAsync(context, logger);
            await EnsureDefaultCompanyTemplateAsync(context, logger);
            return;
        }

        if (context.Database.IsSqlServer())
        {
            await context.Database.MigrateAsync();
            await EnsureDefaultTenantWorkspaceAsync(context, logger);
            await EnsureDefaultCompanyTemplateAsync(context, logger);
            return;
        }

        // Legacy SQLite upgrade path retained for test/transition tooling only.
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
        await EnsureDefaultTenantWorkspaceAsync(context, logger);
        await EnsureDefaultCompanyTemplateAsync(context, logger);
        var unresolved = await context.ShipmentOrders.CountAsync(o => o.ContractFolderId == null);
        if (unresolved > 0) logger.LogWarning("{Count} historical orders have no unambiguous contract folder; review before editing.", unresolved);
    }

    private static async Task EnsureDefaultTenantWorkspaceAsync(AppDbContext context, ILogger logger)
    {
        var defaultWorkspace = await context.TenantWorkspaces.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == AppDbContext.DefaultTenantId);
        if (defaultWorkspace == null)
        {
            context.TenantWorkspaces.Add(new Models.Entities.TenantWorkspace
            {
                Id = AppDbContext.DefaultTenantId,
                CompanyName = "Công ty Mặc Định (Hải An / Kingmaker)",
                TaxCode = "1800123456",
                AiCredits = 2,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            logger.LogInformation("Khởi tạo thành công Workspace mặc định với 2 AI Credits.");
        }
    }

    private static async Task EnsureDefaultCompanyTemplateAsync(AppDbContext context, ILogger logger)
    {
        var defaultTemplate = await context.CompanyTemplates.FirstOrDefaultAsync(t => t.IsDefault);
        if (defaultTemplate == null)
        {
            var config = new ShoeExportInvoice.Api.Models.Templates.DocumentTemplateConfig
            {
                TemplateName = "Mẫu Tiêu Chuẩn Kingmaker / Hải An (Mặc định)",
                InvSheet = new ShoeExportInvoice.Api.Models.Templates.InvSheetConfig
                {
                    SheetName = "INV",
                    Header = new ShoeExportInvoice.Api.Models.Templates.InvHeaderCells
                    {
                        InvoiceNoCell = "J4",
                        DateCell = "J5",
                        ContractNoCell = "J6",
                        DeliveryTermsCell = "J7",
                        PaymentTermsCell = "J8",
                        DestinationCell = "J9",
                        BuyerNameCell = "D4",
                        BuyerAddressCell = "D5"
                    },
                    Table = new ShoeExportInvoice.Api.Models.Templates.InvTableColumns
                    {
                        StartRow = 13,
                        SttCol = "B",
                        ItemCodeCol = "C",
                        DescriptionCol = "D",
                        QuantityCol = "E",
                        UnitCol = "F",
                        CmtUnitPriceCol = "G",
                        DapUnitPriceCol = "H",
                        CmtAmountCol = "I",
                        DapAmountCol = "J"
                    }
                },
                PklSheet = new ShoeExportInvoice.Api.Models.Templates.PklSheetConfig
                {
                    SheetName = "PKL",
                    StartRow = 12,
                    CartonRangeCol = "A",
                    ItemCodeCol = "B",
                    DescriptionCol = "C",
                    QuantityCol = "D",
                    UnitCol = "E",
                    CartonsCol = "F",
                    NetWeightCol = "G",
                    GrossWeightCol = "H"
                }
            };

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var configJson = System.Text.Json.JsonSerializer.Serialize(config, jsonOptions);

            context.CompanyTemplates.Add(new ShoeExportInvoice.Api.Models.Entities.CompanyTemplate
            {
                Name = "Mẫu Tiêu Chuẩn Kingmaker / Hải An (Mặc định)",
                Description = "Mẫu phôi Excel hóa đơn xuất khẩu và đóng gói chuẩn cho Kingmaker / Hải An",
                TemplateFileName = "Shipment_Template.xlsx",
                TemplateFilePath = "Templates/Shipment_Template.xlsx",
                ConfigJson = configJson,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            logger.LogInformation("Đã khởi tạo bản ghi mẫu phôi mặc định: Mẫu Tiêu Chuẩn Kingmaker / Hải An (Mặc định)");
        }
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

    /// <summary>
    /// Creates or repairs the well-known local accounts used to exercise each authorization flow.
    /// This must only be called for an explicitly enabled development environment because it
    /// resets passwords to public, non-production values on every startup.
    /// </summary>
    public static async Task SeedDevelopmentUsersAsync(
        Microsoft.AspNetCore.Identity.UserManager<ShoeExportInvoice.Api.Models.Entities.ApplicationUser> userManager,
        ILogger logger)
    {
        var accounts = new[]
        {
            new DevelopmentAccount("admin", "@Admin1234", "Quản trị viên", Models.Entities.Department.Admin),
            new DevelopmentAccount("xnk", "@Xnk123456", "Nhân viên Xuất Nhập Khẩu", Models.Entities.Department.Xnk),
            new DevelopmentAccount("kho", "@Kho123456", "Nhân viên Kho Thành Phẩm", Models.Entities.Department.Kho),
            new DevelopmentAccount("ketoan", "@KeToan123", "Nhân viên Kế toán", Models.Entities.Department.KeToan),
            new DevelopmentAccount("xnkmanager", "@XnkManager123", "Trưởng phòng Xuất Nhập Khẩu", Models.Entities.Department.XnkManager)
        };

        foreach (var account in accounts)
        {
            var user = await userManager.FindByNameAsync(account.Username);
            if (user == null)
            {
                user = new Models.Entities.ApplicationUser
                {
                    UserName = account.Username,
                    FullName = account.FullName,
                    Department = account.Department,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                var createResult = await userManager.CreateAsync(user, account.Password);
                if (!createResult.Succeeded)
                    throw new InvalidOperationException($"Cannot create development user {account.Username}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
            else
            {
                user.FullName = account.FullName;
                user.Department = account.Department;
                user.IsActive = true;
                user.LockoutEnd = null;
                user.AccessFailedCount = 0;
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                    throw new InvalidOperationException($"Cannot repair development user {account.Username}: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");

                if (await userManager.HasPasswordAsync(user))
                {
                    var removeResult = await userManager.RemovePasswordAsync(user);
                    if (!removeResult.Succeeded)
                        throw new InvalidOperationException($"Cannot remove development password for {account.Username}: {string.Join(", ", removeResult.Errors.Select(e => e.Description))}");
                }
                var passwordResult = await userManager.AddPasswordAsync(user, account.Password);
                if (!passwordResult.Succeeded)
                    throw new InvalidOperationException($"Cannot reset development password for {account.Username}: {string.Join(", ", passwordResult.Errors.Select(e => e.Description))}");
            }
        }

        logger.LogWarning("Development login accounts were created/repaired. Never enable these credentials in Production.");
    }

    private sealed record DevelopmentAccount(
        string Username,
        string Password,
        string FullName,
        Models.Entities.Department Department);
}
