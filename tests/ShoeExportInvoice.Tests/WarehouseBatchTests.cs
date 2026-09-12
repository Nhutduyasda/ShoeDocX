using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Controllers;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using Xunit;

namespace ShoeExportInvoice.Tests;

public class WarehouseBatchTests
{
    private static AppDbContext CreateTestDbContext()
    {
        var dbName = "TestDb_Warehouse_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbName}.db;Pooling=False")
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        // Seed some product master data
        context.ProductMasters.AddRange(
            new ProductMaster
            {
                StyleCode = "40700-066",
                Description = "Giày thể thao mẫu 40700-066",
                UnitPriceCMT = 2.5m,
                UnitPriceCMT_Go = 1.8m
            },
            new ProductMaster
            {
                StyleCode = "40700-011",
                Description = "Giày mẫu 40700-011",
                UnitPriceCMT = 3.0m
            }
        );
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task SaveBatch_ShouldAutoFlag_PendingReview_ForNewStyleCodes()
    {
        using var context = CreateTestDbContext();
        var controller = new WarehouseController(context, NullLogger<WarehouseController>.Instance);

        var request = new SaveWarehouseBatchRequestDto
        {
            BatchNumber = "LẦN 14",
            ExportDate = new DateTime(2026, 8, 27),
            ContractNote = "5BUY HD THÀNH HÌNH",
            Items = new List<SaveWarehouseBatchItemRequestDto>
            {
                new() { StyleCode = "40700-066", Quantity = 288, ProcessType = ProcessType.Standard },
                new() { StyleCode = "40700-066.G", Quantity = 300, ProcessType = ProcessType.GoKhongMay },
                new() { StyleCode = "UNKNOWN-999", Quantity = 120, ProcessType = ProcessType.Standard }
            }
        };

        var result = await controller.SaveBatch(request);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var batchDto = Assert.IsType<WarehouseBatchDto>(okResult.Value);

        Assert.Equal("LẦN 14 27/08 5BUY HD THÀNH HÌNH", batchDto.BatchName);
        Assert.Equal(708, batchDto.TotalQuantity);
        Assert.Equal(3, batchDto.Items.Count);

        // 40700-066 exists in DB -> IsPendingReview should be false
        var item1 = batchDto.Items.First(i => i.StyleCode == "40700-066");
        Assert.False(item1.IsPendingReview);

        // 40700-066.G base code exists -> IsPendingReview should be false, ProcessType GoKhongMay
        var item2 = batchDto.Items.First(i => i.StyleCode == "40700-066.G");
        Assert.False(item2.IsPendingReview);
        Assert.Equal(ProcessType.GoKhongMay, item2.ProcessType);

        // UNKNOWN-999 does not exist -> IsPendingReview should be TRUE (optimistic save)
        var item3 = batchDto.Items.First(i => i.StyleCode == "UNKNOWN-999");
        Assert.True(item3.IsPendingReview);
    }

    [Fact]
    public async Task SubmitBatch_ShouldUpdateStatus_ToSubmittedToXnk()
    {
        using var context = CreateTestDbContext();
        var controller = new WarehouseController(context, NullLogger<WarehouseController>.Instance);

        var request = new SaveWarehouseBatchRequestDto
        {
            BatchNumber = "LẦN 15",
            ExportDate = new DateTime(2026, 8, 28),
            ContractNote = "6BUY COLUM",
            Items = new List<SaveWarehouseBatchItemRequestDto>
            {
                new() { StyleCode = "40700-011", Quantity = 500, ProcessType = ProcessType.Standard }
            }
        };

        var saveResult = await controller.SaveBatch(request);
        var okSave = Assert.IsType<OkObjectResult>(saveResult.Result);
        var batchDto = Assert.IsType<WarehouseBatchDto>(okSave.Value);
        Assert.Equal(WarehouseBatchStatus.Draft, batchDto.Status);

        // Now submit to XNK
        var submitResult = await controller.SubmitBatch(batchDto.Id);
        var okSubmit = Assert.IsType<OkObjectResult>(submitResult.Result);
        var submittedDto = Assert.IsType<WarehouseBatchDto>(okSubmit.Value);

        Assert.Equal(WarehouseBatchStatus.SubmittedToXnk, submittedDto.Status);
        Assert.NotNull(submittedDto.SubmittedAt);
    }

    [Fact]
    public async Task MarkProcessed_ShouldUpdateStatus_AndLinkShipmentOrder()
    {
        using var context = CreateTestDbContext();
        var controller = new WarehouseController(context, NullLogger<WarehouseController>.Instance);

        var batch = new WarehouseBatch
        {
            BatchName = "LẦN 16 29/08 TEST",
            BatchNumber = "LẦN 16",
            ExportDate = DateTime.Today,
            ContractNote = "TEST",
            Status = WarehouseBatchStatus.SubmittedToXnk
        };
        context.WarehouseBatches.Add(batch);
        await context.SaveChangesAsync();

        var markResult = await controller.MarkProcessed(batch.Id, shipmentOrderId: 42);
        var okResult = Assert.IsType<OkObjectResult>(markResult);

        var updated = await context.WarehouseBatches.FindAsync(batch.Id);
        Assert.NotNull(updated);
        Assert.Equal(WarehouseBatchStatus.ProcessedByXnk, updated.Status);
        Assert.Equal(42, updated.ShipmentOrderId);
    }
}
