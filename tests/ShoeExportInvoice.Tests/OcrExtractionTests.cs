using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Controllers;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Tests;

public class OcrExtractionTests
{
    [Fact]
    public async Task ExtractFromImageAsync_WithoutApiKey_ShouldThrowInvalidOperationException()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "",
                ["OcrSettings:OpenAIApiKey"] = ""
            })
            .Build();

        var httpClient = new HttpClient();
        var service = new OcrExtractionService(httpClient, context, configuration, NullLogger<OcrExtractionService>.Instance);

        using var dummyStream = new MemoryStream(new byte[] { 1, 2, 3 });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExtractFromImageAsync(dummyStream, "image/png"));
    }

    [Fact]
    public async Task ParseAndEnrichOcrResultAsync_ShouldCorrectlyParseReconcileAndMapGoKhongMay()
    {
        // Setup SQLite in-memory connection
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Seed products
        context.ProductMasters.AddRange(
            new ProductMaster
            {
                StyleCode = "42072-030",
                Description = "Giày thể thao nữ",
                UnitPriceCMT = 3.2m,
                UnitPriceDAP = 8.2m,
                HsCode = "64041990",
                Unit = "đôi",
                PairPerCarton = 12,
                CreatedAt = DateTime.UtcNow
            },
            new ProductMaster
            {
                StyleCode = "45428-2LX",
                Description = "Giày thể thao nam",
                UnitPriceCMT = 4.1m,
                UnitPriceDAP = 9.5m,
                HsCode = "64041990",
                Unit = "đôi",
                PairPerCarton = 12,
                CreatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new OcrExtractionService(httpClient, context, configuration, NullLogger<OcrExtractionService>.Instance);

        var sampleOpenAiJson = @"{
          ""title"": ""LẦN 20 08/9 5BUY HD THÀNH HÌNH"",
          ""reportedTotal"": 6348,
          ""items"": [
            { ""styleCode"": ""42072-410"", ""quantity"": 36, ""note"": """" },
            { ""styleCode"": ""41898-2LI"", ""quantity"": 228, ""note"": """" },
            { ""styleCode"": ""42072-030"", ""quantity"": 4032, ""note"": """" },
            { ""styleCode"": ""42073-030"", ""quantity"": 312, ""note"": """" },
            { ""styleCode"": ""45428-2LX"", ""quantity"": 780, ""note"": ""GÒ KHÔNG MAY"" },
            { ""styleCode"": ""43223-001"", ""quantity"": 960, ""note"": ""GÒ KHÔNG MAY"" }
          ]
        }";

        var result = await service.ParseAndEnrichOcrResultAsync(sampleOpenAiJson, default);

        Assert.NotNull(result);
        Assert.Equal("LẦN 20 08/9 5BUY HD THÀNH HÌNH", result.Title);
        Assert.Equal(6348, result.ReportedTotal);
        Assert.Equal(6348, result.CalculatedTotal);
        Assert.True(result.IsTotalMatched);
        Assert.Equal(6, result.Items.Count);

        // Check GoKhongMay mapping
        var goItem = result.Items.FirstOrDefault(x => x.StyleCode == "45428-2LX");
        Assert.NotNull(goItem);
        Assert.Equal(ProcessType.GoKhongMay, goItem.ProcessType);
        Assert.Equal(780, goItem.Quantity);
        Assert.True(goItem.IsMatched);
        Assert.Equal(4.1m, goItem.UnitPriceCMT);
        Assert.Equal(9.5m, goItem.UnitPriceDAP);

        // Check Standard mapping
        var stdItem = result.Items.FirstOrDefault(x => x.StyleCode == "42072-030");
        Assert.NotNull(stdItem);
        Assert.Equal(ProcessType.Standard, stdItem.ProcessType);
        Assert.Equal(4032, stdItem.Quantity);
        Assert.True(stdItem.IsMatched);
    }

    [Theory]
    [InlineData("BM5879-464(KM3)", "BM5879-464")]
    [InlineData("BL5879-482(KM3)", "BL5879-482")]
    [InlineData("YL3564-469(KM3)", "YL3564-469")]
    [InlineData("BM5879-464 (KM3)", "BM5879-464")]
    [InlineData("BM5879-464 (X3)", "BM5879-464")]
    [InlineData("45428-2LX.G(KM3)", "45428-2LX.G")]
    [InlineData("45428-2LX(KM3).G", "45428-2LX.G")]
    [InlineData("42072-030", "42072-030")]
    public void NormalizeStyleCode_ShouldStripPartnerSuffixInParentheses(string raw, string expected)
    {
        var result = OcrExtractionService.NormalizeStyleCode(raw);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ParseAndEnrichOcrResultAsync_WithParenthesisPartnerSuffix_ShouldNormalizeAndMatchMasterData()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Seed products with clean code
        context.ProductMasters.AddRange(
            new ProductMaster
            {
                StyleCode = "BM5879-464",
                Description = "Giày thể thao nữ KM3",
                UnitPriceCMT = 3.5m,
                UnitPriceDAP = 9.0m,
                HsCode = "64041990",
                Unit = "đôi",
                PairPerCarton = 12,
                CreatedAt = DateTime.UtcNow
            },
            new ProductMaster
            {
                StyleCode = "YL3564-469",
                Description = "Giày thể thao nam YL",
                UnitPriceCMT = 4.2m,
                UnitPriceDAP = 10.5m,
                HsCode = "64041990",
                Unit = "đôi",
                PairPerCarton = 12,
                CreatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new OcrExtractionService(httpClient, context, configuration, NullLogger<OcrExtractionService>.Instance);

        var sampleJsonWithSuffixes = @"{
          ""title"": ""PHIẾU XUẤT KHO XƯỞNG"",
          ""reportedTotal"": 1500,
          ""items"": [
            { ""styleCode"": ""BM5879-464(KM3)"", ""quantity"": 1000, ""note"": """" },
            { ""styleCode"": ""YL3564-469 (X3)"", ""quantity"": 500, ""note"": """" }
          ]
        }";

        var result = await service.ParseAndEnrichOcrResultAsync(sampleJsonWithSuffixes, default);

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);

        // Check BM5879-464 was normalized and matched
        var item1 = result.Items.FirstOrDefault(x => x.StyleCode == "BM5879-464");
        Assert.NotNull(item1);
        Assert.True(item1.IsMatched);
        Assert.Equal(3.5m, item1.UnitPriceCMT);
        Assert.Equal(9.0m, item1.UnitPriceDAP);
        Assert.Equal("Giày thể thao nữ KM3", item1.Description);

        // Check YL3564-469 was normalized and matched
        var item2 = result.Items.FirstOrDefault(x => x.StyleCode == "YL3564-469");
        Assert.NotNull(item2);
        Assert.True(item2.IsMatched);
        Assert.Equal(4.2m, item2.UnitPriceCMT);
        Assert.Equal(10.5m, item2.UnitPriceDAP);
        Assert.Equal("Giày thể thao nam YL", item2.Description);
    }

    [Fact]
    public async Task ParseAndEnrichOcrResultAsync_ShouldKeepMultipleDocumentsSeparate_AndNotInventTotal()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new OcrExtractionService(new HttpClient(), context, new ConfigurationBuilder().Build(), NullLogger<OcrExtractionService>.Instance);

        var json = @"{
          ""documents"": [
            { ""title"": ""LẦN 19"", ""reportedTotal"": 11748, ""sourceRegion"": { ""x"": 0.01, ""y"": 0.05, ""width"": 0.46, ""height"": 0.9 },
              ""items"": [
                { ""styleCode"": ""41898-2LI"", ""quantity"": 84, ""note"": """" },
                { ""styleCode"": ""42072-410"", ""quantity"": 5316, ""note"": """" },
                { ""styleCode"": ""40700-060"", ""quantity"": 612, ""note"": """" },
                { ""styleCode"": ""42073-030"", ""quantity"": 2880, ""note"": """" },
                { ""styleCode"": ""42072-030"", ""quantity"": 2856, ""note"": """" }
              ] },
            { ""title"": ""LẦN 20"", ""reportedTotal"": null,
              ""items"": [
                { ""styleCode"": ""42072-410"", ""quantity"": 1704, ""note"": """" },
                { ""styleCode"": ""42073-030"", ""quantity"": 288, ""note"": """" },
                { ""styleCode"": ""42072-030.G"", ""quantity"": 5550, ""note"": """" }
              ] }
          ]
        }";

        var result = await service.ParseAndEnrichOcrResultAsync(json, default);

        Assert.Equal(2, result.Documents.Count);
        Assert.Equal(11748, result.Documents[0].CalculatedTotal);
        Assert.True(result.Documents[0].IsTotalMatched);
        Assert.Equal(7542, result.Documents[1].CalculatedTotal);
        Assert.Null(result.Documents[1].ReportedTotal);
        Assert.False(result.Documents[1].HasReportedTotal);
        Assert.False(result.Documents[1].IsTotalMatched);
        Assert.Equal(5316, Assert.Single(result.Documents[0].Items.Where(i => i.StyleCode == "42072-410")).Quantity);
        Assert.Equal(1704, Assert.Single(result.Documents[1].Items.Where(i => i.StyleCode == "42072-410")).Quantity);
        Assert.Equal(ProcessType.GoKhongMay, result.Documents[1].Items.Last().ProcessType);
        Assert.Equal(0.46, result.Documents[0].SourceRegion?.Width);
    }

    [Fact]
    public async Task Phase223_Test3_ManualConfirmation_InvalidatedWhenQuantityEdited()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new OcrExtractionService(new HttpClient(), context, new ConfigurationBuilder().Build(), NullLogger<OcrExtractionService>.Instance);

        var doc = new OcrDetectedDocumentDto
        {
            DocumentId = "DOC-1",
            Title = "LẦN 19",
            ReportedTotal = 1000,
            CalculatedTotal = 950,
            IsManuallyConfirmed = true,
            Items = new List<OcrItemDto>
            {
                new() { StyleCode = "STYLE-A", Quantity = 950, ProcessType = ProcessType.Standard }
            }
        };

        // When user edits quantity: 950 -> 900
        doc.Items[0].Quantity = 900;
        var recomputed = await service.ReEnrichOcrDocumentsForPartnerAsync(new List<OcrDetectedDocumentDto> { doc }, null);

        Assert.Equal(900, recomputed[0].CalculatedTotal);
        // Confirmation must be invalidated because calculatedTotal changed
        Assert.False(recomputed[0].IsManuallyConfirmed);
    }

    [Fact]
    public async Task Phase223_Test4_ManualConfirmationAudit_ShouldBeCreated()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var controller = new OcrController(
            new OcrExtractionService(new HttpClient(), context, new ConfigurationBuilder().Build(), NullLogger<OcrExtractionService>.Instance),
            new SequenceService(context, NullLogger<SequenceService>.Instance),
            new StubExcelService(),
            context,
            NullLogger<OcrController>.Instance
        );

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-456"),
            new Claim(ClaimTypes.Name, "Tester"),
            new Claim(ClaimTypes.Role, "Xnk")
        }, "mock"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new OcrMismatchConfirmRequestDto
        {
            DocumentId = "DOC-MISMATCH-1",
            DocumentTitle = "LẦN 19",
            ReportedTotal = 235,
            CalculatedTotal = 11748,
            ContractFolderId = 10,
            Reason = "Số 235 là ghi chú trên phiếu, các dòng 11.748 đôi là đúng"
        };

        var result = await controller.ConfirmMismatch(request, default);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);

        var audit = await context.BusinessAuditLogs.SingleAsync(a => a.Action == "OCR_MISMATCH_CONFIRMED");
        Assert.Equal("OcrDocument", audit.ResourceType);
        Assert.Equal("DOC-MISMATCH-1", audit.ResourceId);
        Assert.Equal("user-456", audit.ActorUserId);
        Assert.Equal("Tester", audit.ActorUserName);
        Assert.Contains("11748", audit.NewStateJson);
        Assert.Contains("235", audit.NewStateJson);
    }

    [Fact]
    public async Task Phase223_Test5_ChangePartner_ShouldNotChange_OcrItemRawQuantity()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new OcrExtractionService(new HttpClient(), context, new ConfigurationBuilder().Build(), NullLogger<OcrExtractionService>.Instance);

        var doc = new OcrDetectedDocumentDto
        {
            DocumentId = "DOC-PARTNER-SWITCH",
            Title = "LẦN 19",
            Items = new List<OcrItemDto>
            {
                new() { StyleCode = "42072-410", Quantity = 5316 },
                new() { StyleCode = "42073-030", Quantity = 2880 },
                new() { StyleCode = "42072-030", Quantity = 2856 }
            }
        };

        // Enrich with Partner 1 (e.g. 101)
        var forPartner1 = await service.ReEnrichOcrDocumentsForPartnerAsync(new List<OcrDetectedDocumentDto> { doc }, 101);
        Assert.Equal(5316, forPartner1[0].Items[0].Quantity);
        Assert.Equal(2880, forPartner1[0].Items[1].Quantity);
        Assert.Equal(2856, forPartner1[0].Items[2].Quantity);

        // Switch to Partner 2 (e.g. 202)
        var forPartner2 = await service.ReEnrichOcrDocumentsForPartnerAsync(forPartner1, 202);
        Assert.Equal(5316, forPartner2[0].Items[0].Quantity);
        Assert.Equal(2880, forPartner2[0].Items[1].Quantity);
        Assert.Equal(2856, forPartner2[0].Items[2].Quantity);
        Assert.Equal(11052, forPartner2[0].CalculatedTotal);
    }

    [Fact]
    public async Task Phase223_Test6_ReEnrichPartnerA_To_B_ShouldUpdateProductMasterInfo()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var folderA = new MasterDataFolder { Name = "Partner KM3" };
        var folderB = new MasterDataFolder { Name = "Partner 5BUY", DefaultPairsPerCarton = 24 };
        context.MasterDataFolders.AddRange(folderA, folderB);
        await context.SaveChangesAsync();

        context.ProductMasters.AddRange(
            new ProductMaster
            {
                FolderId = folderA.Id,
                StyleCode = "42072-030",
                Description = "Mô tả cho KM3",
                UnitPriceCMT = 3.0m,
                UnitPriceDAP = 8.0m,
                PairPerCarton = 12
            },
            new ProductMaster
            {
                FolderId = folderB.Id,
                StyleCode = "42072-030",
                Description = "Mô tả cho 5BUY",
                UnitPriceCMT = 4.5m,
                UnitPriceDAP = 10.0m,
                PairPerCarton = 24
            }
        );
        await context.SaveChangesAsync();

        var service = new OcrExtractionService(new HttpClient(), context, new ConfigurationBuilder().Build(), NullLogger<OcrExtractionService>.Instance);

        var doc = new OcrDetectedDocumentDto
        {
            DocumentId = "DOC-SWITCH-TEST",
            Title = "LẦN 20",
            Items = new List<OcrItemDto>
            {
                new() { StyleCode = "42072-030", Quantity = 100 }
            }
        };

        // Enrich with Partner A
        var resA = await service.ReEnrichOcrDocumentsForPartnerAsync(new List<OcrDetectedDocumentDto> { doc }, folderA.Id);
        Assert.True(resA[0].Items[0].IsMatched);
        Assert.Equal(3.0m, resA[0].Items[0].UnitPriceCMT);
        Assert.Equal(8.0m, resA[0].Items[0].UnitPriceDAP);
        Assert.Equal(12, resA[0].Items[0].PairPerCarton);
        Assert.Equal("Mô tả cho KM3", resA[0].Items[0].Description);

        // Switch to Partner B
        var resB = await service.ReEnrichOcrDocumentsForPartnerAsync(resA, folderB.Id);
        Assert.True(resB[0].Items[0].IsMatched);
        Assert.Equal(4.5m, resB[0].Items[0].UnitPriceCMT);
        Assert.Equal(10.0m, resB[0].Items[0].UnitPriceDAP);
        Assert.Equal(24, resB[0].Items[0].PairPerCarton);
        Assert.Equal("Mô tả cho 5BUY", resB[0].Items[0].Description);
    }

    [Fact]
    public async Task ParseAndEnrichOcrResultAsync_SideBySideParallelTables_ShouldExtractTwoIndependentDocuments()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.ProductMasters.AddRange(
            new ProductMaster { StyleCode = "42072-410", Description = "Shoe A", UnitPriceCMT = 3m, UnitPriceDAP = 8m },
            new ProductMaster { StyleCode = "42073-030", Description = "Shoe B", UnitPriceCMT = 4m, UnitPriceDAP = 9m }
        );
        await context.SaveChangesAsync();

        var service = new OcrExtractionService(new HttpClient(), context, new ConfigurationBuilder().Build(), NullLogger<OcrExtractionService>.Instance);

        // Simulated side-by-side tables in 1 receipt image (Lần 19 on Left, Lần 20 on Right)
        var sideBySideJson = @"{
          ""documents"": [
            {
              ""title"": ""LẦN 19"",
              ""reportedTotal"": 1500,
              ""sourceRegion"": { ""x"": 0.02, ""y"": 0.05, ""width"": 0.46, ""height"": 0.88 },
              ""items"": [
                { ""styleCode"": ""42072-410"", ""quantity"": 500, ""note"": """" },
                { ""styleCode"": ""42073-030"", ""quantity"": 1000, ""note"": ""GÒ KHÔNG MAY"" }
              ]
            },
            {
              ""title"": ""LẦN 20"",
              ""reportedTotal"": 2000,
              ""sourceRegion"": { ""x"": 0.52, ""y"": 0.05, ""width"": 0.46, ""height"": 0.88 },
              ""items"": [
                { ""styleCode"": ""42072-410"", ""quantity"": 2000, ""note"": """" }
              ]
            }
          ]
        }";

        var result = await service.ParseAndEnrichOcrResultAsync(sideBySideJson, default);

        Assert.NotNull(result);
        Assert.Equal(2, result.Documents.Count);

        var doc1 = result.Documents[0];
        Assert.Equal("LẦN 19", doc1.Title);
        Assert.Equal(1500, doc1.ReportedTotal);
        Assert.Equal(1500, doc1.CalculatedTotal);
        Assert.True(doc1.IsTotalMatched);
        Assert.Equal(2, doc1.Items.Count);
        Assert.Equal(ProcessType.GoKhongMay, doc1.Items[1].ProcessType);
        Assert.NotNull(doc1.SourceRegion);
        Assert.True(doc1.SourceRegion.X < 0.1);

        var doc2 = result.Documents[1];
        Assert.Equal("LẦN 20", doc2.Title);
        Assert.Equal(2000, doc2.ReportedTotal);
        Assert.Equal(2000, doc2.CalculatedTotal);
        Assert.True(doc2.IsTotalMatched);
        Assert.Single(doc2.Items);
        Assert.Equal(2000, doc2.Items[0].Quantity);
        Assert.NotNull(doc2.SourceRegion);
        Assert.True(doc2.SourceRegion.X >= 0.5);
    }

    [Fact]
    public async Task BatchScan_WithImageHavingParallelTables_ShouldFlattenIntoSeparateResults()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var fakeOcrService = new FakeParallelOcrService();
        var controller = new OcrController(
            fakeOcrService,
            new SequenceService(context, NullLogger<SequenceService>.Instance),
            new StubExcelService(),
            context,
            NullLogger<OcrController>.Instance
        );

        var formFile = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "files", "two_tables.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var response = await controller.BatchScan(new List<IFormFile> { formFile }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var results = Assert.IsAssignableFrom<List<BatchOcrScanResultDto>>(okResult.Value);

        Assert.Equal(2, results.Count);
        Assert.Equal("LẦN 19", results[0].Title);
        Assert.Equal(1500, results[0].ReportedTotal);
        Assert.Equal("LẦN 20", results[1].Title);
        Assert.Equal(2000, results[1].ReportedTotal);
        Assert.All(results, r => Assert.True(r.IsSuccess));
    }

    private sealed class FakeParallelOcrService : IOcrExtractionService
    {
        public Task<OcrExtractionResponseDto> ExtractFromImageAsync(Stream imageStream, string mimeType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OcrExtractionResponseDto
            {
                Documents = new List<OcrDetectedDocumentDto>
                {
                    new OcrDetectedDocumentDto
                    {
                        DocumentId = "DOC-19",
                        Title = "LẦN 19",
                        ReportedTotal = 1500,
                        CalculatedTotal = 1500,
                        Items = new List<OcrItemDto>
                        {
                            new OcrItemDto { StyleCode = "42072-410", Quantity = 500 }
                        }
                    },
                    new OcrDetectedDocumentDto
                    {
                        DocumentId = "DOC-20",
                        Title = "LẦN 20",
                        ReportedTotal = 2000,
                        CalculatedTotal = 2000,
                        Items = new List<OcrItemDto>
                        {
                            new OcrItemDto { StyleCode = "42073-030", Quantity = 2000 }
                        }
                    }
                }
            });
        }

        public Task<List<OcrDetectedDocumentDto>> ReEnrichOcrDocumentsForPartnerAsync(List<OcrDetectedDocumentDto> documents, int? partnerFolderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(documents);
        }
    }

    private sealed class StubExcelService : IExcelImportExportService
    {
        public PklPreviewResponseDto CalculatePklBreakdown(CreateShipmentRequestDto request) => throw new NotImplementedException();
        public DocumentPreviewResponseDto CalculateDocumentPreview(CreateShipmentRequestDto request) => throw new NotImplementedException();
        public Task<byte[]> ExportShipmentMultiSheetExcelAsync(CreateShipmentRequestDto request) => Task.FromResult(new byte[] { 1 });
        public Task<byte[]> ExportSplitToZipAsync(CreateShipmentRequestDto goRequest, string goFileName, CreateShipmentRequestDto standardRequest, string standardFileName) => throw new NotImplementedException();
        public Task<byte[]> ExportShipmentToExcelAsync(ShipmentExportModel model) => throw new NotImplementedException();
        public byte[] GenerateProductMasterTemplate() => throw new NotImplementedException();
        public Task<byte[]> ExportProductMastersToExcelAsync() => throw new NotImplementedException();
        public Task<ImportResultDto> ImportProductMastersFromExcelAsync(Stream fileStream, bool updateExisting = true, int? folderId = null, ColumnMappingOverrideDto? mappingOverride = null) => throw new NotImplementedException();
        public Task<ImportPreviewResponseDto> PreviewProductMastersFromExcelAsync(Stream fileStream, int? folderId = null) => throw new NotImplementedException();
    }
}
