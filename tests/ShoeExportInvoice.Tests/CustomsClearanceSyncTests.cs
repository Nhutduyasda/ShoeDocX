using System.IO;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;
using Xunit;

namespace ShoeExportInvoice.Tests;

public class DummyWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "ShoeExportInvoice";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public class CustomsClearanceSyncTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public CustomsClearanceSyncTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
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

    private MemoryStream CreateSampleVnaccsExcelStream()
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("TKX");

        // Header thông tin tờ khai
        ws.Cell("B2").Value = "Số tờ khai:";
        ws.Cell("C2").Value = "308883922820";

        ws.Cell("E2").Value = "Mã loại hình:";
        ws.Cell("F2").Value = "E52";

        ws.Cell("H2").Value = "Mã phân loại kiểm tra:";
        ws.Cell("I2").Value = "1"; // Luồng Xanh

        ws.Cell("B3").Value = "Ngày đăng ký:";
        ws.Cell("C3").Value = "24/08/2026 13:04:12";

        ws.Cell("E3").Value = "Cơ quan Hải quan tiếp nhận tờ khai:";
        ws.Cell("F3").Value = "HQHGCT";

        ws.Cell("B4").Value = "Số hóa đơn:";
        ws.Cell("C4").Value = "KMHD-NEW2026-0219";

        ws.Cell("E4").Value = "Số lượng kiện:";
        ws.Cell("F4").Value = "140 PK";

        ws.Cell("H4").Value = "Tổng trọng lượng hàng (Gross):";
        ws.Cell("I4").Value = "456 KGM";

        ws.Cell("B5").Value = "Tổng trị giá hóa đơn:";
        ws.Cell("C5").Value = "13586.28";

        ws.Cell("E5").Value = "Ký hiệu và số hiệu: TONG TRI GIA GIA CONG: 5044.80 USD";

        // Dòng hàng 1
        ws.Cell("A8").Value = "<01>";
        ws.Cell("B8").Value = "64041990";
        ws.Cell("C8").Value = "40700-007 (KM3.PO5.26)#&Giày nữ vải dệt đế cao su (Đơn giá gia công: 3.00 USD/đôi)";
        ws.Cell("D8").Value = "204";
        ws.Cell("E8").Value = "PRS";
        ws.Cell("F8").Value = "8.10";
        ws.Cell("G8").Value = "1652.40";

        // Dòng hàng 2
        ws.Cell("A10").Value = "<02>";
        ws.Cell("B10").Value = "64041990";
        ws.Cell("C10").Value = "45187-1WQ (KM3.PO5.26)#&Giày thể thao nam vải dệt (Đơn giá gia công: 3.01 USD/đôi)";
        ws.Cell("D10").Value = "480";
        ws.Cell("E10").Value = "PRS";
        ws.Cell("F10").Value = "8.20";
        ws.Cell("G10").Value = "3936.00";

        // Dòng hàng 3
        ws.Cell("A12").Value = "<03>";
        ws.Cell("B12").Value = "64041990";
        ws.Cell("C12").Value = "45428-2LX.G (KM3.PO5.26)#&Giày nam Gò không may (Đơn giá gia công: 3.00 USD/đôi)";
        ws.Cell("D12").Value = "996";
        ws.Cell("E12").Value = "PRS";
        ws.Cell("F12").Value = "8.03";
        ws.Cell("G12").Value = "7997.88";

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void ParseDeclarationFile_ShouldExtractHeaderAndItemsCorrectly()
    {
        using var context = new AppDbContext(_dbOptions);
        var dummyEnv = new DummyWebHostEnvironment();
        var service = new CustomsDeclarationService(context, NullLogger<CustomsDeclarationService>.Instance, dummyEnv);

        using var stream = CreateSampleVnaccsExcelStream();
        var parsed = service.ParseDeclarationFile(stream, "TK 308883922820-219.xlsx");

        Assert.Equal("308883922820", parsed.DeclarationNo);
        Assert.Equal("KMHD-NEW2026-0219", parsed.InvoiceNo);
        Assert.Equal("E52", parsed.CustomsDeclarationType);
        Assert.Equal(1, parsed.CustomsChannel);
        Assert.Equal(140, parsed.PackageQty);
        Assert.Equal(456m, parsed.GrossWeight);
        Assert.Equal(13586.28m, parsed.TotalDap);
        Assert.Equal(5044.80m, parsed.TotalCmt);
        Assert.NotNull(parsed.ClearanceDate);

        // Kiểm tra dòng hàng
        Assert.Equal(3, parsed.Items.Count);

        var item1 = parsed.Items[0];
        Assert.Equal("40700-007", item1.StyleCode);
        Assert.Equal(204, item1.Quantity);
        Assert.Equal(8.10m, item1.UnitPriceDap);
        Assert.Equal(3.00m, item1.UnitPriceCmt);
        Assert.Equal(ProcessType.Standard, item1.ProcessType);

        var item3 = parsed.Items[2];
        Assert.Equal("45428-2LX", item3.StyleCode);
        Assert.Equal(ProcessType.GoKhongMay, item3.ProcessType);
        Assert.Equal(996, item3.Quantity);
    }

    [Fact]
    public async Task ReconcileAsync_WhenDataMatches100Percent_ShouldReturnFullyMatched()
    {
        using var context = new AppDbContext(_dbOptions);
        var dummyEnv = new DummyWebHostEnvironment();
        var service = new CustomsDeclarationService(context, NullLogger<CustomsDeclarationService>.Instance, dummyEnv);

        // Chuẩn bị đơn hàng trong DB trùng khớp 100%
        var order = new ShipmentOrder
        {
            InvoiceNo = "KMHD-NEW2026-0219",
            InvoiceDate = DateTime.UtcNow,
            CustomerName = "Kingmaker III",
            Items = new List<ShipmentOrderItem>
            {
                new() { StyleCode = "40700-007", FullItemCode = "40700-007", Quantity = 204, UnitPriceDAP = 8.10m, UnitPriceCMT = 3.00m },
                new() { StyleCode = "45187-1WQ", FullItemCode = "45187-1WQ", Quantity = 480, UnitPriceDAP = 8.20m, UnitPriceCMT = 3.01m },
                new() { StyleCode = "45428-2LX", FullItemCode = "45428-2LX.G", Quantity = 996, UnitPriceDAP = 8.03m, UnitPriceCMT = 3.00m, ProcessType = ProcessType.GoKhongMay }
            }
        };
        context.ShipmentOrders.Add(order);
        await context.SaveChangesAsync();

        using var stream = CreateSampleVnaccsExcelStream();
        var parsed = service.ParseDeclarationFile(stream, "TK 308883922820-219.xlsx");

        var result = await service.ReconcileAsync(parsed);

        Assert.True(result.IsOrderFound);
        Assert.True(result.IsFullyMatched);
        Assert.Empty(result.Discrepancies);
        Assert.Equal(3, result.ComparisonRows.Count);
        Assert.All(result.ComparisonRows, r => Assert.True(r.IsMatched));
    }

    [Fact]
    public async Task ReconcileAsync_WhenQuantityDiffers_ShouldDetectDiscrepancy()
    {
        using var context = new AppDbContext(_dbOptions);
        var dummyEnv = new DummyWebHostEnvironment();
        var service = new CustomsDeclarationService(context, NullLogger<CustomsDeclarationService>.Instance, dummyEnv);

        // Đơn hàng trong DB có số lượng khác (200 thay vì 204)
        var order = new ShipmentOrder
        {
            InvoiceNo = "KMHD-NEW2026-0219",
            InvoiceDate = DateTime.UtcNow,
            CustomerName = "Kingmaker III",
            Items = new List<ShipmentOrderItem>
            {
                new() { StyleCode = "40700-007", FullItemCode = "40700-007", Quantity = 200, UnitPriceDAP = 8.10m, UnitPriceCMT = 3.00m }
            }
        };
        context.ShipmentOrders.Add(order);
        await context.SaveChangesAsync();

        using var stream = CreateSampleVnaccsExcelStream();
        var parsed = service.ParseDeclarationFile(stream, "TK 308883922820-219.xlsx");

        var result = await service.ReconcileAsync(parsed);

        Assert.True(result.IsOrderFound);
        Assert.False(result.IsFullyMatched);
        Assert.NotEmpty(result.Discrepancies);
    }
}
