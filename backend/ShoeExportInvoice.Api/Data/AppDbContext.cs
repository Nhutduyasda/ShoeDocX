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
    public DbSet<ShipmentSourceBatch> ShipmentSourceBatches => Set<ShipmentSourceBatch>();
    public DbSet<WarehouseBatchItem> WarehouseBatchItems => Set<WarehouseBatchItem>();
    public DbSet<ShipmentUnlockAudit> ShipmentUnlockAudits => Set<ShipmentUnlockAudit>();
    public DbSet<BusinessAuditLog> BusinessAuditLogs => Set<BusinessAuditLog>();
    public DbSet<RevokedJwt> RevokedJwts => Set<RevokedJwt>();
    public DbSet<CompanyTemplate> CompanyTemplates => Set<CompanyTemplate>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<BomMaster> BomMasters => Set<BomMaster>();
    public DbSet<BomItem> BomItems => Set<BomItem>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<OrderSizeRun> OrderSizeRuns => Set<OrderSizeRun>();
    public DbSet<MaterialRequirementPlan> MaterialRequirementPlans => Set<MaterialRequirementPlan>();
    public DbSet<MaterialRequirementPlanItem> MaterialRequirementPlanItems => Set<MaterialRequirementPlanItem>();
    public DbSet<MaterialRequirementPlanSize> MaterialRequirementPlanSizes => Set<MaterialRequirementPlanSize>();

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
            entity.HasIndex(e => new { e.FolderId, e.StyleCode }).IsUnique();
            entity.HasIndex(e => e.StyleCode).IsUnique().HasFilter("[FolderId] IS NULL");
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
            entity.HasIndex(e => e.DeclarationNo).IsUnique().HasFilter("[DeclarationNo] IS NOT NULL AND [DeclarationNo] <> ''");
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
            entity.HasIndex(e => e.ShipmentOrderId).IsUnique().HasFilter("[ShipmentOrderId] IS NOT NULL");
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

        modelBuilder.Entity<ShipmentSourceBatch>(entity =>
        {
            entity.HasKey(e => new { e.ShipmentOrderId, e.SourceBatchId });
            entity.HasIndex(e => new { e.SourceBatchId, e.RelationType });
            entity.HasOne(e => e.ShipmentOrder).WithMany(e => e.SourceBatches)
                .HasForeignKey(e => e.ShipmentOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.SourceBatch).WithMany()
                .HasForeignKey(e => e.SourceBatchId).OnDelete(DeleteBehavior.Restrict);
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

        // BOM and material calculator configuration
        modelBuilder.Entity<Material>(entity =>
        {
            entity.Property(e => e.CurrentStock).HasPrecision(18, 4);
            entity.Property(e => e.CurrentStock).IsConcurrencyToken();
            entity.HasIndex(e => new { e.TenantId, e.MaterialCode }).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Materials_CurrentStock_NonNegative", "CurrentStock >= 0");
                t.HasCheckConstraint("CK_Materials_MaterialType", "MaterialType IN (0, 1)");
            });
        });

        modelBuilder.Entity<BomMaster>(entity =>
        {
            entity.Property(e => e.ProcessType).HasDefaultValue(ProcessType.Standard);
            entity.HasIndex(e => new { e.TenantId, e.StyleCode, e.ProcessType, e.Version }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.StyleCode, e.CreatedAt });
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.BomMaster)
                  .HasForeignKey(e => e.BomMasterId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint("CK_BomMasters_ProcessType", "ProcessType IN (1, 2)"));
        });

        modelBuilder.Entity<BomItem>(entity =>
        {
            entity.Property(e => e.NetConsumption).HasPrecision(18, 4);
            entity.Property(e => e.WastageRatePercent).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.BomMasterId, e.MaterialId }).IsUnique();
            entity.HasOne(e => e.Material)
                  .WithMany(e => e.BomItems)
                  .HasForeignKey(e => e.MaterialId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_BomItems_NetConsumption_NonNegative", "NetConsumption >= 0");
                t.HasCheckConstraint("CK_BomItems_WastageRate", "WastageRatePercent BETWEEN 0 AND 100");
            });
        });

        modelBuilder.Entity<ProductionOrder>(entity =>
        {
            entity.Property(e => e.ProcessType).HasDefaultValue(ProcessType.Standard);
            entity.HasIndex(e => new { e.TenantId, e.OrderNo }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.StyleCode });
            entity.Property(e => e.Status).IsConcurrencyToken();
            entity.HasMany(e => e.SizeRuns)
                  .WithOne(e => e.ProductionOrder)
                  .HasForeignKey(e => e.ProductionOrderId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProductionOrders_TotalQuantity_NonNegative", "TotalQuantity >= 0");
                t.HasCheckConstraint("CK_ProductionOrders_ProcessType", "ProcessType IN (1, 2)");
                t.HasCheckConstraint("CK_ProductionOrders_Status", "Status IN (0, 1, 2, 3, 4)");
            });
        });

        modelBuilder.Entity<OrderSizeRun>(entity =>
        {
            entity.HasIndex(e => new { e.ProductionOrderId, e.SizeName }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_OrderSizeRuns_Quantity_Positive", "Quantity > 0"));
        });

        modelBuilder.Entity<MaterialRequirementPlan>(entity =>
        {
            entity.HasIndex(e => e.ProductionOrderId).IsUnique();
            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.HasOne(e => e.ProductionOrder).WithOne(e => e.MaterialRequirementPlan)
                .HasForeignKey<MaterialRequirementPlan>(e => e.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Items).WithOne(e => e.Plan)
                .HasForeignKey(e => e.MaterialRequirementPlanId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MaterialRequirementPlanItem>(entity =>
        {
            entity.HasIndex(e => new { e.MaterialRequirementPlanId, e.MaterialId }).IsUnique();
            entity.HasOne(e => e.Material).WithMany().HasForeignKey(e => e.MaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.NetConsumption).HasPrecision(18, 4);
            entity.Property(e => e.WastageRatePercent).HasPrecision(18, 4);
            entity.Property(e => e.RequiredQuantity).HasPrecision(18, 4);
            entity.Property(e => e.StockAtCalculation).HasPrecision(18, 4);
            entity.HasMany(e => e.SizeBreakdown).WithOne(e => e.PlanItem)
                .HasForeignKey(e => e.MaterialRequirementPlanItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MaterialRequirementPlanSize>(entity =>
        {
            entity.HasIndex(e => new { e.MaterialRequirementPlanItemId, e.SizeName }).IsUnique();
            entity.Property(e => e.RequiredQuantity).HasPrecision(18, 4);
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

        modelBuilder.Entity<Material>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && e.TenantId == null));

        modelBuilder.Entity<BomMaster>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && e.TenantId == null));

        modelBuilder.Entity<BomItem>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && e.TenantId == null));

        modelBuilder.Entity<ProductionOrder>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && e.TenantId == null));

        modelBuilder.Entity<OrderSizeRun>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && e.TenantId == null));

        modelBuilder.Entity<MaterialRequirementPlan>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && e.TenantId == null));
        modelBuilder.Entity<MaterialRequirementPlanItem>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && e.TenantId == null));
        modelBuilder.Entity<MaterialRequirementPlanSize>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId || (CurrentTenantId == DefaultTenantId && e.TenantId == null));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AssignTenantIds();
        var (folderIds, changedFolderIds) = GetPackingPolicyTargets();
        if (folderIds.Count > 0) MasterDataFolders.Where(f => folderIds.Contains(f.Id)).Load();
        if (changedFolderIds.Count > 0) ProductMasters.Where(p => p.FolderId.HasValue && changedFolderIds.Contains(p.FolderId.Value)).Load();
        ApplyFolderPackingPolicy();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AssignTenantIds();
        var (folderIds, changedFolderIds) = GetPackingPolicyTargets();
        if (folderIds.Count > 0) await MasterDataFolders.Where(f => folderIds.Contains(f.Id)).LoadAsync(cancellationToken);
        if (changedFolderIds.Count > 0) await ProductMasters.Where(p => p.FolderId.HasValue && changedFolderIds.Contains(p.FolderId.Value)).LoadAsync(cancellationToken);
        ApplyFolderPackingPolicy();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private (List<int> FolderIds, List<int> ChangedFolderIds) GetPackingPolicyTargets()
    {
        var changedFolders = ChangeTracker.Entries<MasterDataFolder>()
            .Where(e => e.State == EntityState.Modified && e.Property(f => f.DefaultPairsPerCarton).IsModified)
            .Select(e => e.Entity.Id).ToList();
        var ids = ChangeTracker.Entries<ProductMaster>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified && e.Entity.FolderId.HasValue)
            .Select(e => e.Entity.FolderId!.Value).Concat(changedFolders).Distinct().ToList();
        return (ids, changedFolders);
    }

    private void ApplyFolderPackingPolicy()
    {
        // One authoritative packing size per folder, including imports and bulk moves.
        var folders = ChangeTracker.Entries<MasterDataFolder>()
            .Where(e => e.State != EntityState.Deleted && e.Entity.Id > 0).Select(e => e.Entity).ToDictionary(f => f.Id);
        foreach (var entry in ChangeTracker.Entries<ProductMaster>())
        {
            if (entry.State == EntityState.Deleted) continue;
            var product = entry.Entity;
            var folder = product.Folder;
            if (product.FolderId.HasValue && folders.TryGetValue(product.FolderId.Value, out var assigned)) folder = assigned;
            if (folder == null || (!product.FolderId.HasValue && Entry(folder).State != EntityState.Added)) continue;
            if (product.PairPerCarton != folder.DefaultPairsPerCarton)
            {
                product.PairPerCarton = folder.DefaultPairsPerCarton;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    public async Task<int> RepairFolderPackingAsync(CancellationToken cancellationToken = default)
    {
        var mismatches = await ProductMasters.Include(p => p.Folder)
            .Where(p => p.Folder != null && p.PairPerCarton != p.Folder.DefaultPairsPerCarton)
            .ToListAsync(cancellationToken);
        foreach (var product in mismatches)
        {
            product.PairPerCarton = product.Folder!.DefaultPairsPerCarton;
            product.UpdatedAt = DateTime.UtcNow;
        }
        if (mismatches.Count > 0) await SaveChangesAsync(cancellationToken);
        return mismatches.Count;
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
