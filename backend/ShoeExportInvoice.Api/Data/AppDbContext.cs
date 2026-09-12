using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ProductMaster> ProductMasters => Set<ProductMaster>();
    public DbSet<MasterDataFolder> MasterDataFolders => Set<MasterDataFolder>();
    public DbSet<ShipmentOrder> ShipmentOrders => Set<ShipmentOrder>();
    public DbSet<ShipmentOrderItem> ShipmentOrderItems => Set<ShipmentOrderItem>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<CustomsSettlementPeriod> CustomsSettlementPeriods => Set<CustomsSettlementPeriod>();
    public DbSet<CustomsSettlementItem> CustomsSettlementItems => Set<CustomsSettlementItem>();

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
        });

        // ShipmentOrder configuration
        modelBuilder.Entity<ShipmentOrder>(entity =>
        {
            entity.HasIndex(e => e.InvoiceNo).IsUnique();
            entity.HasOne<MasterDataFolder>().WithMany().HasForeignKey(e => e.ContractFolderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.ContractFolderId, e.Status, e.CustomsDeclarationType, e.ClearanceDate });
            entity.Property(e => e.IsLocked).IsConcurrencyToken();
            entity.Property(e => e.Status).IsConcurrencyToken();
            entity.HasIndex(e => e.DeclarationNo);
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
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.SettlementPeriod)
                  .HasForeignKey(e => e.SettlementPeriodId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CustomsSettlementItem configuration
        modelBuilder.Entity<CustomsSettlementItem>(entity =>
        {
            entity.HasIndex(e => new { e.SettlementPeriodId, e.ProductCode });
            entity.Property(e => e.OpeningBalance).HasPrecision(18, 2);
            entity.Property(e => e.InPeriodProduction).HasPrecision(18, 2);
            entity.Property(e => e.InPeriodExport).HasPrecision(18, 2);
            entity.Property(e => e.OtherExport).HasPrecision(18, 2);
            entity.Property(e => e.ClosingBalance).HasPrecision(18, 2);
        });
    }
}
