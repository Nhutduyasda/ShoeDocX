using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;
using Xunit;

namespace ShoeExportInvoice.Tests;

public class MasterDataFolderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public MasterDataFolderTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new AppDbContext(_dbOptions);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateFolder_And_GetTree_ReturnsHierarchicalStructureWithProductCounts()
    {
        using var context = new AppDbContext(_dbOptions);
        var folderService = new MasterDataFolderService(context, NullLogger<MasterDataFolderService>.Instance);

        // 1. Create root folder "Kingmaker III"
        var root1 = await folderService.CreateFolderAsync(new CreateFolderDto
        {
            Name = "Kingmaker III",
            CustomerName = "Kingmaker Ltd",
            ContractNo = "KM3-2026",
            DefaultPairsPerCarton = 12,
            DefaultUnit = "PRS"
        });

        // 2. Create subfolder under Kingmaker III
        var sub1 = await folderService.CreateFolderAsync(new CreateFolderDto
        {
            Name = "Mùa Hè 2026",
            ParentId = root1.Id,
            DefaultPairsPerCarton = 12
        });

        // 3. Create another root folder "Đối tác NewBalance (24 đôi/thùng)"
        var root2 = await folderService.CreateFolderAsync(new CreateFolderDto
        {
            Name = "NewBalance",
            DefaultPairsPerCarton = 24,
            DefaultUnit = "PRS"
        });

        // Add products
        context.ProductMasters.AddRange(
            new ProductMaster { StyleCode = "KM-01", Description = "Shoe 1", FolderId = root1.Id },
            new ProductMaster { StyleCode = "KM-02", Description = "Shoe 2", FolderId = sub1.Id },
            new ProductMaster { StyleCode = "NB-01", Description = "Shoe 3", FolderId = root2.Id }
        );
        await context.SaveChangesAsync();

        // Query Tree
        var tree = await folderService.GetTreeAsync();

        Assert.Equal(2, tree.Count); // 2 root folders

        var kmRoot = tree.First(f => f.Id == root1.Id);
        Assert.Equal("Kingmaker III", kmRoot.Name);
        Assert.Equal(1, kmRoot.ProductCount); // direct products
        Assert.Equal(2, kmRoot.TotalProductCount); // direct + subfolder products
        Assert.Single(kmRoot.Children);
        Assert.Equal("Mùa Hè 2026", kmRoot.Children[0].Name);
        Assert.Equal(1, kmRoot.Children[0].ProductCount);

        var nbRoot = tree.First(f => f.Id == root2.Id);
        Assert.Equal(24, nbRoot.DefaultPairsPerCarton);
        Assert.Equal(1, nbRoot.ProductCount);
        Assert.Equal(1, nbRoot.TotalProductCount);
    }

    [Fact]
    public async Task MoveFolder_ChangesParentSuccessfully_PreventsCircularReference()
    {
        using var context = new AppDbContext(_dbOptions);
        var folderService = new MasterDataFolderService(context, NullLogger<MasterDataFolderService>.Instance);

        var f1 = await folderService.CreateFolderAsync(new CreateFolderDto { Name = "F1" });
        var f2 = await folderService.CreateFolderAsync(new CreateFolderDto { Name = "F2", ParentId = f1.Id });
        var f3 = await folderService.CreateFolderAsync(new CreateFolderDto { Name = "F3", ParentId = f2.Id });

        // Moving F1 into its descendant F3 must throw InvalidOperationException (circular reference guard)
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await folderService.MoveFolderAsync(f1.Id, new MoveFolderDto { TargetParentId = f3.Id });
        });

        // Moving F3 to root (ParentId = null) should succeed
        var success = await folderService.MoveFolderAsync(f3.Id, new MoveFolderDto { TargetParentId = null, DisplayOrder = 0 });
        Assert.True(success);

        var updatedF3 = await folderService.GetByIdAsync(f3.Id);
        Assert.NotNull(updatedF3);
        Assert.Null(updatedF3!.ParentId);
    }

    [Fact]
    public async Task BulkMoveProducts_TransfersProductsBetweenFolders()
    {
        using var context = new AppDbContext(_dbOptions);
        var folderService = new MasterDataFolderService(context, NullLogger<MasterDataFolderService>.Instance);

        var folderA = await folderService.CreateFolderAsync(new CreateFolderDto { Name = "Folder A" });
        var folderB = await folderService.CreateFolderAsync(new CreateFolderDto { Name = "Folder B" });

        var p1 = new ProductMaster { StyleCode = "P1", Description = "Product 1", FolderId = folderA.Id };
        var p2 = new ProductMaster { StyleCode = "P2", Description = "Product 2", FolderId = folderA.Id };
        context.ProductMasters.AddRange(p1, p2);
        await context.SaveChangesAsync();

        var movedCount = await folderService.BulkMoveProductsAsync(new BulkMoveProductsDto
        {
            ProductIds = new List<int> { p1.Id, p2.Id },
            TargetFolderId = folderB.Id
        });

        Assert.Equal(2, movedCount);

        var checkP1 = await context.ProductMasters.FindAsync(p1.Id);
        var checkP2 = await context.ProductMasters.FindAsync(p2.Id);
        Assert.Equal(folderB.Id, checkP1!.FolderId);
        Assert.Equal(folderB.Id, checkP2!.FolderId);
    }

    [Fact]
    public async Task ImportExcel_WithFolderId_AssignsFolderAndInheritsPairsPerCarton()
    {
        using var context = new AppDbContext(_dbOptions);
        var folderService = new MasterDataFolderService(context, NullLogger<MasterDataFolderService>.Instance);
        var excelService = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance);

        // Create partner folder with 24 pairs per carton
        var partnerFolder = await folderService.CreateFolderAsync(new CreateFolderDto
        {
            Name = "Partner 24 Pairs",
            DefaultPairsPerCarton = 24,
            DefaultUnit = "PRS"
        });

        // Generate minimal 8-column Excel file (Workbook1 style, no header)
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        // Row 1: Col 1 = StyleCode, Col 2 = PO, Col 3 = FullCode, Col 4 = CMT, Col 5 = DAP, Col 6 = Desc, Col 7 = Unit, Col 8 = HS
        ws.Cell(1, 1).SetValue("P-TEST-24");
        ws.Cell(1, 2).SetValue("PO-01");
        ws.Cell(1, 3).SetValue("P-TEST-24-FULL");
        ws.Cell(1, 4).SetValue(2.50m);
        ws.Cell(1, 5).SetValue(15.00m);
        ws.Cell(1, 6).SetValue("Giày Thể Thao 24 Prs");
        ws.Cell(1, 7).SetValue("PRS");
        ws.Cell(1, 8).SetValue("6403.99.90");

        // Row 2: .G code (Gò)
        ws.Cell(2, 1).SetValue("P-TEST-24.G");
        ws.Cell(2, 2).SetValue("PO-01");
        ws.Cell(2, 3).SetValue("P-TEST-24-FULL.G");
        ws.Cell(2, 4).SetValue(1.20m);
        ws.Cell(2, 5).SetValue(7.50m);
        ws.Cell(2, 6).SetValue("Giày Thể Thao Gò");
        ws.Cell(2, 7).SetValue("PRS");
        ws.Cell(2, 8).SetValue("6403.99.90");

        using var memoryStream = new MemoryStream();
        wb.SaveAs(memoryStream);
        memoryStream.Position = 0;

        var result = await excelService.ImportProductMastersFromExcelAsync(memoryStream, updateExisting: true, folderId: partnerFolder.Id);

        Assert.True(result.Success);
        Assert.Equal(1, result.CreatedCount); // 1 base style created (Row 2 merged as .G option)

        var product = await context.ProductMasters.FirstOrDefaultAsync(p => p.StyleCode == "P-TEST-24");
        Assert.NotNull(product);
        Assert.Equal(partnerFolder.Id, product!.FolderId);
        Assert.Equal(24, product.PairPerCarton); // Inherited 24 pairs per carton from folder!
        Assert.True(product.HasGoOption);
        Assert.Equal(1.20m, product.UnitPriceCMT_Go);
    }

    [Fact]
    public async Task CreateAndGetFolder_WithDeliveryAddressAndPoSuffix_PersistsCorrectly()
    {
        using var context = new AppDbContext(_dbOptions);
        var folderService = new MasterDataFolderService(context, NullLogger<MasterDataFolderService>.Instance);

        var created = await folderService.CreateFolderAsync(new CreateFolderDto
        {
            Name = "Đối tác B - ABC Footwear",
            CustomerName = "CÔNG TY TNHH ABC FOOTWEAR (VIỆT NAM)",
            DeliveryAddress = "LÔ B, KCN VSIP, TỈNH BÌNH DƯƠNG, VIỆT NAM",
            ContractNo = "ABC-2026/01",
            PoSuffix = "(ABC.PO1.26)",
            DefaultPairsPerCarton = 24,
            DefaultUnit = "đôi"
        });

        Assert.NotNull(created);
        Assert.Equal("LÔ B, KCN VSIP, TỈNH BÌNH DƯƠNG, VIỆT NAM", created.DeliveryAddress);
        Assert.Equal("(ABC.PO1.26)", created.PoSuffix);
        Assert.Equal(24, created.DefaultPairsPerCarton);

        var fetched = await folderService.GetByIdAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.Equal("LÔ B, KCN VSIP, TỈNH BÌNH DƯƠNG, VIỆT NAM", fetched!.DeliveryAddress);
        Assert.Equal("(ABC.PO1.26)", fetched.PoSuffix);

        var tree = await folderService.GetTreeAsync();
        var treeNode = tree.FirstOrDefault(f => f.Id == created.Id);
        Assert.NotNull(treeNode);
        Assert.Equal("LÔ B, KCN VSIP, TỈNH BÌNH DƯƠNG, VIỆT NAM", treeNode!.DeliveryAddress);
        Assert.Equal("(ABC.PO1.26)", treeNode.PoSuffix);
    }

    [Fact]
    public async Task DeleteAllAsync_WithFolderId_DeletesOnlyProductsInTargetFolderAndSubfolders()
    {
        using var context = new AppDbContext(_dbOptions);
        var folderService = new MasterDataFolderService(context, NullLogger<MasterDataFolderService>.Instance);
        var productService = new ProductMasterService(context, NullLogger<ProductMasterService>.Instance);

        // 1. Create Folder A and Subfolder A1
        var folderA = await folderService.CreateFolderAsync(new CreateFolderDto { Name = "Folder A" });
        var folderA1 = await folderService.CreateFolderAsync(new CreateFolderDto { Name = "Folder A1", ParentId = folderA.Id });

        // 2. Create Folder B
        var folderB = await folderService.CreateFolderAsync(new CreateFolderDto { Name = "Folder B" });

        // 3. Add products:
        context.ProductMasters.Add(new ProductMaster { StyleCode = "P1", Description = "Desc 1", FolderId = folderA.Id, UnitPriceCMT = 1, UnitPriceDAP = 2 });
        context.ProductMasters.Add(new ProductMaster { StyleCode = "P2", Description = "Desc 2", FolderId = folderA1.Id, UnitPriceCMT = 1, UnitPriceDAP = 2 });
        context.ProductMasters.Add(new ProductMaster { StyleCode = "P3", Description = "Desc 3", FolderId = folderB.Id, UnitPriceCMT = 1, UnitPriceDAP = 2 });
        context.ProductMasters.Add(new ProductMaster { StyleCode = "P4", Description = "Desc 4", FolderId = null, UnitPriceCMT = 1, UnitPriceDAP = 2 });

        await context.SaveChangesAsync();
        Assert.Equal(4, await context.ProductMasters.CountAsync());

        // 4. Delete products in Folder A (should delete P1 in A and P2 in child A1)
        var deletedCount = await productService.DeleteAllAsync(folderA.Id);

        Assert.Equal(2, deletedCount);

        var remainingProducts = await context.ProductMasters.ToListAsync();
        Assert.Equal(2, remainingProducts.Count);
        Assert.Contains(remainingProducts, p => p.StyleCode == "P3");
        Assert.Contains(remainingProducts, p => p.StyleCode == "P4");

        // Folders themselves should still exist
        Assert.NotNull(await context.MasterDataFolders.FindAsync(folderA.Id));
        Assert.NotNull(await context.MasterDataFolders.FindAsync(folderA1.Id));
        Assert.NotNull(await context.MasterDataFolders.FindAsync(folderB.Id));
    }
}
