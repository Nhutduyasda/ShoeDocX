using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Tests;

public sealed class FolderPackingPolicyTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    public FolderPackingPolicyTests()
    {
        _connection.Open();
        _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task CreateUpdateAndBulkMove_AlwaysUseAssignedFolderPacking()
    {
        var folder = new MasterDataFolder { Name = "NEW", DefaultPairsPerCarton = 24 };
        var second = new MasterDataFolder { Name = "OTHER", DefaultPairsPerCarton = 18 };
        _db.AddRange(folder, second);
        await _db.SaveChangesAsync();
        var service = new ProductMasterService(_db);
        var created = await service.CreateAsync(new() { StyleCode = "STYLE-01", Description = "Test", FolderId = folder.Id, PairPerCarton = 12 });
        Assert.Equal(24, created.PairPerCarton);
        var edited = await service.UpdateAsync(created.Id, new() { StyleCode = "STYLE-01", Description = "Test", PairPerCarton = 12 });
        Assert.Equal(24, edited!.PairPerCarton);
        await service.BulkMoveProductsAsync([created.Id], second.Id);
        Assert.Equal(18, (await service.GetByIdAsync(created.Id))!.PairPerCarton);
    }

    [Fact]
    public async Task ChangingFolderPacking_UpdatesUnloadedProductsInSameSave()
    {
        var folder = new MasterDataFolder { Name = "NEW", DefaultPairsPerCarton = 24 };
        var product = new ProductMaster { StyleCode = "STYLE-01", Description = "Test", Folder = folder, PairPerCarton = 12 };
        _db.Add(product);
        await _db.SaveChangesAsync();
        Assert.Equal(24, product.PairPerCarton);
        _db.ChangeTracker.Clear();
        var loadedFolder = await _db.MasterDataFolders.SingleAsync();
        loadedFolder.DefaultPairsPerCarton = 30;
        await _db.SaveChangesAsync();
        Assert.Equal(30, (await _db.ProductMasters.AsNoTracking().SingleAsync()).PairPerCarton);
    }

    [Fact]
    public async Task RepairLegacyMismatch_IsIdempotentAndLeavesOtherFieldsUnchanged()
    {
        var folder = new MasterDataFolder { Name = "NEW", DefaultPairsPerCarton = 24 };
        var product = new ProductMaster { StyleCode = "STYLE-01", Description = "Keep", Folder = folder, PairPerCarton = 24, UnitPriceDAP = 7.1234m };
        _db.Add(product);
        await _db.SaveChangesAsync();
        await _db.ProductMasters.ExecuteUpdateAsync(s => s.SetProperty(p => p.PairPerCarton, 12));
        _db.ChangeTracker.Clear();
        Assert.Equal(1, await _db.RepairFolderPackingAsync());
        Assert.Equal(0, await _db.RepairFolderPackingAsync());
        var repaired = await _db.ProductMasters.SingleAsync();
        Assert.Equal(24, repaired.PairPerCarton);
        Assert.Equal("Keep", repaired.Description);
        Assert.Equal(7.1234m, repaired.UnitPriceDAP);
    }

    [Fact]
    public void SynchronousSave_EnforcesPackingButUnassignedProductsRemainEditable()
    {
        var folder = new MasterDataFolder { Name = "NEW", DefaultPairsPerCarton = 24 };
        var assigned = new ProductMaster { StyleCode = "STYLE-01", Description = "Test", Folder = folder, PairPerCarton = 12 };
        var unassigned = new ProductMaster { StyleCode = "STYLE-02", Description = "Test", PairPerCarton = 18 };
        _db.AddRange(assigned, unassigned);
        _db.SaveChanges();
        Assert.Equal(24, assigned.PairPerCarton);
        Assert.Equal(18, unassigned.PairPerCarton);
    }

    [Fact]
    public async Task ExcelImport_CannotOverrideFolderPackingEvenWithMappedColumn()
    {
        var folder = new MasterDataFolder { Name = "NEW", DefaultPairsPerCarton = 24 };
        _db.Add(folder);
        await _db.SaveChangesAsync();
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Products");
        var headers = new[] { "Style Code", "CMT", "DAP", "Description", "Pair/CTN" };
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        sheet.Cell(2, 1).Value = "STYLE-01";
        sheet.Cell(2, 2).Value = 3m;
        sheet.Cell(2, 3).Value = 7m;
        sheet.Cell(2, 4).Value = "Test";
        sheet.Cell(2, 5).Value = 12;
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        stream.Position = 0;
        var service = new ExcelImportExportService(_db, NullLogger<ExcelImportExportService>.Instance);
        var result = await service.ImportProductMastersFromExcelAsync(stream, folderId: folder.Id,
            mappingOverride: new() { StyleCodeCol = 1, CmtPriceCol = 2, DapPriceCol = 3, DescriptionCol = 4, PairsPerCartonCol = 5 });
        Assert.Empty(result.Errors);
        Assert.Equal(24, (await _db.ProductMasters.SingleAsync()).PairPerCarton);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }
}
