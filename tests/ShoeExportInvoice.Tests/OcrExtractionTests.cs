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
}
