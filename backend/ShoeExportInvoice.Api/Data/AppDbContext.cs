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
            entity.HasOne(f => f.Parent)
                  .WithMany(f => f.Children)
                  .HasForeignKey(f => f.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ProductMaster configuration
        modelBuilder.Entity<ProductMaster>(entity =>
        {
            entity.HasIndex(e => e.StyleCode).IsUnique();
            entity.Property(e => e.UnitPriceCMT).HasPrecision(18, 4);
            entity.Property(e => e.UnitPriceDAP).HasPrecision(18, 4);
            entity.HasOne(e => e.Folder)
                  .WithMany(f => f.Products)
                  .HasForeignKey(e => e.FolderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ShipmentOrder configuration
        modelBuilder.Entity<ShipmentOrder>(entity =>
        {
            entity.HasIndex(e => e.InvoiceNo).IsUnique();
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
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.SettlementPeriod)
                  .HasForeignKey(e => e.SettlementPeriodId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CustomsSettlementItem configuration
        modelBuilder.Entity<CustomsSettlementItem>(entity =>
        {
            entity.Property(e => e.OpeningBalance).HasPrecision(18, 2);
            entity.Property(e => e.InPeriodProduction).HasPrecision(18, 2);
            entity.Property(e => e.InPeriodExport).HasPrecision(18, 2);
            entity.Property(e => e.OtherExport).HasPrecision(18, 2);
            entity.Property(e => e.ClosingBalance).HasPrecision(18, 2);
        });
    }
}
