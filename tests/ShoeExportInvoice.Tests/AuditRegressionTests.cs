using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using ShoeExportInvoice.Api.Controllers;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Tests;

public class AuditRegressionTests
{
    private static AppDbContext MemoryDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static CustomsDeclarationService Customs(AppDbContext db, string? root = null) => new(db,
        NullLogger<CustomsDeclarationService>.Instance, new DummyWebHostEnvironment { ContentRootPath = root ?? Path.GetTempPath() });

    [Fact]
    public async Task UpdateShipment_UsesRouteIdAndOverwritesClientPricingFromContractMasterData()
    {
        using var db = MemoryDb();
        var folder = new MasterDataFolder { Name = "Contract A", ContractNo = "A" };
        db.Add(folder);
        await db.SaveChangesAsync();
        db.Add(new ProductMaster
        {
            FolderId = folder.Id, StyleCode = "STYLE-1", Description = "Authoritative description",
            Unit = "PRS", PairPerCarton = 24, UnitPriceCMT = 3.25m, UnitPriceDAP = 12.5m
        });
        var order = new ShipmentOrder
        {
            ContractFolderId = folder.Id, InvoiceNo = "OLD-INV", CustomerName = "Customer",
            Items = [new ShipmentOrderItem { StyleCode = "STYLE-1", FullItemCode = "STYLE-1", Quantity = 1 }]
        };
        db.Add(order);
        await db.SaveChangesAsync();

        var controller = new ShipmentsController(db, new FakeExcelService(), new FakeSequenceService(),
            NullLogger<ShipmentsController>.Instance);
        var response = await controller.UpdateShipment(order.Id, new CreateShipmentRequestDto
        {
            ContractFolderId = folder.Id,
            InvoiceNo = "NEW-INV",
            CustomerName = "Customer",
            Items = [new CreateShipmentItemDto
            {
                StyleCode = "STYLE-1", Quantity = 10, UnitPriceCMT = 999m, UnitPriceDAP = 999m,
                Description = "client value", Unit = "client", PairPerCarton = 1
            }]
        });

        Assert.IsType<OkObjectResult>(response);
        db.ChangeTracker.Clear();
        var saved = await db.ShipmentOrders.Include(s => s.Items).SingleAsync();
        Assert.Equal(order.Id, saved.Id);
        Assert.Equal("NEW-INV", saved.InvoiceNo);
        var item = Assert.Single(saved.Items);
        Assert.Equal(3.25m, item.UnitPriceCMT);
        Assert.Equal(12.5m, item.UnitPriceDAP);
        Assert.Equal(24, item.PairPerCarton);
        Assert.Equal("Authoritative description", item.Description);
    }

    [Fact]
    public async Task SameCodeInDifferentContracts_IsAllowed_ButSameContractCaseDuplicateIsRejected()
    {
        using var db = MemoryDb();
        var a = new MasterDataFolder { Name = "A", ContractNo = "A" };
        var b = new MasterDataFolder { Name = "B", ContractNo = "B", DefaultPairsPerCarton = 24 };
        db.AddRange(a, b); await db.SaveChangesAsync();
        var service = new ProductMasterService(db);
        await service.CreateAsync(new CreateProductMasterDto { StyleCode = "BM5879-464", Description = "A", FolderId = a.Id, UnitPriceDAP = 8 });
        await service.CreateAsync(new CreateProductMasterDto { StyleCode = "BM5879-464", Description = "B", FolderId = b.Id, UnitPriceDAP = 9 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new CreateProductMasterDto { StyleCode = "bm5879-464", FolderId = a.Id }));
        Assert.Equal(2, await db.ProductMasters.CountAsync());
    }

    [Theory]
    [InlineData("1.200", 1200)]
    [InlineData("1.200,00", 1200)]
    [InlineData("1,200.00", 1200)]
    public void QuantityParsing_IsIndependentOfMachineCulture(string value, int expected)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "vi-VN", "en-US", "de-DE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.True(CustomsDeclarationService.TryParseCustomsQuantity(value, out var result));
                Assert.Equal(expected, result);
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData("-12")]
    [InlineData("12,5")]
    [InlineData("wrong12")]
    public void InvalidQuantity_IsNeverSilentlyRoundedOrMadePositive(string value) =>
        Assert.False(CustomsDeclarationService.TryParseCustomsQuantity(value, out _));

    [Fact]
    public async Task ConfirmRequiresOriginalFile_AndRejectsForgedMatchFlag()
    {
        using var db = MemoryDb();
        var order = new ShipmentOrder { InvoiceNo = "OTHER", CustomerName = "Test", Status = ShipmentStatus.Exported };
        db.Add(order); await db.SaveChangesAsync();
        var service = Customs(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmSyncAsync(order.Id, new() { IsFullyMatched = true }));
        using var file = CustomsClearanceSyncTests.CreateSampleVnaccsExcelStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmSyncAsync(order.Id, new() { IsFullyMatched = true }, file, "file.xlsx"));
        Assert.False(order.IsLocked);
        Assert.Equal(ShipmentStatus.Exported, order.Status);
    }

    [Theory]
    [InlineData(true, ShipmentStatus.Exported)]
    [InlineData(false, ShipmentStatus.Cleared)]
    public async Task EitherLockCondition_PreventsResync(bool locked, ShipmentStatus status)
    {
        using var db = MemoryDb();
        var order = new ShipmentOrder { InvoiceNo = "LOCK", CustomerName = "Test", IsLocked = locked, Status = status };
        db.Add(order); await db.SaveChangesAsync();
        using var file = CustomsClearanceSyncTests.CreateSampleVnaccsExcelStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Customs(db).ConfirmSyncAsync(order.Id, new(), file, "file.xlsx"));
    }

    [Fact]
    public async Task Reconciliation_RejectsCmtMismatchAndHeaderTotalMismatch()
    {
        using var db = MemoryDb();
        var order = new ShipmentOrder { InvoiceNo = "INV", CustomerName = "Test", Items = [new() {
            StyleCode = "BM5879-464", Quantity = 10, UnitPriceDAP = 8, UnitPriceCMT = 3 }] };
        db.Add(order); await db.SaveChangesAsync();
        var declaration = new CustomsDeclarationParsedDto { InvoiceNo = "INV", Items = [new() {
            StyleCode = "BM5879-464", Quantity = 10, UnitPriceDap = 8, UnitPriceCmt = 4 }] };
        Assert.False((await Customs(db).ReconcileAsync(declaration, order.Id)).IsFullyMatched);
        declaration.Items[0].UnitPriceCmt = 0;
        declaration.TotalDap = 81;
        Assert.False((await Customs(db).ReconcileAsync(declaration, order.Id)).IsFullyMatched);
        declaration.TotalDap = 80;
        Assert.True((await Customs(db).ReconcileAsync(declaration, order.Id)).IsFullyMatched);
    }

    [Fact]
    public async Task FailedWorkbookGeneration_RollsBackOrdersAndSequence()
    {
        using var db = MemoryDb();
        var folder = new MasterDataFolder { Name = "Contract A", ContractNo = "A" };
        db.Add(folder);
        await db.SaveChangesAsync();
        db.Add(new ProductMaster { StyleCode = "BM5879-464", Description = "Test", UnitPriceDAP = 8, UnitPriceCMT = 3, FolderId = folder.Id });
        await db.SaveChangesAsync();
        var sequence = new SequenceService(db, NullLogger<SequenceService>.Instance);
        // Force a failure after database persistence, while writing response headers.
        var controller = new ShipmentsController(db, new ExcelImportExportService(db, NullLogger<ExcelImportExportService>.Instance),
            sequence, NullLogger<ShipmentsController>.Instance);
        var response = await controller.ExportExcel(new() { ContractFolderId = folder.Id, InvoiceNo = "INV999", CustomerName = "Test", StartInvoiceNumber = 999,
            Items = [new() { StyleCode = "BM5879-464", Quantity = 25, UnitPriceDAP = 8, UnitPriceCMT = 3 }] });
        Assert.IsType<ObjectResult>(response);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.ShipmentOrders.ToListAsync());
        Assert.Equal(233, await sequence.GetCurrentNextNumberAsync());
    }

    [Fact]
    public async Task SequenceNeverMovesBackward_AndConcurrentAllocationsAreDistinct()
    {
        var path = Path.Combine(Path.GetTempPath(), $"xnk-sequence-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        try
        {
            using (var db = new AppDbContext(options)) { await db.Database.EnsureCreatedAsync(); }
            var values = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(async () => {
                using var db = new AppDbContext(options);
                return (await new SequenceService(db, NullLogger<SequenceService>.Instance).GetNextSequenceNumbersAsync())[0];
            })));
            Assert.Equal(4, values.Distinct().Count());
            using var check = new AppDbContext(options);
            var service = new SequenceService(check, NullLogger<SequenceService>.Instance);
            await service.SetNextSequenceNumberAsync(1);
            Assert.Equal(values.Max() + 1, await service.GetCurrentNextNumberAsync());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PartnerSequenceConcurrentReservations_AreAtomicAndDistinct()
    {
        var path = Path.Combine(Path.GetTempPath(), $"xnk-partner-sequence-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False;Default Timeout=30").Options;
        try
        {
            int folderId;
            using (var db = new AppDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                var folder = new MasterDataFolder { Name = "Concurrent partner", CurrentSequenceNumber = 233 };
                db.Add(folder); await db.SaveChangesAsync(); folderId = folder.Id;
            }
            var values = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
            {
                using var db = new AppDbContext(options);
                return (await new SequenceService(db, NullLogger<SequenceService>.Instance)
                    .ReservePartnerSequenceNumbersAsync(folderId))[0];
            })));
            Assert.Equal(6, values.Distinct().Count());
            Assert.Equal(Enumerable.Range(233, 6), values.OrderBy(x => x));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SizeBreakdownMismatch_IsRejectedWithSpecificMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ShipmentSizeBreakdownValidator.Validate([
            new CreateShipmentItemDto { StyleCode = "STYLE-1", Quantity = 24, SizeBreakdownJson = "{\"36\":10,\"37\":12}" }
        ]));
        Assert.Contains("22 đôi", ex.Message);
        Assert.Contains("24 đôi", ex.Message);
    }

    [Fact]
    public async Task AdminUnlockClearedShipment_WritesAuditAndReturnsOrderToEditableStatus()
    {
        using var db = MemoryDb();
        var order = new ShipmentOrder { InvoiceNo = "AMA-1", CustomerName = "Test", Status = ShipmentStatus.Cleared, IsLocked = true };
        db.Add(order); await db.SaveChangesAsync();
        var controller = new ShipmentsController(db, new FakeExcelService(), new FakeSequenceService(), NullLogger<ShipmentsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, "admin-id"), new Claim(ClaimTypes.Name, "Admin User"), new Claim(ClaimTypes.Role, "Admin")
                    ], "test"))
                }
            }
        };
        Assert.IsType<OkObjectResult>(await controller.UnlockClearedShipment(order.Id, new("Khai bổ sung AMA sau thông quan")));
        Assert.False(order.IsLocked);
        Assert.Equal(ShipmentStatus.Exported, order.Status);
        var audit = Assert.Single(await db.ShipmentUnlockAudits.ToListAsync());
        Assert.Equal("admin-id", audit.UnlockedByUserId);
        Assert.Contains("AMA", audit.Reason);
    }

    [Fact]
    public async Task FreshMigration_IsRepeatable_AndCreatesContractConstraints()
    {
        var path = Path.Combine(Path.GetTempPath(), $"xnk-migration-{Guid.NewGuid():N}.db");
        try
        {
            using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);
            await DbInitializer.InitializeAsync(db, NullLogger.Instance);
            await DbInitializer.InitializeAsync(db, NullLogger.Instance);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var folder = new MasterDataFolder { Name = "Contract" };
            db.Add(folder); await db.SaveChangesAsync();
            db.Add(new ShipmentOrder { InvoiceNo = "MIGRATION", CustomerName = "Test", ContractFolderId = folder.Id });
            await db.SaveChangesAsync();
            var folders = new MasterDataFolderService(db, NullLogger<MasterDataFolderService>.Instance);
            await Assert.ThrowsAsync<InvalidOperationException>(() => folders.DeleteFolderAsync(folder.Id, true));
        }
        finally { File.Delete(path); }
    }
}
