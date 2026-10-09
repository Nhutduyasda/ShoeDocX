using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Controllers;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Tests;

public sealed class CustomsMismatchDetailsTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly CustomsDeclarationService _service;

    public CustomsMismatchDetailsTests()
    {
        _connection.Open();
        _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _service = new(_db, NullLogger<CustomsDeclarationService>.Instance, new DummyWebHostEnvironment());
    }

    // Reproduce the native layout without checking a private declaration into Git.
    private static XLWorkbook NativeWorkbook(bool american = false, bool numeric = false)
    {
        var book = new XLWorkbook();
        var sheet = book.AddWorksheet("TKX");
        sheet.Cell("C4").Value = "Số tờ khai";
        sheet.Cell("E4").Value = "308987873850";
        sheet.Cell("C8").Value = "Ngày đăng ký";
        sheet.Cell("F8").Value = "23/09/2026 17:24:56";
        sheet.Cell("C41").Value = "Tổng trọng lượng hàng (Gross)";
        if (numeric) sheet.Cell("H41").Value = 1199m;
        else sheet.Cell("H41").Value = american ? "1,199" : "1.199";
        sheet.Cell("L49").Value = "Số hóa đơn";
        sheet.Cell("P49").Value = "A";
        sheet.Cell("Q49").Value = "-";
        sheet.Cell("R49").Value = "KMIII-NEW2026-0078";
        sheet.Cell("L53").Value = "Tổng trị giá hóa đơn";
        sheet.Cell("Q53").Value = "DAP - USD";
        if (numeric) sheet.Cell("U53").Value = 27886m;
        else sheet.Cell("U53").Value = american ? "27,886" : "27.886";
        var codes = new[] { "YM6101-013", "BM5879-464", "YL8534-088" };
        var quantities = new[] { 817, 1510, 2045 };
        var prices = new[] { 7m, 5.2m, 7m };
        var amounts = new[] { 5719m, 7852m, 14315m };
        var rows = new[] { 155, 179, 212 };
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            sheet.Cell(row, 3).Value = $"<0{index + 1}>";
            sheet.Cell(row + 3, 3).Value = "Mô tả hàng hóa";
            sheet.Cell(row + 3, 6).Value = $"{codes[index]} (PO)#&Mũ giày (đơn giá gia công: 3.53usd/đôi).#&VN";
            sheet.Cell(row + 6, 15).Value = "Số lượng (1)";
            sheet.Cell(row + 6, 25).Value = "PR";
            sheet.Cell(row + 8, 3).Value = "Trị giá hóa đơn";
            sheet.Cell(row + 8, 15).Value = "Đơn giá hóa đơn";
            if (numeric)
            {
                sheet.Cell(row + 6, 17).Value = quantities[index];
                sheet.Cell(row + 8, 6).Value = amounts[index];
                sheet.Cell(row + 8, 18).Value = prices[index];
            }
            else
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo(american ? "en-US" : "vi-VN");
                sheet.Cell(row + 6, 17).Value = quantities[index].ToString("#,##0", culture);
                sheet.Cell(row + 8, 6).Value = amounts[index].ToString("#,##0", culture);
                sheet.Cell(row + 8, 18).Value = prices[index].ToString("0.#", culture);
            }
        }
        return book;
    }

    private static MemoryStream Stream(XLWorkbook book)
    {
        var stream = new MemoryStream();
        book.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void IntegerGroupedAmounts_ParseAllLinesAndTotal(bool american, bool numeric)
    {
        using var book = NativeWorkbook(american, numeric);
        using var stream = Stream(book);
        var parsed = _service.ParseDeclarationFile(stream, "native.xlsx");
        Assert.Equal(3, parsed.Items.Count);
        Assert.Equal(4372, parsed.TotalItemQuantity);
        Assert.Equal(27886m, parsed.TotalDap);
        Assert.Equal(1199m, parsed.GrossWeight);
        Assert.Equal(5719m, parsed.Items[0].AmountDap);
        Assert.Equal(3.53m, parsed.Items[0].UnitPriceCmt);
    }

    [Fact]
    public void NumericCellsAndHighPrecisionUnitPrices_AreNotReinterpretedAsThousands()
    {
        using var book = NativeWorkbook(numeric: true);
        book.Worksheet(1).Cell("R163").Value = 7.1234m;
        book.Worksheet(1).Cell("F163").Value = 5819.819m;
        using var stream = Stream(book);
        var parsed = _service.ParseDeclarationFile(stream, "numeric.xlsx");
        Assert.Equal(7.1234m, parsed.Items[0].UnitPriceDap);
        Assert.Equal(5819.819m, parsed.Items[0].AmountDap);
    }

    [Fact]
    public async Task WrongInvoice_ReturnsFullComparisonAndExplicitTotalDifferences()
    {
        var order = new ShipmentOrder { InvoiceNo = "KMIII-NEW2026-0079", CustomerName = "Test",
            Items = [new() { StyleCode = "BM5879-464", Quantity = 514, UnitPriceDAP = 5.2m, UnitPriceCMT = 3.57m },
                new() { StyleCode = "BI5879-464", Quantity = 133, UnitPriceDAP = 5.2m, UnitPriceCMT = 3.57m },
                new() { StyleCode = "BI5879-063", Quantity = 421, UnitPriceDAP = 5.2m, UnitPriceCMT = 3.57m },
                new() { StyleCode = "BM5879-063", Quantity = 1705, UnitPriceDAP = 5.2m, UnitPriceCMT = 3.57m }] };
        _db.Add(order);
        await _db.SaveChangesAsync();
        using var book = NativeWorkbook();
        using var stream = Stream(book);
        var controller = Controller();
        var file = new FormFile(stream, 0, stream.Length, "file", "native.xlsx");
        var response = await controller.ParseAndCompare(file, order.Id);
        var result = Assert.IsType<CustomsReconciliationResultDto>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.True(result.IsInvoiceMismatch);
        Assert.False(result.IsFullyMatched);
        Assert.Contains("0078", result.InvoiceMismatchWarning);
        Assert.Contains("0079", result.InvoiceMismatchWarning);
        Assert.Equal(6, result.ComparisonRows.Count);
        Assert.Contains(result.Discrepancies, d => d.Contains("YM6101-013") && d.Contains("không có trong Invoice"));
        Assert.Contains(result.Discrepancies, d => d.Contains("BI5879-063") && d.Contains("không tìm thấy trên tờ khai"));
        Assert.Contains(result.Discrepancies, d => d.Contains("Tổng DAP") && d.Contains("27.886,00") && d.Contains("14.419,60") && d.Contains("13.466,40"));
        stream.Position = 0;
        var rejected = Assert.IsType<BadRequestObjectResult>(await controller.ConfirmSync(order.Id,
            new() { IsFullyMatched = true }, file));
        Assert.Equal(400, rejected.StatusCode);
        Assert.False(order.IsLocked);
        Assert.NotEqual(ShipmentStatus.Cleared, order.Status);
        Assert.Null(order.CustomsAttachmentFilePath);
    }

    [Fact]
    public async Task CmtOnlyMismatch_IsReportedAsCmtWithoutFalseDapWarning()
    {
        var order = new ShipmentOrder { InvoiceNo = "INV", CustomerName = "Test", Items =
            [new() { StyleCode = "STYLE-01", Quantity = 10, UnitPriceDAP = 5.2m, UnitPriceCMT = 3.5m }] };
        _db.Add(order);
        await _db.SaveChangesAsync();
        var result = await _service.ReconcileAsync(new() { InvoiceNo = "INV", TotalDap = 52m, TotalCmt = 36m,
            Items = [new() { StyleCode = "STYLE-01", Quantity = 10, UnitPriceDap = 5.2m, UnitPriceCmt = 3.6m, HasCmt = true }] }, order.Id);
        Assert.False(result.IsFullyMatched);
        Assert.Contains("CMT", Assert.Single(result.ComparisonRows).StatusText);
        Assert.DoesNotContain(result.Discrepancies, d => d.Contains("DAP"));
        Assert.Contains(result.Discrepancies, d => d.Contains("Tổng CMT") && d.Contains("36,00") && d.Contains("35,00") && d.Contains("1,00"));
    }

    [Fact]
    public async Task IncorrectAmount_Returns400WithCodePositionAndActualArithmetic()
    {
        using var book = NativeWorkbook();
        book.Worksheet(1).Cell("F163").Value = "5.720";
        using var stream = Stream(book);
        var response = await Controller().ParseAndCompare(new FormFile(stream, 0, stream.Length, "file", "bad.xlsx"));
        var error = JsonSerializer.SerializeToElement(Assert.IsType<BadRequestObjectResult>(response.Result).Value,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal("CUSTOMS_PARSE_VALIDATION_FAILED", error.GetProperty("code").GetString());
        var issue = error.GetProperty("details")[0];
        Assert.Equal("YM6101-013", issue.GetProperty("styleCode").GetString());
        Assert.Equal(163, issue.GetProperty("row").GetInt32());
        Assert.Equal(6, issue.GetProperty("column").GetInt32());
        Assert.Equal(817, issue.GetProperty("quantity").GetInt32());
        Assert.Equal(7m, issue.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(5720m, issue.GetProperty("actualAmount").GetDecimal());
        Assert.Equal(5719m, issue.GetProperty("expectedAmount").GetDecimal());
    }

    [Fact]
    public void ConflictingNumberConventions_AreReportedRatherThanGuessed()
    {
        using var book = NativeWorkbook();
        book.Worksheet(1).Cell("R163").Value = "7.0"; // Conflicts with labelled 5,2 and grouped quantities.
        using var stream = Stream(book);
        var error = Assert.Throws<CustomsParseException>(() => _service.ParseDeclarationFile(stream, "ambiguous.xlsx"));
        Assert.Equal("numberFormat", Assert.Single(error.Details).Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveDeclaredAmount_IsRejectedRatherThanRecalculated(int amount)
    {
        using var book = NativeWorkbook();
        book.Worksheet(1).Cell("F163").Value = amount;
        using var stream = Stream(book);
        var error = Assert.Throws<CustomsParseException>(() => _service.ParseDeclarationFile(stream, "invalid.xlsx"));
        var issue = Assert.Single(error.Details);
        Assert.Equal("amountDap", issue.Field);
        Assert.Equal(163, issue.Row);
        Assert.Equal(6, issue.Column);
        Assert.Equal((decimal)amount, issue.ActualAmount);
    }

    private CustomsController Controller() => new(_service, _db, NullLogger<CustomsController>.Instance)
    { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }
}
