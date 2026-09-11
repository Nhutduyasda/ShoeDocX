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
using Xunit;

namespace ShoeExportInvoice.Tests;

public class FakeSequenceService : ISequenceService
{
    public int SetNextSequenceNumberCallCount { get; private set; }

    public Task<int[]> GetNextSequenceNumbersAsync(int count = 1) => Task.FromResult(new[] { 233, 234 });
    public Task SetNextSequenceNumberAsync(int nextNumber)
    {
        SetNextSequenceNumberCallCount++;
        return Task.CompletedTask;
    }
    public Task<int> GetCurrentNextNumberAsync() => Task.FromResult(233);
    public string ToInvoiceNo(int sequenceNumber) => $"KMHD-NEW2026-0{sequenceNumber}";
    public string ToFileName(int sequenceNumber) => $"KM3-26-DH{sequenceNumber}.xlsx";
    public int? ExtractSequenceNumber(string invoiceNo) => 233;
}

public class FakeExcelService : IExcelImportExportService
{
    public int ExportCallCount { get; private set; }

    public Task<ImportPreviewResponseDto> PreviewProductMastersFromExcelAsync(Stream fileStream, int? folderId = null) => throw new NotImplementedException();
    public Task<ImportResultDto> ImportProductMastersFromExcelAsync(Stream fileStream, bool updateExisting = true, int? folderId = null, ColumnMappingOverrideDto? mappingOverride = null) => throw new NotImplementedException();
    public byte[] GenerateProductMasterTemplate() => Array.Empty<byte>();
    public Task<byte[]> ExportProductMastersToExcelAsync() => Task.FromResult(Array.Empty<byte>());
    public Task<byte[]> ExportShipmentToExcelAsync(ShipmentExportModel model) => Task.FromResult(Array.Empty<byte>());
    public PklPreviewResponseDto CalculatePklBreakdown(CreateShipmentRequestDto request) => new();
    public Task<byte[]> ExportShipmentMultiSheetExcelAsync(CreateShipmentRequestDto request)
    {
        ExportCallCount++;
        return Task.FromResult(new byte[] { 1, 2, 3 });
    }
    public Task<byte[]> ExportSplitToZipAsync(CreateShipmentRequestDto goRequest, string goFileName, CreateShipmentRequestDto standardRequest, string standardFileName)
    {
        ExportCallCount++;
        return Task.FromResult(new byte[] { 1, 2, 3 });
    }
}

public class ExportMasterDataValidationGuardTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public ExportMasterDataValidationGuardTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new AppDbContext(_dbOptions);
        context.Database.EnsureCreated();

        // Seed an existing product in ProductMasters
        context.ProductMasters.Add(new ProductMaster
        {
            Id = 1,
            StyleCode = "42072-030",
            Description = "Giày thể thao mẫu chuẩn",
            UnitPriceCMT = 2.5m,
            UnitPriceDAP = 20.0m,
            HsCode = "64041990",
            Unit = "đôi",
            PairPerCarton = 12,
            CreatedAt = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task ExportExcel_WhenStyleCodeNotInMasterData_ShouldReturn400BadRequestWithMissingCodes()
    {
        using var context = new AppDbContext(_dbOptions);
        var fakeExcelService = new FakeExcelService();
        var fakeSeqService = new FakeSequenceService();

        var controller = new ShipmentsController(
            context,
            fakeExcelService,
            fakeSeqService,
            NullLogger<ShipmentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var request = new CreateShipmentRequestDto
        {
            InvoiceNo = "KMHD-NEW2026-0233",
            Items = new List<CreateShipmentItemDto>
            {
                new() { StyleCode = "42072-030", Quantity = 24 }, // Có trong Master Data
                new() { StyleCode = "42072-999", Quantity = 12 }  // Chưa có trong Master Data
            }
        };

        var result = await controller.ExportExcel(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);

        // Kiểm tra cấu trúc JSON phản hồi
        dynamic responseValue = badRequest.Value!;
        var successProp = responseValue.GetType().GetProperty("success")?.GetValue(responseValue, null);
        var messageProp = responseValue.GetType().GetProperty("message")?.GetValue(responseValue, null) as string;
        var missingCodesProp = responseValue.GetType().GetProperty("missingCodes")?.GetValue(responseValue, null) as System.Collections.IEnumerable;

        Assert.False((bool)successProp);
        Assert.NotNull(messageProp);
        Assert.Contains("42072-999", messageProp);

        var missingList = missingCodesProp?.Cast<string>().ToList();
        Assert.NotNull(missingList);
        Assert.Single(missingList);
        Assert.Equal("42072-999", missingList[0]);

        // Đảm bảo không gọi service xuất file và không tăng sequence
        Assert.Equal(0, fakeExcelService.ExportCallCount);
        Assert.Equal(0, fakeSeqService.SetNextSequenceNumberCallCount);
    }

    [Fact]
    public async Task ExportExcel_WhenAllStyleCodesExistIncludingGoSuffix_ShouldPassGuard()
    {
        using var context = new AppDbContext(_dbOptions);
        var fakeExcelService = new FakeExcelService();
        var fakeSeqService = new FakeSequenceService();

        var controller = new ShipmentsController(
            context,
            fakeExcelService,
            fakeSeqService,
            NullLogger<ShipmentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var request = new CreateShipmentRequestDto
        {
            InvoiceNo = "KMHD-NEW2026-0233",
            Items = new List<CreateShipmentItemDto>
            {
                new() { StyleCode = "42072-030", Quantity = 24, ProcessType = ProcessType.Standard },
                new() { StyleCode = "42072-030.G", Quantity = 12, ProcessType = ProcessType.Standard } // Mã Gò dựa trên mã gốc
            }
        };

        var result = await controller.ExportExcel(request);

        // Không bị chặn 400 Bad Request
        Assert.IsNotType<BadRequestObjectResult>(result);
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("KM3-26-DH233.xlsx", fileResult.FileDownloadName);
        Assert.Equal(1, fakeExcelService.ExportCallCount);
    }

    [Fact]
    public async Task ExcelImportExportService_ThrowsInvalidOperationException_WhenStyleCodeMissingInMaster()
    {
        using var context = new AppDbContext(_dbOptions);
        var service = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance);

        var request = new CreateShipmentRequestDto
        {
            InvoiceNo = "KMHD-NEW2026-0233",
            Items = new List<CreateShipmentItemDto>
            {
                new() { StyleCode = "UNREGISTERED-CODE-999", Quantity = 12 }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExportShipmentMultiSheetExcelAsync(request));

        Assert.Contains("UNREGISTERED-CODE-999", ex.Message);
    }
}
