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
    public Task<int[]> ReservePartnerSequenceNumbersAsync(int folderId, int count = 1, int? requestedStart = null) =>
        Task.FromResult(Enumerable.Range(requestedStart ?? 233, count).ToArray());
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
    public DocumentPreviewResponseDto CalculateDocumentPreview(CreateShipmentRequestDto request) => new();
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

        context.MasterDataFolders.Add(new MasterDataFolder { Id = 1, Name = "Contract A", ContractNo = "A" });
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
            FolderId = 1,
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
        var fakeSeqService = new SequenceService(context, NullLogger<SequenceService>.Instance);

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
            ContractFolderId = 1,
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
    }

    [Fact]
    public async Task ExportExcel_WhenAllStyleCodesExistIncludingGoSuffix_ShouldPassGuard()
    {
        using var context = new AppDbContext(_dbOptions);
        var fakeExcelService = new FakeExcelService();
        var fakeSeqService = new SequenceService(context, NullLogger<SequenceService>.Instance);

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
            ContractFolderId = 1,
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
        Assert.Equal(234, (await context.MasterDataFolders.FindAsync(1))!.CurrentSequenceNumber);
    }

    [Fact]
    public async Task ExportExcel_UsesSelectedPartnerPatterns_AndAdvancesPartnerSequence()
    {
        using var context = new AppDbContext(_dbOptions);
        var folder = (await context.MasterDataFolders.FindAsync(1))!;
        folder.InvoiceNoPattern = "PARTNER-{SEQ:4}";
        folder.FileNamePattern = "KM3-2026-{SEQ}.xlsx";
        folder.CurrentSequenceNumber = 233;
        await context.SaveChangesAsync();

        var controller = new ShipmentsController(
            context,
            new FakeExcelService(),
            new SequenceService(context, NullLogger<SequenceService>.Instance),
            NullLogger<ShipmentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.ExportExcel(new CreateShipmentRequestDto
        {
            ContractFolderId = 1,
            InvoiceNo = "PARTNER-0233",
            StartInvoiceNumber = 233,
            ContractNo = "A",
            CustomerName = "Partner A",
            Items = new List<CreateShipmentItemDto>
            {
                new() { StyleCode = "42072-030", Quantity = 24, ProcessType = ProcessType.Standard }
            }
        });

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("KM3-2026-233.xlsx", fileResult.FileDownloadName);
        Assert.Equal(234, (await context.MasterDataFolders.FindAsync(1))!.CurrentSequenceNumber);
    }

    [Fact]
    public async Task ExportExcel_WhenUserInputsExplicitSequence_HonorsExplicitSequenceEvenIfCurrentSequenceIsHigher()
    {
        using var context = new AppDbContext(_dbOptions);
        var folder = (await context.MasterDataFolders.FindAsync(1))!;
        folder.InvoiceNoPattern = "KMHD-NEW2026-{SEQ:4}";
        folder.FileNamePattern = "KM3-26-DH{SEQ}.xlsx";
        folder.CurrentSequenceNumber = 240; // Database sequence counter is already at 240
        await context.SaveChangesAsync();

        var controller = new ShipmentsController(
            context,
            new FakeExcelService(),
            new SequenceService(context, NullLogger<SequenceService>.Instance),
            NullLogger<ShipmentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // User explicitly enters invoice sequence 204 (shorthand "204")
        var result = await controller.ExportExcel(new CreateShipmentRequestDto
        {
            ContractFolderId = 1,
            InvoiceNo = "204",
            StartInvoiceNumber = 204,
            ContractNo = "KM-01",
            CustomerName = "Kingmaker",
            Items = new List<CreateShipmentItemDto>
            {
                new() { StyleCode = "42072-030", Quantity = 24, ProcessType = ProcessType.Standard }
            }
        });

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("KM3-26-DH204.xlsx", fileResult.FileDownloadName);

        // Header check
        Assert.True(controller.Response.Headers.TryGetValue("X-Export-Info", out var headerVal));
        var exportInfo = System.Text.Json.JsonSerializer.Deserialize<ExportResultDto>(headerVal!, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(exportInfo);
        Assert.Equal("KM3-26-DH204.xlsx", exportInfo.SingleFileName);
        Assert.Equal("KMHD-NEW2026-0204", exportInfo.SingleInvoiceNo);

        // Counter in DB should remain 240 (never moved backward, but didn't force 241)
        var updatedFolder = await context.MasterDataFolders.FindAsync(1);
        Assert.Equal(240, updatedFolder!.CurrentSequenceNumber);
    }

    [Fact]
    public async Task ReservePartnerSequenceNumbersAsync_StrictlyHonorsRequestedStart()
    {
        using var context = new AppDbContext(_dbOptions);
        var service = new SequenceService(context, NullLogger<SequenceService>.Instance);

        var folder = (await context.MasterDataFolders.FindAsync(1))!;
        folder.CurrentSequenceNumber = 500;
        await context.SaveChangesAsync();

        // 1. Requested start 204 when DB is 500 -> must return [204]
        var reserved = await service.ReservePartnerSequenceNumbersAsync(1, 1, 204);
        Assert.Equal(new[] { 204 }, reserved);

        // DB remains 500
        var checkFolder = await context.MasterDataFolders.FindAsync(1);
        Assert.Equal(500, checkFolder!.CurrentSequenceNumber);

        // 2. Requested start 600 (higher than DB 500) -> must return [600, 601], and advance DB to 602
        var reservedAdvance = await service.ReservePartnerSequenceNumbersAsync(1, 2, 600);
        Assert.Equal(new[] { 600, 601 }, reservedAdvance);

        checkFolder = await context.MasterDataFolders.FindAsync(1);
        Assert.Equal(602, checkFolder!.CurrentSequenceNumber);

        // 3. Automatic reservation (requestedStart is null) -> continues from 602 -> returns [602], advances DB to 603
        var reservedAuto = await service.ReservePartnerSequenceNumbersAsync(1, 1);
        Assert.Equal(new[] { 602 }, reservedAuto);

        checkFolder = await context.MasterDataFolders.FindAsync(1);
        Assert.Equal(603, checkFolder!.CurrentSequenceNumber);
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
