using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Data;
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
}
