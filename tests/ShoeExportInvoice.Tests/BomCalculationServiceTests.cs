using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;
using ShoeExportInvoice.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace ShoeExportInvoice.Tests;

public class BomCalculationServiceTests
{
    [Fact]
    public async Task CalculateAsync_CalculatesCommonAndSizeDependentMaterials()
    {
        await using var fixture = new BomFixture();
        var order = await fixture.SeedScenarioAsync();

        var result = await new BomCalculationService(fixture.Context).CalculateAsync(order.Id);

        Assert.Equal(order.Id, result.ProductionOrderId);
        Assert.Equal(150, result.TotalQuantity);
        Assert.Equal("V2", result.BomVersion);
        Assert.Equal(2, result.Materials.Count);

        var leather = Assert.Single(result.Materials, x => x.MaterialCode == "LEATHER");
        Assert.Equal(346.5m, leather.TotalRequiredQuantity);
        Assert.Empty(leather.SizeBreakdown);

        var sole = Assert.Single(result.Materials, x => x.MaterialCode == "SOLE");
        Assert.Equal(157.5m, sole.TotalRequiredQuantity);
        Assert.Collection(sole.SizeBreakdown,
            size38 =>
            {
                Assert.Equal("38", size38.SizeName);
                Assert.Equal(50, size38.OrderQuantity);
                Assert.Equal(52.5m, size38.RequiredQuantity);
            },
            size39 =>
            {
                Assert.Equal("39", size39.SizeName);
                Assert.Equal(100, size39.OrderQuantity);
                Assert.Equal(105m, size39.RequiredQuantity);
            });
    }

    [Fact]
    public async Task CalculateAsync_UsesIdAsTieBreakerForLatestBom()
    {
        await using var fixture = new BomFixture();
        var material = fixture.AddMaterial("MAT", MaterialType.Common);
        var createdAt = new DateTime(2026, 9, 19, 1, 0, 0, DateTimeKind.Utc);
        fixture.AddBom("STYLE-1", "V1", createdAt, material, 1m);
        fixture.AddBom("STYLE-1", "V2", createdAt, material, 2m);
        var order = fixture.AddOrder("STYLE-1", 10, ("40", 10));
        await fixture.Context.SaveChangesAsync();

        var result = await new BomCalculationService(fixture.Context).CalculateAsync(order.Id);

        Assert.Equal("V2", result.BomVersion);
        Assert.Equal(20m, Assert.Single(result.Materials).TotalRequiredQuantity);
    }

    [Fact]
    public async Task CalculateAsync_PreservesDecimalPrecision()
    {
        await using var fixture = new BomFixture();
        var material = fixture.AddMaterial("GLUE", MaterialType.Common);
        fixture.AddBom("STYLE-1", "V1", DateTime.UtcNow, material, 0.1234m, 7.25m);
        var order = fixture.AddOrder("STYLE-1", 3, ("40", 3));
        await fixture.Context.SaveChangesAsync();

        var result = await new BomCalculationService(fixture.Context).CalculateAsync(order.Id);

        Assert.Equal(3m * 0.1234m * 1.0725m, Assert.Single(result.Materials).TotalRequiredQuantity);
    }

    [Fact]
    public async Task CalculateAsync_ThrowsWhenOrderDoesNotExist()
    {
        await using var fixture = new BomFixture();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => new BomCalculationService(fixture.Context).CalculateAsync(999));
    }

    [Fact]
    public async Task CalculateAsync_ThrowsWhenBomDoesNotExist()
    {
        await using var fixture = new BomFixture();
        var order = fixture.AddOrder("NO-BOM", 10, ("40", 10));
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => new BomCalculationService(fixture.Context).CalculateAsync(order.Id));
    }

    [Fact]
    public async Task CalculateAsync_ThrowsWhenSizeRunIsEmpty()
    {
        await using var fixture = new BomFixture();
        var order = new ProductionOrder { OrderNo = "PO-EMPTY", StyleCode = "STYLE-1", TotalQuantity = 10 };
        fixture.Context.ProductionOrders.Add(order);
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new BomCalculationService(fixture.Context).CalculateAsync(order.Id));
    }

    [Fact]
    public async Task CalculateAsync_ThrowsWhenSizeRunTotalDoesNotMatchOrder()
    {
        await using var fixture = new BomFixture();
        var order = fixture.AddOrder("STYLE-1", 11, ("40", 10));
        await fixture.Context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new BomCalculationService(fixture.Context).CalculateAsync(order.Id));
        Assert.Contains("không khớp", error.Message);
    }

    [Fact]
    public async Task CalculateAsync_ThrowsWhenBomHasNoItems()
    {
        await using var fixture = new BomFixture();
        fixture.Context.BomMasters.Add(new BomMaster
        {
            StyleCode = "STYLE-1", Version = "V1", CreatedAt = DateTime.UtcNow
        });
        var order = fixture.AddOrder("STYLE-1", 10, ("40", 10));
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new BomCalculationService(fixture.Context).CalculateAsync(order.Id));
    }

    [Fact]
    public async Task QueryFilters_IsolateBomModuleByTenant()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var setup = new AppDbContext(options, new TestTenantService(tenantA)))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Materials.Add(new Material
            {
                MaterialCode = "A", MaterialName = "Tenant A", Unit = "kg"
            });
            await setup.SaveChangesAsync();
        }

        await using var tenantBContext = new AppDbContext(options, new TestTenantService(tenantB));
        Assert.Empty(await tenantBContext.Materials.ToListAsync());
    }

    [Fact]
    public async Task DatabaseConstraints_RejectDuplicateSizeAndMaterialUsage()
    {
        await using var fixture = new BomFixture();
        var material = fixture.AddMaterial("MAT", MaterialType.Common);
        var bom = fixture.AddBom("STYLE-1", "V1", DateTime.UtcNow, material, 1m);
        bom.Items.Add(new BomItem { Material = material, NetConsumption = 2m });
        fixture.AddOrder("STYLE-1", 20, ("40", 10), ("40", 10));

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabaseRelationships_CascadeDetailsAndRestrictMaterialDeletion()
    {
        await using var fixture = new BomFixture();
        var material = fixture.AddMaterial("MAT", MaterialType.Common);
        var bom = fixture.AddBom("STYLE-1", "V1", DateTime.UtcNow, material, 1m);
        var order = fixture.AddOrder("STYLE-1", 10, ("40", 10));
        await fixture.Context.SaveChangesAsync();

        Assert.Throws<InvalidOperationException>(() => fixture.Context.Materials.Remove(material));
        fixture.Context.ChangeTracker.Clear();

        fixture.Context.BomMasters.Remove(await fixture.Context.BomMasters.SingleAsync(b => b.Id == bom.Id));
        fixture.Context.ProductionOrders.Remove(await fixture.Context.ProductionOrders.SingleAsync(o => o.Id == order.Id));
        await fixture.Context.SaveChangesAsync();

        Assert.Empty(await fixture.Context.BomItems.ToListAsync());
        Assert.Empty(await fixture.Context.OrderSizeRuns.ToListAsync());
        Assert.Single(await fixture.Context.Materials.ToListAsync());
    }

    [Fact]
    public async Task CalculateAsync_SelectsBomByStyleAndProcessAndCreatesSnapshot()
    {
        await using var fixture = new BomFixture();
        var material = fixture.AddMaterial("MAT", MaterialType.Common);
        fixture.AddBom("STYLE-1", "STD", DateTime.UtcNow.AddMinutes(-1), material, 1m);
        var goBom = fixture.AddBom("STYLE-1", "GO", DateTime.UtcNow, material, 3m);
        goBom.ProcessType = ProcessType.GoKhongMay;
        var order = fixture.AddOrder("STYLE-1", 10, ("40", 10));
        order.ProcessType = ProcessType.Standard;
        await fixture.Context.SaveChangesAsync();

        var result = await new BomCalculationService(fixture.Context).CalculateAsync(order.Id);

        Assert.Equal("STD", result.BomVersion);
        var saved = await fixture.Context.ProductionOrders.Include(o => o.MaterialRequirementPlan)!
            .ThenInclude(p => p!.Items).SingleAsync(o => o.Id == order.Id);
        Assert.Equal(ProductionOrderStatus.Calculated, saved.Status);
        Assert.Equal("STD", saved.MaterialRequirementPlan!.BomVersion);
        Assert.Equal(10m, Assert.Single(saved.MaterialRequirementPlan.Items).RequiredQuantity);
    }

    [Fact]
    public async Task ApprovedOrder_IssuesStockOnceAndBlocksDuplicateIssue()
    {
        await using var fixture = new BomFixture();
        var material = fixture.AddMaterial("MAT", MaterialType.Common);
        material.CurrentStock = 20m;
        fixture.AddBom("STYLE-1", "V1", DateTime.UtcNow, material, 1m);
        var order = fixture.AddOrder("STYLE-1", 10, ("40", 10));
        await fixture.Context.SaveChangesAsync();
        var service = new BomCalculationService(fixture.Context);
        await service.CalculateAsync(order.Id);
        var controller = new BomController(fixture.Context, service);

        await controller.Approve(order.Id, CancellationToken.None);
        await controller.Issue(order.Id, CancellationToken.None);

        Assert.Equal(10m, (await fixture.Context.Materials.SingleAsync(m => m.Id == material.Id)).CurrentStock);
        Assert.Equal(ProductionOrderStatus.Issued, (await fixture.Context.ProductionOrders.SingleAsync(o => o.Id == order.Id)).Status);
        var duplicate = await controller.Issue(order.Id, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(duplicate.Result);
        Assert.Equal(10m, (await fixture.Context.Materials.SingleAsync(m => m.Id == material.Id)).CurrentStock);
    }

    private sealed class TestTenantService : ICurrentTenantService
    {
        public TestTenantService(Guid tenantId) => TenantId = tenantId;
        public Guid TenantId { get; }
        public Task<TenantWorkspace> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class BomFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Context { get; }

        public BomFixture()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();
        }

        public Material AddMaterial(string code, MaterialType type)
        {
            var material = new Material
            {
                MaterialCode = code,
                MaterialName = code,
                Unit = type == MaterialType.Common ? "m2" : "đôi",
                MaterialType = type
            };
            Context.Materials.Add(material);
            return material;
        }

        public BomMaster AddBom(
            string styleCode,
            string version,
            DateTime createdAt,
            Material material,
            decimal consumption,
            decimal wastage = 0m)
        {
            var bom = new BomMaster
            {
                StyleCode = styleCode,
                Version = version,
                CreatedAt = createdAt,
                Items = new List<BomItem>
                {
                    new()
                    {
                        Material = material,
                        NetConsumption = consumption,
                        WastageRatePercent = wastage
                    }
                }
            };
            Context.BomMasters.Add(bom);
            return bom;
        }

        public ProductionOrder AddOrder(string styleCode, int total, params (string Size, int Quantity)[] sizes)
        {
            var order = new ProductionOrder
            {
                OrderNo = $"PO-{Guid.NewGuid():N}",
                StyleCode = styleCode,
                TotalQuantity = total,
                SizeRuns = sizes.Select(s => new OrderSizeRun
                {
                    SizeName = s.Size,
                    Quantity = s.Quantity
                }).ToList()
            };
            Context.ProductionOrders.Add(order);
            return order;
        }

        public async Task<ProductionOrder> SeedScenarioAsync()
        {
            var leather = AddMaterial("LEATHER", MaterialType.Common);
            var sole = AddMaterial("SOLE", MaterialType.SizeDependent);
            AddBom("STYLE-1", "V1", new DateTime(2026, 1, 1), leather, 99m);
            var latest = AddBom("STYLE-1", "V2", new DateTime(2026, 2, 1), leather, 2.2m, 5m);
            latest.Items.Add(new BomItem
            {
                Material = sole,
                NetConsumption = 1m,
                WastageRatePercent = 5m
            });
            var order = AddOrder("STYLE-1", 150, ("38", 50), ("39", 100));
            await Context.SaveChangesAsync();
            return order;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
