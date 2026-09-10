using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ProductMaster> ProductMasters => Set<ProductMaster>();
    public DbSet<ShipmentOrder> ShipmentOrders => Set<ShipmentOrder>();
    public DbSet<ShipmentOrderItem> ShipmentOrderItems => Set<ShipmentOrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ProductMaster configuration
        modelBuilder.Entity<ProductMaster>(entity =>
        {
            entity.HasIndex(e => e.StyleCode).IsUnique();
            entity.Property(e => e.UnitPriceCMT).HasPrecision(18, 4);
            entity.Property(e => e.UnitPriceDAP).HasPrecision(18, 4);
        });

        // ShipmentOrder configuration
        modelBuilder.Entity<ShipmentOrder>(entity =>
        {
            entity.HasIndex(e => e.InvoiceNo).IsUnique();
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
    }
}
