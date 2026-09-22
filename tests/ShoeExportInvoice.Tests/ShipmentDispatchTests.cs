using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Tests;

public sealed class ShipmentDispatchTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private ShipmentDispatchService _service = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _service = new ShipmentDispatchService(_db, new FakeExcel(), new FakeSequence(),
            NullLogger<ShipmentDispatchService>.Instance, new HttpContextAccessor());
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }

    [Fact]
    public async Task Merge_Should_Correctly_Sum_Quantities_And_Group_By_ProcessType()
    {
        var folder = await SeedFolderAsync();
        AddBatch(folder.Id, "23", (" 42072-267 ", 936, ProcessType.Standard));
        AddBatch(folder.Id, "24", ("42072-267", 1062, ProcessType.Standard));
        await _db.SaveChangesAsync();

        var result = await _service.PreviewMergeAsync(new() { SourceBatchIds = _db.WarehouseBatches.Select(b => b.Id).ToList() }, default);

        Assert.Single(result.MergedItems);
        Assert.Equal(1998, result.MergedItems[0].Quantity);
    }

    [Fact]
    public async Task Merge_Should_Not_Merge_Different_ProcessTypes()
    {
        var folder = await SeedFolderAsync();
        AddBatch(folder.Id, "23", ("42072-267", 936, ProcessType.Standard));
        AddBatch(folder.Id, "24", ("42072-267.G", 1062, ProcessType.GoKhongMay));
        await _db.SaveChangesAsync();
        var result = await _service.PreviewMergeAsync(new() { SourceBatchIds = _db.WarehouseBatches.Select(b => b.Id).ToList() }, default);
        Assert.Equal(2, result.MergedItems.Count);
    }

    [Fact]
    public async Task OcrMerge_Should_OnlyMergeSelectedDocuments_AndKeepProcessTypesSeparate()
    {
        var folder = await SeedFolderAsync("42072-410");
        var request = new MergeShipmentRequestDto
        {
            ContractFolderId = folder.Id,
            SourceDocuments =
            [
                OcrDocument("D19", ("42072-410", 5316, ProcessType.Standard)),
                OcrDocument("D23", ("42072-410", 1704, ProcessType.Standard), ("42072-410.G", 200, ProcessType.GoKhongMay))
            ]
        };

        var result = await _service.PreviewMergeAsync(request, default);

        Assert.Equal(2, result.MergedItems.Count);
        Assert.Equal(7020, result.MergedItems.Single(i => i.ProcessType == ProcessType.Standard).Quantity);
        Assert.Equal(200, result.MergedItems.Single(i => i.ProcessType == ProcessType.GoKhongMay).Quantity);
    }

    [Fact]
    public async Task OcrSplit_Should_RecalculateOriginalFromSourceDocument()
    {
        var request = new ValidateSplitRequestDto
        {
            SourceDocument = OcrDocument("D20", ("42072-410", 1704, ProcessType.Standard), ("42073-030", 288, ProcessType.Standard), ("42072-030", 5550, ProcessType.Standard)),
            SubInvoices =
            [
                new() { Items = [Item("42072-410", 852), Item("42073-030", 144), Item("42072-030", 2775)] },
                new() { Items = [Item("42072-410", 852), Item("42073-030", 144), Item("42072-030", 2775)] }
            ]
        };

        var result = await _service.ValidateSplitAsync(request, default);

        Assert.True(result.IsValid);
        Assert.Equal(7542, result.OriginalTotal);
        Assert.Equal(7542, result.AllocatedTotal);
    }

    [Fact]
    public async Task OcrSplit_Should_NotAcceptClientAllocationForAnotherDocument()
    {
        var result = await _service.ValidateSplitAsync(new()
        {
            SourceDocument = OcrDocument("D19", ("A", 100, ProcessType.Standard)),
            SubInvoices = [new() { Items = [Item("B", 50)] }, new() { Items = [Item("B", 50)] }]
        }, default);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "SOURCE_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task MergedOcrSplit_Should_RebuildMergedSource_AndValidateFourInvoices()
    {
        var documents = new List<OcrDispatchSourceDocumentDto>
        {
            OcrDocument("L19", ("41898-2LI", 84, ProcessType.Standard), ("42072-410", 5316, ProcessType.Standard),
                ("40700-060", 612, ProcessType.Standard), ("42073-030", 2880, ProcessType.Standard), ("42072-030", 2856, ProcessType.Standard)),
            OcrDocument("L20", ("42072-410", 1704, ProcessType.Standard), ("42073-030", 288, ProcessType.Standard),
                ("42072-030", 5550, ProcessType.Standard))
        };
        var merged = new Dictionary<string, int> { ["41898-2LI"] = 84, ["42072-410"] = 7020, ["40700-060"] = 612, ["42073-030"] = 3168, ["42072-030"] = 8406 };
        var folder = await SeedFolderAsync(merged.Keys.ToArray());
        var preview = await _service.PreviewMergeAsync(new() { ContractFolderId = folder.Id, SourceDocuments = documents }, default);
        var invoices = Enumerable.Range(0, 4).Select(index => new SubInvoiceAllocationDto
        {
            Items = merged.Select(pair => Item(pair.Key, pair.Value / 4 + (index < pair.Value % 4 ? 1 : 0))).ToList()
        }).ToList();

        var result = await _service.ValidateSplitAsync(new() { SourceDocuments = documents, SubInvoices = invoices }, default);

        Assert.Equal(5, preview.MergedItems.Count);
        Assert.Equal(19290, preview.TotalQuantity);
        Assert.Equal(7020, preview.MergedItems.Single(item => item.StyleCode == "42072-410").Quantity);
        Assert.Equal(3168, preview.MergedItems.Single(item => item.StyleCode == "42073-030").Quantity);
        Assert.Equal(8406, preview.MergedItems.Single(item => item.StyleCode == "42072-030").Quantity);
        Assert.True(result.IsValid);
        Assert.Equal(19290, result.OriginalTotal);
        Assert.Equal(19290, result.AllocatedTotal);
        Assert.All(result.ItemChecks, check => Assert.True(check.IsMatched));
        Assert.Equal(7020, result.ItemChecks.Single(check => check.StyleCode == "42072-410").OriginalQty);
        Assert.Equal(3168, result.ItemChecks.Single(check => check.StyleCode == "42073-030").OriginalQty);
        Assert.Equal(8406, result.ItemChecks.Single(check => check.StyleCode == "42072-030").OriginalQty);
    }

    [Fact]
    public async Task MergedOcrSplit_Should_Fail_WhenMergedAllocationIsWrong()
    {
        var result = await _service.ValidateSplitAsync(new()
        {
            SourceDocuments = [OcrDocument("L19", ("A", 100, ProcessType.Standard)), OcrDocument("L20", ("A", 50, ProcessType.Standard))],
            SubInvoices = [new() { Items = [Item("A", 70)] }, new() { Items = [Item("A", 70)] }]
        }, default);

        Assert.False(result.IsValid);
        Assert.Equal(150, result.OriginalTotal);
        Assert.Contains(result.Errors, error => error.Code == "SPLIT_TOTAL_MISMATCH");
    }

    [Fact]
    public async Task Split_Should_Fail_When_Quantities_Do_Not_Match_Original()
    {
        var id = await SeedBatchAsync(("A", 11816, ProcessType.Standard));
        var result = await Validate(id, (5900, 5900));
        Assert.False(result.IsValid);
        Assert.Equal(-16, result.Discrepancy);
        Assert.Contains(result.Errors, e => e.Code == "SPLIT_TOTAL_MISMATCH");
    }

    [Fact]
    public async Task Split_Should_Fail_When_One_Style_Is_Overallocated_And_Another_Is_Underallocated()
    {
        var id = await SeedBatchAsync(("A", 100, ProcessType.Standard), ("B", 100, ProcessType.Standard));
        var request = new ValidateSplitRequestDto { SourceBatchId = id, SubInvoices =
        [
            new() { Items = [Item("A", 56), Item("B", 44)] },
            new() { Items = [Item("A", 56), Item("B", 44)] }
        ]};
        var result = await _service.ValidateSplitAsync(request, default);
        Assert.False(result.IsValid);
        Assert.Equal(0, result.Discrepancy);
        Assert.Equal(2, result.ItemChecks.Count(c => !c.IsMatched));
    }

    [Fact]
    public async Task Split_Should_Return_Warning_When_Carton_Is_Not_Divisible_By_12()
    {
        var id = await SeedBatchAsync(("A", 1330, ProcessType.Standard));
        var result = await Validate(id, (660, 670));
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "ODD_CARTON_WARNING");
    }

    [Fact]
    public async Task Split_Should_Reject_Empty_SubInvoice()
    {
        var id = await SeedBatchAsync(("A", 24, ProcessType.Standard));
        var result = await _service.ValidateSplitAsync(new() { SourceBatchId = id, SubInvoices = [new(), new() { Items = [Item("A", 24)] }] }, default);
        Assert.Contains(result.Errors, e => e.Code == "EMPTY_SUBINVOICE");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Split_Should_Reject_Zero_Or_Negative_Quantity(int quantity)
    {
        var id = await SeedBatchAsync(("A", 24, ProcessType.Standard));
        var result = await _service.ValidateSplitAsync(new() { SourceBatchId = id, SubInvoices =
            [new() { Items = [Item("A", quantity)] }, new() { Items = [Item("A", 24)] }] }, default);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_ITEM_QUANTITY");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public async Task Split_Should_Reject_Invalid_Number_Of_SubInvoices(int count)
    {
        var id = await SeedBatchAsync(("A", 24, ProcessType.Standard));
        var result = await _service.ValidateSplitAsync(new() { SourceBatchId = id,
            SubInvoices = Enumerable.Range(0, count).Select(_ => new SubInvoiceAllocationDto { Items = [Item("A", 1)] }).ToList() }, default);
        Assert.Contains(result.Errors, e => e.Code == "SPLIT_INVOICE_COUNT_INVALID");
    }

    [Fact]
    public async Task Split_Should_Generate_Consecutive_Invoices_Zip_And_Traceability()
    {
        var id = await SeedBatchAsync(("A", 24, ProcessType.Standard));
        var result = await _service.ExportSplitZipAsync(new() { SourceBatchId = id, SubInvoices =
            [new() { Items = [Item("A", 12)] }, new() { Items = [Item("A", 12)] }] }, default);
        using var archive = new ZipArchive(new MemoryStream(result.Content), ZipArchiveMode.Read);
        Assert.Equal(2, archive.Entries.Count);
        Assert.Equal(2, await _db.ShipmentOrders.CountAsync());
        Assert.Equal(2, await _db.ShipmentSourceBatches.CountAsync());
        Assert.Equal(WarehouseBatchStatus.ProcessedByXnk, (await _db.WarehouseBatches.FindAsync(id))!.Status);
    }

    [Fact]
    public async Task Split_Export_Should_Not_Persist_Partial_Data_When_File_Generation_Fails()
    {
        var id = await SeedBatchAsync(("A", 24, ProcessType.Standard));
        var failing = new ShipmentDispatchService(_db, new FakeExcel(true), new FakeSequence(),
            NullLogger<ShipmentDispatchService>.Instance, new HttpContextAccessor());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.ExportSplitZipAsync(new() { SourceBatchId = id, SubInvoices =
            [new() { Items = [Item("A", 12)] }, new() { Items = [Item("A", 12)] }] }, default));
        Assert.Empty(await _db.ShipmentOrders.ToListAsync());
        Assert.Equal(WarehouseBatchStatus.Draft, (await _db.WarehouseBatches.FindAsync(id))!.Status);
    }

    private async Task<ValidateSplitResultDto> Validate(int id, (int first, int second) q) =>
        await _service.ValidateSplitAsync(new() { SourceBatchId = id, SubInvoices =
            [new() { Items = [Item("A", q.first)] }, new() { Items = [Item("A", q.second)] }] }, default);

    private async Task<int> SeedBatchAsync(params (string code, int qty, ProcessType process)[] items)
    {
        var folder = await SeedFolderAsync(items.Select(i => i.code).Distinct().ToArray());
        var batch = AddBatch(folder.Id, "source", items);
        await _db.SaveChangesAsync();
        return batch.Id;
    }

    private async Task<MasterDataFolder> SeedFolderAsync(params string[] codes)
    {
        var folder = new MasterDataFolder { Name = Guid.NewGuid().ToString(), ContractNo = "C", CustomerName = "Customer" };
        _db.MasterDataFolders.Add(folder);
        await _db.SaveChangesAsync();
        foreach (var code in (codes.Length == 0 ? ["42072-267"] : codes))
            _db.ProductMasters.Add(new ProductMaster { FolderId = folder.Id, StyleCode = code, Description = code, UnitPriceDAP = 1, PairPerCarton = 12 });
        await _db.SaveChangesAsync();
        return folder;
    }

    private WarehouseBatch AddBatch(int folderId, string number, params (string code, int qty, ProcessType process)[] items)
    {
        var batch = new WarehouseBatch { ContractFolderId = folderId, BatchName = number, BatchNumber = number,
            TotalQuantity = items.Sum(i => i.qty), Items = items.Select(i => new WarehouseBatchItem { StyleCode = i.code, Quantity = i.qty, ProcessType = i.process }).ToList() };
        _db.WarehouseBatches.Add(batch);
        return batch;
    }

    private static CreateShipmentItemDto Item(string code, int qty) => new() { StyleCode = code, Quantity = qty, ProcessType = ProcessType.Standard };
    private static OcrDispatchSourceDocumentDto OcrDocument(string id, params (string code, int qty, ProcessType process)[] items) => new()
    {
        DocumentId = id,
        Title = id,
        SourceFileName = "source.png",
        ClientFileId = "client-1",
        Items = items.Select(item => new CreateShipmentItemDto { StyleCode = item.code, Quantity = item.qty, ProcessType = item.process }).ToList()
    };

    private sealed class FakeSequence : ISequenceService
    {
        private int _next = 233;
        public Task<int[]> GetNextSequenceNumbersAsync(int count = 1) => Task.FromResult(Enumerable.Range(_next, count).ToArray());
        public Task<int[]> ReservePartnerSequenceNumbersAsync(int folderId, int count = 1, int? requestedStart = null) { var first = requestedStart ?? _next; _next = first + count; return Task.FromResult(Enumerable.Range(first, count).ToArray()); }
        public Task SetNextSequenceNumberAsync(int nextNumber) { _next = nextNumber; return Task.CompletedTask; }
        public Task<int> GetCurrentNextNumberAsync() => Task.FromResult(_next);
        public string ToInvoiceNo(int sequenceNumber) => $"INV{sequenceNumber}";
        public string ToFileName(int sequenceNumber) => $"INV{sequenceNumber}.xlsx";
        public int? ExtractSequenceNumber(string invoiceNo) => null;
    }

    private sealed class FakeExcel : IExcelImportExportService
    {
        private readonly bool _fail;
        public FakeExcel(bool fail = false) => _fail = fail;
        public PklPreviewResponseDto CalculatePklBreakdown(CreateShipmentRequestDto r) => new() { TotalQuantity = r.Items.Sum(i => i.Quantity),
            TotalCartons = r.Items.Sum(i => (int)Math.Ceiling((double)i.Quantity / (i.PairPerCarton ?? 12))), BreakdownItems = [] };
        public DocumentPreviewResponseDto CalculateDocumentPreview(CreateShipmentRequestDto request) => throw new NotImplementedException();
        public Task<byte[]> ExportShipmentMultiSheetExcelAsync(CreateShipmentRequestDto request) => _fail
            ? throw new InvalidOperationException("Synthetic export failure") : Task.FromResult(new byte[] { 1 });
        public Task<byte[]> ExportSplitToZipAsync(CreateShipmentRequestDto goRequest, string goFileName, CreateShipmentRequestDto standardRequest, string standardFileName) => throw new NotImplementedException();
        public Task<byte[]> ExportShipmentToExcelAsync(ShipmentExportModel model) => throw new NotImplementedException();
        public byte[] GenerateProductMasterTemplate() => throw new NotImplementedException();
        public Task<byte[]> ExportProductMastersToExcelAsync() => throw new NotImplementedException();
        public Task<ImportResultDto> ImportProductMastersFromExcelAsync(Stream fileStream, bool updateExisting = true, int? folderId = null, ColumnMappingOverrideDto? mappingOverride = null) => throw new NotImplementedException();
        public Task<ImportPreviewResponseDto> PreviewProductMastersFromExcelAsync(Stream fileStream, int? folderId = null) => throw new NotImplementedException();
    }
}
