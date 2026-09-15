using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ICurrentTenantService? _currentTenantService;

    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Guid CurrentTenantId => _currentTenantService?.TenantId ?? DefaultTenantId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenantService? currentTenantService = null) : base(options)
    {
        _currentTenantService = currentTenantService;
    }

    public DbSet<TenantWorkspace> TenantWorkspaces => Set<TenantWorkspace>();
    public DbSet<ProductMaster> ProductMasters => Set<ProductMaster>();
    public DbSet<MasterDataFolder> MasterDataFolders => Set<MasterDataFolder>();
    public DbSet<ShipmentOrder> ShipmentOrders => Set<ShipmentOrder>();
    public DbSet<ShipmentOrderItem> ShipmentOrderItems => Set<ShipmentOrderItem>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<CustomsSettlementPeriod> CustomsSettlementPeriods => Set<CustomsSettlementPeriod>();
    public DbSet<CustomsSettlementItem> CustomsSettlementItems => Set<CustomsSettlementItem>();
    public DbSet<WarehouseBatch> WarehouseBatches => Set<WarehouseBatch>();
    public DbSet<WarehouseBatchItem> WarehouseBatchItems => Set<WarehouseBatchItem>();
    public DbSet<ShipmentUnlockAudit> ShipmentUnlockAudits => Set<ShipmentUnlockAudit>();
    public DbSet<BusinessAuditLog> BusinessAuditLogs => Set<BusinessAuditLog>();
    public DbSet<RevokedJwt> RevokedJwts => Set<RevokedJwt>();
    public DbSet<CompanyTemplate> CompanyTemplates => Set<CompanyTemplate>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MasterDataFolder configuration
        modelBuilder.Entity<MasterDataFolder>(entity =>
        {
            entity.HasIndex(f => f.ParentId);
            entity.HasOne(f => f.Parent)
                  .WithMany(f => f.Children)
                  .HasForeignKey(f => f.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ProductMaster configuration
        modelBuilder.Entity<ProductMaster>(entity =>
        {
            entity.Property(e => e.StyleCode).UseCollation("NOCASE");
            entity.HasIndex(e => new { e.FolderId, e.StyleCode }).IsUnique();
            entity.HasIndex(e => e.StyleCode).IsUnique().HasFilter("\"FolderId\" IS NULL");
            entity.Property(e => e.UnitPriceCMT).HasPrecision(18, 4);
            entity.Property(e => e.UnitPriceDAP).HasPrecision(18, 4);
            entity.HasOne(e => e.Folder)
                  .WithMany(f => f.Products)
                  .HasForeignKey(e => e.FolderId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProductMasters_PairPerCarton", "PairPerCarton BETWEEN 1 AND 1000");
                t.HasCheckConstraint("CK_ProductMasters_Prices_NonNegative", "UnitPriceCMT >= 0 AND UnitPriceDAP >= 0 AND (UnitPriceCMT_Go IS NULL OR UnitPriceCMT_Go >= 0) AND (UnitPriceDAP_Go IS NULL OR UnitPriceDAP_Go >= 0)");
            });
        });

        // ShipmentOrder configuration
        modelBuilder.Entity<ShipmentOrder>(entity =>
        {
            entity.HasIndex(e => e.InvoiceNo).IsUnique();
            entity.HasOne<MasterDataFolder>().WithMany().HasForeignKey(e => e.ContractFolderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.ContractFolderId, e.Status, e.CustomsDeclarationType, e.ClearanceDate });
            entity.Property(e => e.IsLocked).IsConcurrencyToken();
            entity.Property(e => e.Status).IsConcurrencyToken();
            entity.HasIndex(e => e.DeclarationNo).IsUnique().HasFilter("\"DeclarationNo\" IS NOT NULL AND \"DeclarationNo\" <> ''");
            entity.Property(e => e.CustomsGrossWeight).HasPrecision(18, 4);
            entity.Property(e => e.CustomsTotalDap).HasPrecision(18, 4);
            entity.Property(e => e.CustomsTotalCmt).HasPrecision(18, 4);
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.ShipmentOrder)
                  .HasForeignKey(e => e.ShipmentOrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ShipmentOrderItem configuration
        modelBuilder.Entity<ShipmentOrderItem>(entity =>
        {
            entity.HasIndex(e => e.StyleCode);
            entity.Property(e => e.UnitPriceCMT).HasPrecision(18, 4);
            entity.Property(e => e.UnitPriceDAP).HasPrecision(18, 4);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ShipmentOrderItems_Quantity_Positive", "Quantity > 0");
                t.HasCheckConstraint("CK_ShipmentOrderItems_PairPerCarton", "PairPerCarton BETWEEN 1 AND 1000");
                t.HasCheckConstraint("CK_ShipmentOrderItems_Prices_NonNegative", "UnitPriceCMT >= 0 AND UnitPriceDAP >= 0");
                t.HasCheckConstraint("CK_ShipmentOrderItems_ProcessType", "ProcessType IN (1, 2)");
            });
        });

        // SystemSetting configuration
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // CustomsSettlementPeriod configuration
        modelBuilder.Entity<CustomsSettlementPeriod>(entity =>
        {
            entity.HasOne<MasterDataFolder>().WithMany().HasForeignKey(e => e.ContractFolderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.ContractFolderId, e.ToDate });
            entity.Property(e => e.Status).IsConcurrencyToken();
            entity.ToTable(t => t.HasCheckConstraint("CK_CustomsSettlementPeriods_Status", "Status IN ('Draft', 'Finalized')"));
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.SettlementPeriod)
                  .HasForeignKey(e => e.SettlementPeriodId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CustomsSettlementItem configuration
        modelBuilder.Entity<CustomsSettlementItem>(entity =>
        {
            entity.HasIndex(e => new { e.SettlementPeriodId, e.ProductCode, e.ProcessType }).IsUnique();
            entity.Property(e => e.OpeningBalance).HasPrecision(18, 2);
            entity.Property(e => e.InPeriodProduction).HasPrecision(18, 2);
            entity.Property(e => e.InPeriodExport).HasPrecision(18, 2);
            entity.Property(e => e.OtherExport).HasPrecision(18, 2);
            entity.Property(e => e.ClosingBalance).HasPrecision(18, 2);
            entity.ToTable(t => t.HasCheckConstraint("CK_CustomsSettlementItems_Inputs_NonNegative", "OpeningBalance >= 0 AND InPeriodProduction >= 0 AND InPeriodExport >= 0 AND OtherExport >= 0"));
        });

        modelBuilder.Entity<BusinessAuditLog>(entity =>
        {
            entity.HasIndex(e => new { e.ResourceType, e.ResourceId, e.CreatedAt });
            entity.HasIndex(e => e.CreatedAt);
        });
        modelBuilder.Entity<RevokedJwt>(entity =>
        {
            entity.HasIndex(e => e.Jti).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
        });

        modelBuilder.Entity<ShipmentUnlockAudit>(entity =>
        {
            entity.HasIndex(e => new { e.ShipmentOrderId, e.UnlockedAt });
            entity.HasOne(e => e.ShipmentOrder).WithMany().HasForeignKey(e => e.ShipmentOrderId).OnDelete(DeleteBehavior.Restrict);
        });

        // WarehouseBatch configuration
        modelBuilder.Entity<WarehouseBatch>(entity =>
        {
            entity.HasIndex(e => e.BatchNumber);
            entity.HasIndex(e => e.ExportDate);
            entity.HasOne(e => e.ContractFolder)
                  .WithMany()
                  .HasForeignKey(e => e.ContractFolderId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ShipmentOrder)
                  .WithMany()
                  .HasForeignKey(e => e.ShipmentOrderId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.Status).IsConcurrencyToken();
            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.HasIndex(e => e.ShipmentOrderId).IsUnique().HasFilter("\"ShipmentOrderId\" IS NOT NULL");
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.WarehouseBatch)
                  .HasForeignKey(e => e.WarehouseBatchId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // WarehouseBatchItem configuration
        modelBuilder.Entity<WarehouseBatchItem>(entity =>
        {
            entity.HasIndex(e => new { e.WarehouseBatchId, e.StyleCode });
            entity.ToTable(t => t.HasCheckConstraint("CK_WarehouseBatchItems_Quantity_Positive", "Quantity > 0"));
        });

        // CompanyTemplate configuration
        modelBuilder.Entity<CompanyTemplate>(entity =>
        {
            entity.HasIndex(e => e.IsDefault);
            entity.HasOne(e => e.Folder)
                  .WithMany()
                  .HasForeignKey(e => e.FolderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // TenantWorkspace configuration
        modelBuilder.Entity<TenantWorkspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CompanyName).HasMaxLength(255);
            entity.Property(e => e.TaxCode).HasMaxLength(50);
        });

        // Global Query Filters for Multi-Tenancy
        modelBuilder.Entity<CompanyTemplate>()
            .HasQueryFilter(t => t.IsDefault || t.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && t.TenantId == null));

        modelBuilder.Entity<ProductMaster>()
            .HasQueryFilter(p => p.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && p.TenantId == null));

        modelBuilder.Entity<MasterDataFolder>()
            .HasQueryFilter(f => f.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && f.TenantId == null));

        modelBuilder.Entity<ShipmentOrder>()
            .HasQueryFilter(s => s.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && s.TenantId == null));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AssignTenantIds();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AssignTenantIds();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AssignTenantIds()
    {
        var tenantId = CurrentTenantId;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is ITenantEntity tenantEntity)
            {
                if (!tenantEntity.TenantId.HasValue || tenantEntity.TenantId.Value == Guid.Empty)
                {
                    tenantEntity.TenantId = tenantId;
                }
            }
        }
    }
}
