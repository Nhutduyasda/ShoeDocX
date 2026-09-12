using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Services;
using Xunit;

namespace ShoeExportInvoice.Tests;

public class ProductMasterExcelImportTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public ProductMasterExcelImportTests()
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
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Import_NoHeaderFile_ShouldReadFromRow1AndHandleGoOptionCorrectly()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        // Dòng 1: Mã chuẩn Thành hình
        ws.Cell(1, 1).SetValue("42072-030");
        ws.Cell(1, 2).SetValue(" (KM3.PO5.26)");
        ws.Cell(1, 3).SetValue("42072-030 (KM3.PO5.26)");
        ws.Cell(1, 4).SetValue(3.2m);
        ws.Cell(1, 5).SetValue(8.2m);
        ws.Cell(1, 6).SetValue("Giày thể thao nữ buộc dây");
        ws.Cell(1, 7).SetValue("PR");
        ws.Cell(1, 8).SetValue("64041990");

        // Dòng 2: Mã chuẩn Thành hình cho 45428-2LX
        ws.Cell(2, 1).SetValue("45428-2LX");
        ws.Cell(2, 2).SetValue(" (KM3.PO5.26)");
        ws.Cell(2, 3).SetValue("45428-2LX (KM3.PO5.26)");
        ws.Cell(2, 4).SetValue(2.8m);
        ws.Cell(2, 5).SetValue(21.0m);
        ws.Cell(2, 6).SetValue("Giày chạy bộ nam cổ thấp");
        ws.Cell(2, 7).SetValue("đôi");
        ws.Cell(2, 8).SetValue("64041990");

        // Dòng 3: Mã Gò không may có đuôi .G cho 45428-2LX
        ws.Cell(3, 1).SetValue("45428-2LX.G");
        ws.Cell(3, 2).SetValue(" (KM3.PO5.26)");
        ws.Cell(3, 3).SetValue("45428-2LX.G (KM3.PO5.26)");
        ws.Cell(3, 4).SetValue(1.16m);
        ws.Cell(3, 5).SetValue(7.2m);
        ws.Cell(3, 6).SetValue("Giày chạy bộ nam cổ thấp (Gò)");
        ws.Cell(3, 7).SetValue("PR");
        ws.Cell(3, 8).SetValue("64041990");

        // Dòng 4: Mã Gò đứng một mình (chưa có mã chuẩn trước đó)
        ws.Cell(4, 1).SetValue("51200-1BK.G");
        ws.Cell(4, 2).SetValue(" (KM3.PO5.26)");
        ws.Cell(4, 3).SetValue("51200-1BK.G (KM3.PO5.26)");
        ws.Cell(4, 4).SetValue(1.95m);
        ws.Cell(4, 5).SetValue(15.2m);
        ws.Cell(4, 6).SetValue("Giày búp bê nữ quai dán");
        ws.Cell(4, 7).SetValue("PR");
        ws.Cell(4, 8).SetValue("64041990");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        using var context = new AppDbContext(_dbOptions);
        var service = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance);

        var result = await service.ImportProductMastersFromExcelAsync(stream, updateExisting: true);

        Assert.True(result.Success);
        Assert.Equal(4, result.TotalRowsRead);
        Assert.Equal(4, result.ImportedCount);
        Assert.Equal(3, result.CreatedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Empty(result.Errors);
        Assert.Contains("Đã import thành công 4 mã sản phẩm", result.Message);

        var p1 = await context.ProductMasters.FirstAsync(p => p.StyleCode == "42072-030");
        Assert.Equal(3.2m, p1.UnitPriceCMT);
        Assert.Equal(8.2m, p1.UnitPriceDAP);
        Assert.False(p1.HasGoOption);

        var p2 = await context.ProductMasters.FirstAsync(p => p.StyleCode == "45428-2LX");
        Assert.Equal(2.8m, p2.UnitPriceCMT);
        Assert.Equal(21.0m, p2.UnitPriceDAP);
        Assert.Equal(1.16m, p2.UnitPriceCMT_Go);
        Assert.Equal(7.2m, p2.UnitPriceDAP_Go);
        Assert.True(p2.HasGoOption);

        var p3 = await context.ProductMasters.FirstAsync(p => p.StyleCode == "51200-1BK");
        Assert.Equal(1.95m, p3.UnitPriceCMT_Go);
        Assert.Equal(15.2m, p3.UnitPriceDAP_Go);
        Assert.True(p3.HasGoOption);
    }

    [Fact]
    public async Task Import_WithHeaderFile_ShouldAutoDetectHeaderAndStartFromRow2()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        ws.Cell(1, 1).SetValue("Mã hình thể");
        ws.Cell(1, 2).SetValue("Đuôi PO");
        ws.Cell(1, 3).SetValue("Mã đầy đủ");
        ws.Cell(1, 4).SetValue("Đơn giá CMT");
        ws.Cell(1, 5).SetValue("Đơn giá DAP");
        ws.Cell(1, 6).SetValue("Mô tả hàng hóa");
        ws.Cell(1, 7).SetValue("ĐVT");
        ws.Cell(1, 8).SetValue("Mã HS");

        ws.Cell(2, 1).SetValue("33109-08A");
        ws.Cell(2, 2).SetValue("(KM3.PO5.26)");
        ws.Cell(2, 3).SetValue("33109-08A (KM3.PO5.26)");
        ws.Cell(2, 4).SetValue(3.10m);
        ws.Cell(2, 5).SetValue(24.50m);
        ws.Cell(2, 6).SetValue("Giày lười nam da tổng hợp");
        ws.Cell(2, 7).SetValue("PR");
        ws.Cell(2, 8).SetValue("64029990");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        using var context = new AppDbContext(_dbOptions);
        var service = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance);

        var result = await service.ImportProductMastersFromExcelAsync(stream, updateExisting: true);

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalRowsRead);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.CreatedCount);
        Assert.Empty(result.Errors);

        var p = await context.ProductMasters.FirstAsync(p => p.StyleCode == "33109-08A");
        Assert.Equal(3.10m, p.UnitPriceCMT);
        Assert.Equal(24.50m, p.UnitPriceDAP);
        Assert.Equal("64029990", p.HsCode);
    }

    [Fact]
    public async Task Import_InvalidNumberFormat_ShouldRecordErrorAndContinue()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        ws.Cell(1, 1).SetValue("42072-030");
        ws.Cell(1, 4).SetValue(2.45m);
        ws.Cell(1, 5).SetValue(18.5m);
        ws.Cell(1, 6).SetValue("Giày mẫu 1");

        ws.Cell(2, 1).SetValue("45428-2LX");
        ws.Cell(2, 4).SetValue("INVALID_PRICE");
        ws.Cell(2, 5).SetValue(20.0m);
        ws.Cell(2, 6).SetValue("Giày mẫu 2");

        ws.Cell(3, 1).SetValue("51200-1BK");
        ws.Cell(3, 4).SetValue(1.95m);
        ws.Cell(3, 5).SetValue(15.2m);
        ws.Cell(3, 6).SetValue("Giày mẫu 3");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        using var context = new AppDbContext(_dbOptions);
        var service = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance);

        var result = await service.ImportProductMastersFromExcelAsync(stream, updateExisting: true);

        Assert.Equal(3, result.TotalRowsRead);
        Assert.Equal(2, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Equal(2, result.Errors[0].RowNumber);
        Assert.Contains("CMT", result.Errors[0].Message);

        Assert.Equal(2, await context.ProductMasters.CountAsync());
    }

    [Fact]
    public async Task Import_ActualWorkbook1File_ShouldSucceedWith28Products()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "Workbook1.xlsx");
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\backend\ShoeExportInvoice.Api\Templates\Workbook1.xlsx"));
        }
        if (!File.Exists(path))
        {
            path = @"C:\Users\nhutd\Documents\antigravity\clever-faraday\backend\ShoeExportInvoice.Api\Templates\Workbook1.xlsx";
        }

        Assert.True(File.Exists(path), $"File Workbook1.xlsx must exist at {path}");

        using var context = new AppDbContext(_dbOptions);
        var service = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance);

        using var fileStream = File.OpenRead(path);
        var result = await service.ImportProductMastersFromExcelAsync(fileStream, updateExisting: true);

        Assert.True(result.Success);
        Assert.Equal(28, result.TotalRowsRead);
        Assert.Equal(28, result.ImportedCount);
        Assert.Empty(result.Errors);
        Assert.Contains("Đã import thành công 28 mã sản phẩm", result.Message);

        var products = await context.ProductMasters.ToListAsync();
        Assert.Equal(28, products.Count);

        // Kiểm tra các mã Gò (.G)
        var goProducts = products.Where(p => p.HasGoOption).ToList();
        Assert.Equal(9, goProducts.Count);

        var sampleProduct = products.First(p => p.StyleCode == "45428-2LX");
        Assert.True(sampleProduct.HasGoOption);
        Assert.Equal("PR", sampleProduct.Unit);
    }

    [Fact]
    public async Task Import_ActualBook1File_AdaptiveParser_ShouldDetectColumnsAndSucceed()
    {
        var path = @"C:\Users\nhutd\Documents\antigravity\clever-faraday\backend\ShoeExportInvoice.Api\Templates\Book1.xlsx";
        Assert.True(File.Exists(path), $"File Book1.xlsx must exist at {path}");

        using var context = new AppDbContext(_dbOptions);
        var service = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance);

        // 1. Kiểm tra Preview & Tự động phát hiện cột
        using (var previewStream = File.OpenRead(path))
        {
            var preview = await service.PreviewProductMastersFromExcelAsync(previewStream);
            Assert.NotNull(preview);
            Assert.Equal(2, preview.StartRowIndex); // Bỏ qua header rác dòng 1, bắt đầu từ dòng 2
            Assert.Equal(1, preview.DetectedMapping.StyleCodeCol); // Cột 1 là StyleCode
            Assert.Equal(4, preview.DetectedMapping.CmtPriceCol);  // Cột 4 là CMT (phát hiện từ header 'CMT')
            Assert.Equal(5, preview.DetectedMapping.DapPriceCol);  // Cột 5 là FOB/DAP (phát hiện từ header 'FOB')
            Assert.Equal(8, preview.DetectedMapping.DescriptionCol); // Cột 8 là Mô tả (văn bản dài nhất chứa từ khóa giày)
            Assert.NotEmpty(preview.PreviewRows);
            Assert.Equal("YL3564-100", preview.PreviewRows[0].StyleCode);
            Assert.Equal(2.32m, preview.PreviewRows[0].UnitPriceCMT);
            Assert.Equal(5.0m, preview.PreviewRows[0].UnitPriceDAP);
        }

        // 2. Kiểm tra Import thực tế
        using (var fileStream = File.OpenRead(path))
        {
            var result = await service.ImportProductMastersFromExcelAsync(fileStream, updateExisting: true);
            Assert.True(result.Success);
            Assert.True(result.ImportedCount > 0);
            Assert.Empty(result.Errors);

            var sample = await context.ProductMasters.FirstOrDefaultAsync(p => p.StyleCode == "YL3564-100");
            Assert.NotNull(sample);
            Assert.Equal(2.32m, sample!.UnitPriceCMT);
            Assert.Equal(5.0m, sample.UnitPriceDAP);
            Assert.Contains("vật liệu dệt", sample.Description.ToLower());
        }
    }

    [Fact]
    public async Task Import_ActualWorkbook1File_AdaptiveParser_ShouldDetectColumnsAndSucceed()
    {
        var path = @"C:\Users\nhutd\Documents\antigravity\clever-faraday\backend\ShoeExportInvoice.Api\Templates\Workbook1.xlsx";
        Assert.True(File.Exists(path), $"File Workbook1.xlsx must exist at {path}");

        using var context = new AppDbContext(_dbOptions);
        var service = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance);

        // 1. Kiểm tra Preview & Tự động phát hiện cột cho file 8 cột không header
        using (var previewStream = File.OpenRead(path))
        {
            var preview = await service.PreviewProductMastersFromExcelAsync(previewStream);
            Assert.NotNull(preview);
            Assert.Equal(1, preview.StartRowIndex); // Dữ liệu bắt đầu ngay dòng 1
            Assert.Equal(1, preview.DetectedMapping.StyleCodeCol);
            Assert.Equal(4, preview.DetectedMapping.CmtPriceCol);
            Assert.Equal(5, preview.DetectedMapping.DapPriceCol);
            Assert.Equal(6, preview.DetectedMapping.DescriptionCol); // File cũ mô tả ở cột 6
            Assert.Equal(7, preview.DetectedMapping.UnitCol);
            Assert.Equal(8, preview.DetectedMapping.HsCodeCol);
        }

        // 2. Kiểm tra Import
        using (var fileStream = File.OpenRead(path))
        {
            var result = await service.ImportProductMastersFromExcelAsync(fileStream, updateExisting: true);
            Assert.True(result.Success);
            Assert.Equal(28, result.ImportedCount);
            Assert.Empty(result.Errors);
        }
    }
}