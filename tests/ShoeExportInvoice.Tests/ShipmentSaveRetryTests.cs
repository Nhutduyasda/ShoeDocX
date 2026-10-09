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

public class ShipmentSaveRetryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public ShipmentSaveRetryTests()
    {
        _connection.Open();
        // Use the actual SQL Server strategy over SQLite to exercise its
        // user-transaction guard without requiring an external SQL Server.
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection, sql => sql.ExecutionStrategy(dependencies =>
                new SqlServerRetryingExecutionStrategy(dependencies)))
            .Options;
        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
        context.MasterDataFolders.Add(new MasterDataFolder { Id = 1, Name = "Contract", ContractNo = "A" });
        context.ProductMasters.Add(new ProductMaster
        {
            StyleCode = "SHOE", Description = "Shoe", FolderId = 1,
            UnitPriceCMT = 2, UnitPriceDAP = 20, PairPerCarton = 12
        });
        context.SaveChanges();
    }

    private static ShipmentsController Controller(AppDbContext context) =>
        new(context, new FakeExcelService(), new FakeSequenceService(),
            NullLogger<ShipmentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static CreateShipmentRequestDto Request(int quantity = 12) => new()
    {
        InvoiceNo = "INV-1", ContractFolderId = 1, ContractNo = "A", CustomerName = "Customer",
        Items = new() { new() { StyleCode = "SHOE", Quantity = quantity } }
    };

    [Fact]
    public async Task RetryStrategy_RejectsUserTransaction_ReproducingReportedError()
    {
        using var context = new AppDbContext(_options);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => context.ShipmentOrders.AnyAsync());
        Assert.Contains("does not support user-initiated transactions", error.Message);
    }

    [Fact]
    public async Task Create_WithSqlServerRetryStrategy_PersistsHeaderAndItems()
    {
        using (var context = new AppDbContext(_options))
        {
            var result = await Controller(context).CreateShipment(Request());
            Assert.IsType<CreatedAtActionResult>(result.Result);
        }
        using var verification = new AppDbContext(_options);
        var saved = Assert.Single(await verification.ShipmentOrders.Include(s => s.Items).ToListAsync());
        Assert.Equal("INV-1", saved.InvoiceNo);
        Assert.Equal(12, Assert.Single(saved.Items).Quantity);
    }

    [Fact]
    public async Task Update_WithSqlServerRetryStrategy_ReplacesItems()
    {
        int id;
        using (var setup = new AppDbContext(_options))
        {
            await Controller(setup).CreateShipment(Request());
            id = (await setup.ShipmentOrders.SingleAsync()).Id;
        }
        using (var context = new AppDbContext(_options))
        {
            var result = await Controller(context).UpdateShipment(id, Request(24));
            Assert.IsType<OkObjectResult>(result);
        }
        using var verification = new AppDbContext(_options);
        var saved = await verification.ShipmentOrders.Include(s => s.Items).SingleAsync();
        Assert.Equal(24, Assert.Single(saved.Items).Quantity);
        Assert.Single(await verification.ShipmentOrderItems.ToListAsync());
    }

    [Fact]
    public async Task Update_WhenItemInsertFails_RollsBackHeaderAndOldItems()
    {
        int id;
        using (var setup = new AppDbContext(_options))
        {
            await Controller(setup).CreateShipment(Request());
            id = (await setup.ShipmentOrders.SingleAsync()).Id;
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectReplacement BEFORE INSERT ON ShipmentOrderItems
                WHEN NEW.Quantity = 24
                BEGIN SELECT RAISE(ABORT, 'Simulated insert failure'); END;
                """);
        }
        using (var context = new AppDbContext(_options))
        {
            var request = Request(24);
            request.CustomerName = "Changed customer";
            var response = Assert.IsType<ObjectResult>(await Controller(context).UpdateShipment(id, request));
            Assert.Equal(500, response.StatusCode);
        }
        using var verification = new AppDbContext(_options);
        var saved = await verification.ShipmentOrders.Include(s => s.Items).SingleAsync();
        Assert.Equal("Customer", saved.CustomerName);
        Assert.Equal(12, Assert.Single(saved.Items).Quantity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_WithSqlServerRetryStrategy_CommitsOrdersAndSequence(bool split)
    {
        using var context = new AppDbContext(_options);
        var excel = new FakeExcelService();
        var request = ExportRequest(split);
        var result = await ExportController(context, excel).ExportExcel(request);
        Assert.IsType<FileContentResult>(result);
        using var verification = new AppDbContext(_options);
        var orders = await verification.ShipmentOrders.Include(s => s.Items).ToListAsync();
        Assert.Equal(split ? 2 : 1, orders.Count);
        Assert.All(orders, order => Assert.Equal(ShipmentStatus.Exported, order.Status));
        Assert.Equal(split ? 81 : 80, (await verification.MasterDataFolders.SingleAsync()).CurrentSequenceNumber);
    }

    [Fact]
    public async Task StandaloneSequence_WithSqlServerRetryStrategy_AllocatesAndOverrides()
    {
        using var context = new AppDbContext(_options);
        var service = new SequenceService(context, NullLogger<SequenceService>.Instance);
        var numbers = await service.GetNextSequenceNumbersAsync(2);
        Assert.Equal(numbers[0] + 1, numbers[1]);
        await service.SetNextSequenceNumberAsync(numbers[1] + 10);
        Assert.Equal(numbers[1] + 10, await service.GetCurrentNextNumberAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_WhenTransientFailureOccurs_ReplaysWithoutDuplicateOrdersOrSkippedSequence(bool split)
    {
        using var context = new AppDbContext(_options);
        var excel = new FailingExportService(transient: true);
        var request = ExportRequest(split);
        var result = await ExportController(context, excel).ExportExcel(request);
        Assert.IsType<FileContentResult>(result);
        Assert.Equal(2, excel.Calls);
        using var verification = new AppDbContext(_options);
        var orders = await verification.ShipmentOrders.Include(s => s.Items).ToListAsync();
        Assert.Equal(split ? 2 : 1, orders.Count);
        Assert.Equal(orders.Count, orders.Select(s => s.InvoiceNo).Distinct().Count());
        Assert.All(orders, order => Assert.Single(order.Items));
        Assert.Equal(split ? 81 : 80, (await verification.MasterDataFolders.SingleAsync()).CurrentSequenceNumber);
    }

    [Fact]
    public async Task Export_WhenFileGenerationFails_RollsBackOrdersAndSequence()
    {
        using var context = new AppDbContext(_options);
        var initialSequence = (await context.MasterDataFolders.SingleAsync()).CurrentSequenceNumber;
        var result = Assert.IsType<BadRequestObjectResult>(
            await ExportController(context, new FailingExportService(transient: false)).ExportExcel(ExportRequest(true)));
        Assert.Equal(400, result.StatusCode);
        using var verification = new AppDbContext(_options);
        Assert.Empty(await verification.ShipmentOrders.ToListAsync());
        Assert.Empty(await verification.ShipmentOrderItems.ToListAsync());
        Assert.Equal(initialSequence, (await verification.MasterDataFolders.SingleAsync()).CurrentSequenceNumber);
    }

    private static CreateShipmentRequestDto ExportRequest(bool split)
    {
        var request = Request();
        request.InvoiceNo = "79";
        request.StartInvoiceNumber = 79;
        if (split) request.Items.Add(new() { StyleCode = "SHOE", Quantity = 24, ProcessType = ProcessType.GoKhongMay });
        return request;
    }

    private static ShipmentsController ExportController(AppDbContext context, IExcelImportExportService excel) =>
        new(context, excel, new SequenceService(context, NullLogger<SequenceService>.Instance),
            NullLogger<ShipmentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private sealed class FailingExportService(bool transient) : FakeExcelService, IExcelImportExportService
    {
        public int Calls { get; private set; }
        private Task<byte[]> Generate()
        {
            Calls++;
            if (!transient) throw new InvalidOperationException("Template failed");
            if (Calls == 1) throw new TimeoutException("Simulated transient failure before commit");
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }
        Task<byte[]> IExcelImportExportService.ExportShipmentMultiSheetExcelAsync(CreateShipmentRequestDto request) => Generate();
        Task<byte[]> IExcelImportExportService.ExportSplitToZipAsync(CreateShipmentRequestDto go, string goName, CreateShipmentRequestDto standard, string standardName) => Generate();
    }

    public void Dispose() => _connection.Dispose();
}
