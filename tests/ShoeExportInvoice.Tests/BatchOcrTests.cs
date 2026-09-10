using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Controllers;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;
using Xunit;

namespace ShoeExportInvoice.Tests;

public class BatchOcrTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public BatchOcrTests()
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

    [Fact]
    public void BatchOcrScanResultDto_ShouldComputeMatchingAndDiscrepancyCorrectly()
    {
        var matchedResult = new BatchOcrScanResultDto
        {
            ReportedTotal = 500,
            CalculatedTotal = 500
        };
        Assert.True(matchedResult.IsMatched);
        Assert.Equal(0, matchedResult.Discrepancy);

        var mismatchedResult = new BatchOcrScanResultDto
        {
            ReportedTotal = 500,
            CalculatedTotal = 480
        };
        Assert.False(mismatchedResult.IsMatched);
        Assert.Equal(-20, mismatchedResult.Discrepancy);
    }

    [Fact]
    public async Task BatchExportZip_ShouldAllocateConsecutiveSequences_AndSaveToDb()
    {
        using (var context = new AppDbContext(_dbOptions))
        {
            // Seed Master Data
            context.ProductMasters.Add(new ProductMaster
            {
                StyleCode = "STYLE-A",
                Description = "Giày thể thao A",
                UnitPriceCMT = 5.0m,
                UnitPriceDAP = 15.0m,
                UnitPriceCMT_Go = 4.0m,
                UnitPriceDAP_Go = 12.0m,
                PairPerCarton = 12,
                Unit = "đôi"
            });
            await context.SaveChangesAsync();
        }

        using (var context = new AppDbContext(_dbOptions))
        {
            var seqService = new SequenceService(context, NullLogger<SequenceService>.Instance);
            var excelService = new ExcelImportExportService(
                context,
                NullLogger<ExcelImportExportService>.Instance);

            var controller = new OcrController(
                null!, // OCR service not called during batch-export-zip
                seqService,
                excelService,
                context,
                NullLogger<OcrController>.Instance);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };

            // Request with 2 Batches:
            // Batch 1: Has both Go and Standard (should split into 2 files)
            // Batch 2: Has only Standard (should produce 1 file)
            // Total files expected = 3
            var request = new BatchOcrConfirmRequestDto
            {
                PoSuffix = "(KM3.PO5.26)",
                ContractNo = "KM-HANEW/01-2025",
                CustomerName = "Target Corp",
                Batches = new List<BatchScanItemExportDto>
                {
                    new BatchScanItemExportDto
                    {
                        BatchId = "b-1",
                        Title = "LẦN 16 THÀNH HÌNH & GÒ",
                        Items = new List<CreateShipmentItemDto>
                        {
                            new CreateShipmentItemDto { StyleCode = "STYLE-A", Quantity = 100, ProcessType = ProcessType.GoKhongMay },
                            new CreateShipmentItemDto { StyleCode = "STYLE-A", Quantity = 200, ProcessType = ProcessType.Standard }
                        }
                    },
                    new BatchScanItemExportDto
                    {
                        BatchId = "b-2",
                        Title = "LẦN 17 THÀNH HÌNH",
                        Items = new List<CreateShipmentItemDto>
                        {
                            new CreateShipmentItemDto { StyleCode = "STYLE-A", Quantity = 350, ProcessType = ProcessType.Standard }
                        }
                    }
                }
            };

            var actionResult = await controller.BatchExportZip(request);
            var fileResult = Assert.IsType<FileContentResult>(actionResult);
            Assert.Equal("application/zip", fileResult.ContentType);
            Assert.NotNull(fileResult.FileContents);
            Assert.True(fileResult.FileContents.Length > 0);

            // Read ZIP archive to verify files
            using var zipMs = new MemoryStream(fileResult.FileContents);
            using var archive = new ZipArchive(zipMs, ZipArchiveMode.Read);

            Assert.Equal(3, archive.Entries.Count);
            Assert.All(archive.Entries, entry => Assert.EndsWith(".xlsx", entry.Name));

            // Verify database orders created
            var savedOrders = await context.ShipmentOrders
                .Include(s => s.Items)
                .OrderBy(s => s.Id)
                .ToListAsync();

            Assert.Equal(3, savedOrders.Count);
            Assert.All(savedOrders, s => Assert.Equal(ShipmentStatus.Exported, s.Status));
            Assert.All(savedOrders, s => Assert.Equal("E52", s.CustomsDeclarationType));

            // Verify quantities
            Assert.Equal(100, savedOrders[0].Items.Sum(i => i.Quantity));
            Assert.Equal(200, savedOrders[1].Items.Sum(i => i.Quantity));
            Assert.Equal(350, savedOrders[2].Items.Sum(i => i.Quantity));
        }
    }
}
