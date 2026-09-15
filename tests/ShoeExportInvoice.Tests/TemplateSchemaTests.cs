using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Controllers;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Models.Templates;
using ShoeExportInvoice.Api.Services;
using Xunit;

namespace ShoeExportInvoice.Tests;

public class TestWebHostEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "ShoeExportInvoice.Api";
    public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public class TemplateSchemaTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public TemplateSchemaTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    private static (AppDbContext Context, SqliteConnection Connection) CreateInMemoryDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }

    [Fact]
    public async Task DbInitializer_Seeds_DefaultCompanyTemplate_With_ValidConfig()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"template-test-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath};Pooling=False")
                .Options;

            using (var context = new AppDbContext(options))
            {
                await DbInitializer.InitializeAsync(context, NullLogger.Instance);

                var defaultTemplate = await context.CompanyTemplates.FirstOrDefaultAsync(t => t.IsDefault);
                Assert.NotNull(defaultTemplate);
                Assert.Equal("Mẫu Tiêu Chuẩn Kingmaker / Hải An (Mặc định)", defaultTemplate.Name);
                Assert.Equal("Shipment_Template.xlsx", defaultTemplate.TemplateFileName);
                Assert.True(defaultTemplate.IsDefault);

                var config = JsonSerializer.Deserialize<DocumentTemplateConfig>(defaultTemplate.ConfigJson);
                Assert.NotNull(config);
                Assert.Equal("INV", config.InvSheet.SheetName);
                Assert.Equal("J4", config.InvSheet.Header.InvoiceNoCell);
                Assert.Equal("J5", config.InvSheet.Header.DateCell);
                Assert.Equal(13, config.InvSheet.Table.StartRow);
                Assert.Equal("PKL", config.PklSheet.SheetName);
                Assert.Equal(12, config.PklSheet.StartRow);
            }
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
    }

    [Fact]
    public async Task TemplateService_GetAll_And_GetDefault_WorksCorrectly()
    {
        var (context, conn) = CreateInMemoryDb();
        using (conn)
        using (context)
        {
            var config = new DocumentTemplateConfig
            {
                TemplateName = "Mẫu Tiêu Chuẩn Kingmaker / Hải An (Mặc định)",
                InvSheet = new InvSheetConfig { Header = new InvHeaderCells { InvoiceNoCell = "J4" } }
            };
            var configJson = JsonSerializer.Serialize(config);

            context.CompanyTemplates.Add(new CompanyTemplate
            {
                Name = "Mẫu Tiêu Chuẩn Kingmaker / Hải An (Mặc định)",
                TemplateFileName = "Shipment_Template.xlsx",
                TemplateFilePath = "Templates/Shipment_Template.xlsx",
                ConfigJson = configJson,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new TemplateService(context, new TestWebHostEnvironment(), NullLogger<TemplateService>.Instance);

            var all = await service.GetAllTemplatesAsync();
            Assert.Single(all);
            Assert.Equal("Mẫu Tiêu Chuẩn Kingmaker / Hải An (Mặc định)", all[0].Name);
            Assert.NotNull(all[0].Config);
            Assert.Equal("J4", all[0].Config!.InvSheet.Header.InvoiceNoCell);

            var defaultTpl = await service.GetDefaultTemplateAsync();
            Assert.NotNull(defaultTpl);
            Assert.Equal(all[0].Id, defaultTpl.Id);
        }
    }

    [Fact]
    public async Task TemplateService_UpdateTemplateConfig_ValidatesJson()
    {
        var (context, conn) = CreateInMemoryDb();
        using (conn)
        using (context)
        {
            var config = new DocumentTemplateConfig();
            var configJson = JsonSerializer.Serialize(config);

            var tpl = new CompanyTemplate
            {
                Name = "Mẫu Tiêu Chuẩn",
                TemplateFileName = "Shipment_Template.xlsx",
                TemplateFilePath = "Templates/Shipment_Template.xlsx",
                ConfigJson = configJson,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            };
            context.CompanyTemplates.Add(tpl);
            await context.SaveChangesAsync();

            var service = new TemplateService(context, new TestWebHostEnvironment(), NullLogger<TemplateService>.Instance);

            // Invalid JSON should throw ArgumentException
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.UpdateTemplateConfigAsync(tpl.Id, "invalid-json"));

            // Valid JSON should update successfully
            var updatedConfig = new DocumentTemplateConfig
            {
                TemplateName = "Custom Template",
                InvSheet = new InvSheetConfig { Header = new InvHeaderCells { InvoiceNoCell = "K4" } },
                PklSheet = new PklSheetConfig { StartRow = 15 }
            };
            var validJson = JsonSerializer.Serialize(updatedConfig);
            var success = await service.UpdateTemplateConfigAsync(tpl.Id, validJson);
            Assert.True(success);

            var reloaded = await service.GetTemplateByIdAsync(tpl.Id);
            Assert.NotNull(reloaded);
            Assert.Equal("K4", reloaded.Config!.InvSheet.Header.InvoiceNoCell);
            Assert.Equal(15, reloaded.Config!.PklSheet.StartRow);
        }
    }

    [Fact]
    public async Task TemplatesController_Endpoints_ReturnExpectedResults()
    {
        var (context, conn) = CreateInMemoryDb();
        using (conn)
        using (context)
        {
            var config = new DocumentTemplateConfig();
            var configJson = JsonSerializer.Serialize(config);

            context.CompanyTemplates.Add(new CompanyTemplate
            {
                Name = "Mẫu Mặc Định",
                TemplateFileName = "Shipment_Template.xlsx",
                TemplateFilePath = "Templates/Shipment_Template.xlsx",
                ConfigJson = configJson,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new TemplateService(context, new TestWebHostEnvironment(), NullLogger<TemplateService>.Instance);
            var excelService = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance);
            var aiParser = new TemplateAiParserService(
                new HttpClient(),
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
                excelService,
                NullLogger<TemplateAiParserService>.Instance);
            var controller = new TemplatesController(service, aiParser, NullLogger<TemplatesController>.Instance);

            var getAllResult = await controller.GetAll();
            var okResult = Assert.IsType<OkObjectResult>(getAllResult.Result);
            var templates = Assert.IsAssignableFrom<List<CompanyTemplateDto>>(okResult.Value);
            Assert.Single(templates);

            var getByIdResult = await controller.GetById(templates[0].Id);
            var okById = Assert.IsType<OkObjectResult>(getByIdResult.Result);
            var resultDto = Assert.IsType<CompanyTemplateDto>(okById.Value);
            Assert.Equal(templates[0].Id, resultDto.Id);
        }
    }

    [Fact]
    public async Task ExportShipmentMultiSheetExcel_WithDefaultTemplate_MapsCoordinatesAccurately()
    {
        var service = new ExcelImportExportService(null!, null!);
        var request = new CreateShipmentRequestDto
        {
            InvoiceNo = "INV-DEFAULT-TEST-01",
            InvoiceDate = new DateTime(2026, 9, 15),
            ContractNo = "CTR-DEFAULT-01",
            CustomerName = "CÔNG TY TNHH HẢI AN",
            Address = "Hải Phòng, Việt Nam",
            Items = new List<CreateShipmentItemDto>
            {
                new()
                {
                    StyleCode = "TEST-STYLE-01",
                    Description = "Giày Thể Thao Mẫu",
                    Quantity = 24,
                    UnitPriceCMT = 5.0m,
                    UnitPriceDAP = 15.0m,
                    PairPerCarton = 12
                }
            }
        };

        var bytes = await service.ExportShipmentMultiSheetExcelAsync(request);
        Assert.NotNull(bytes);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var invSheet = wb.Worksheet("INV");
        Assert.NotNull(invSheet);

        // Default coordinates: J4 for InvoiceNo, J6 for ContractNo, D4 for BuyerName
        Assert.Equal("INV-DEFAULT-TEST-01", invSheet.Cell("J4").GetString());
        Assert.Equal("CTR-DEFAULT-01", invSheet.Cell("J6").GetString());
        Assert.Equal("CÔNG TY TNHH HẢI AN", invSheet.Cell("D4").GetString());
    }

    [Fact]
    public async Task ExportShipmentMultiSheetExcel_WithCustomTemplateConfig_MapsCoordinatesDynamically()
    {
        var (context, conn) = CreateInMemoryDb();
        using (conn)
        using (context)
        {
            var customConfig = new DocumentTemplateConfig
            {
                TemplateName = "Mẫu Tùy Chỉnh Điểm Tọa Độ",
                InvSheet = new InvSheetConfig
                {
                    SheetName = "INV",
                    Header = new InvHeaderCells
                    {
                        InvoiceNoCell = "J4",
                        DateCell = "J5",
                        ContractNoCell = "J6",
                        BuyerNameCell = "D4",
                        BuyerAddressCell = "D5",
                        DestinationCell = "D6"
                    },
                    Table = new InvTableColumns
                    {
                        StartRow = 13,
                        ItemCodeCol = "C",
                        DescriptionCol = "D",
                        QuantityCol = "E",
                        UnitCol = "F",
                        CmtUnitPriceCol = "G",
                        DapUnitPriceCol = "H",
                        CmtAmountCol = "I",
                        DapAmountCol = "J"
                    }
                },
                PklSheet = new PklSheetConfig
                {
                    SheetName = "PKL",
                    StartRow = 12,
                    CartonRangeCol = "A",
                    ItemCodeCol = "B",
                    DescriptionCol = "C",
                    QuantityCol = "D",
                    UnitCol = "E",
                    CartonsCol = "F",
                    NetWeightCol = "G",
                    GrossWeightCol = "H"
                }
            };

            var customTpl = new CompanyTemplate
            {
                Name = "Mẫu Tùy Chỉnh",
                TemplateFileName = "Shipment_Template.xlsx",
                TemplateFilePath = "Templates/Shipment_Template.xlsx",
                ConfigJson = JsonSerializer.Serialize(customConfig),
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            };
            context.CompanyTemplates.Add(customTpl);
            await context.SaveChangesAsync();

            var tplService = new TemplateService(context, new TestWebHostEnvironment(), NullLogger<TemplateService>.Instance);
            var service = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance, templateService: tplService);

            var request = new CreateShipmentRequestDto
            {
                TemplateId = customTpl.Id,
                InvoiceNo = "INV-CUSTOM-0099",
                InvoiceDate = new DateTime(2026, 9, 15),
                ContractNo = "CTR-CUSTOM-0099",
                CustomerName = "TẬP ĐOÀN ĐỐI TÁC MỚI",
                Address = "Bình Dương, Việt Nam",
                UseSavedSnapshot = true,
                Items = new List<CreateShipmentItemDto>
                {
                    new()
                    {
                        StyleCode = "STYLE-CUSTOM-01",
                        Description = "Giày Thời Trang Mới",
                        Quantity = 120,
                        UnitPriceCMT = 4.5m,
                        UnitPriceDAP = 14.0m,
                        PairPerCarton = 12
                    }
                }
            };

            var bytes = await service.ExportShipmentMultiSheetExcelAsync(request);
            Assert.NotNull(bytes);

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            var invSheet = wb.Worksheet("INV");
            Assert.NotNull(invSheet);

            Assert.Equal("INV-CUSTOM-0099", invSheet.Cell("J4").GetString());
            Assert.Equal("CTR-CUSTOM-0099", invSheet.Cell("J6").GetString());
            Assert.Equal("TẬP ĐOÀN ĐỐI TÁC MỚI", invSheet.Cell("D4").GetString());
            Assert.Equal("VIETNAM", invSheet.Cell("D6").GetString());

            // Check PKL sheet
            var pklSheet = wb.Worksheet("PKL");
            Assert.NotNull(pklSheet);
            Assert.Equal("STYLE-CUSTOM-01", pklSheet.Cell("B12").GetString());
            Assert.Equal(120, pklSheet.Cell("D12").GetDouble());
        }
    }

    [Fact]
    public async Task ExportShipmentToExcel_WithCustomTemplateConfig_MapsCoordinatesDynamically()
    {
        var (context, conn) = CreateInMemoryDb();
        using (conn)
        using (context)
        {
            var customConfig = new DocumentTemplateConfig
            {
                TemplateName = "Mẫu Shipment Model Tùy Chỉnh",
                InvSheet = new InvSheetConfig
                {
                    SheetName = "INV",
                    Header = new InvHeaderCells
                    {
                        InvoiceNoCell = "J4",
                        DateCell = "J5",
                        ContractNoCell = "J6",
                        BuyerNameCell = "D4",
                        DestinationCell = "D6"
                    },
                    Table = new InvTableColumns
                    {
                        StartRow = 13,
                        ItemCodeCol = "C",
                        DescriptionCol = "D",
                        QuantityCol = "E",
                        UnitCol = "F",
                        CmtUnitPriceCol = "G",
                        DapUnitPriceCol = "H",
                        CmtAmountCol = "I",
                        DapAmountCol = "J"
                    }
                }
            };

            var customTpl = new CompanyTemplate
            {
                Name = "Mẫu Model Tùy Chỉnh",
                TemplateFileName = "Shipment_Template.xlsx",
                TemplateFilePath = "Templates/Shipment_Template.xlsx",
                ConfigJson = JsonSerializer.Serialize(customConfig),
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            };
            context.CompanyTemplates.Add(customTpl);
            await context.SaveChangesAsync();

            var tplService = new TemplateService(context, new TestWebHostEnvironment(), NullLogger<TemplateService>.Instance);
            var service = new ExcelImportExportService(context, NullLogger<ExcelImportExportService>.Instance, templateService: tplService);

            var model = new ShipmentExportModel
            {
                TemplateId = customTpl.Id,
                InvoiceNo = "INV-MODEL-001",
                InvoiceDate = new DateTime(2026, 9, 15),
                ContractNo = "CTR-MODEL-001",
                CustomerName = "KHÁCH HÀNG MODEL TEST",
                Items = new List<ShipmentExportItemModel>
                {
                    new()
                    {
                        StyleCode = "MD-01",
                        Description = "Mô tả mẫu",
                        Quantity = 50,
                        UnitPriceCMT = 2.0m,
                        UnitPriceDAP = 8.0m,
                        PairPerCarton = 10
                    }
                }
            };

            var bytes = await service.ExportShipmentToExcelAsync(model);
            Assert.NotNull(bytes);

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);
            var invSheet = wb.Worksheet("INV");
            Assert.NotNull(invSheet);

            Assert.Equal("INV-MODEL-001", invSheet.Cell("J4").GetString());
            Assert.Equal("CTR-MODEL-001", invSheet.Cell("J6").GetString());
            Assert.Equal("KHÁCH HÀNG MODEL TEST", invSheet.Cell("D4").GetString());
            Assert.Equal("VIETNAM", invSheet.Cell("D6").GetString());
        }
    }

    private static string FindTemplateFilePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Templates", "Shipment_Template.xlsx"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "backend", "ShoeExportInvoice.Api", "Templates", "Shipment_Template.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "ShoeExportInvoice.Api", "Templates", "Shipment_Template.xlsx")
        };

        foreach (var c in candidates)
        {
            var fullPath = Path.GetFullPath(c);
            if (File.Exists(fullPath)) return fullPath;
        }

        throw new FileNotFoundException("Không tìm thấy file mẫu Shipment_Template.xlsx để chạy test.");
    }

    [Fact]
    public void TemplateAiParser_ExtractTextGrid_ProducesCompactAndAccurateGridRepresentation()
    {
        var templatePath = FindTemplateFilePath();
        using var stream = File.OpenRead(templatePath);

        var excelService = new ExcelImportExportService(null!, null!);
        var parserService = new TemplateAiParserService(
            new HttpClient(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            excelService,
            NullLogger<TemplateAiParserService>.Instance);

        var grid = parserService.ExtractTextGrid(stream);
        _output.WriteLine("=== EXTRACTED GRID ===");
        _output.WriteLine(grid);

        Assert.NotNull(grid);
        Assert.Contains("=== SHEET: INV ===", grid);
        Assert.Contains("=== SHEET: PKL ===", grid);
    }

    [Fact]
    public async Task TemplateAiParser_AnalyzeTemplateAsync_RecognizesTemplateCoordinates()
    {
        var templatePath = FindTemplateFilePath();
        using var stream = File.OpenRead(templatePath);

        var excelService = new ExcelImportExportService(null!, null!);
        var parserService = new TemplateAiParserService(
            new HttpClient(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            excelService,
            NullLogger<TemplateAiParserService>.Instance);

        var result = await parserService.AnalyzeTemplateAsync(stream, "Mẫu_Thử_Nghiệm_Kingmaker.xlsx");

        Assert.NotNull(result);
        Assert.NotNull(result.Config);
        Assert.Contains("Mẫu Thử Nghiệm Kingmaker", result.DetectedName);

        // Header coordinates in INV
        Assert.Equal("J4", result.Config.InvSheet.Header.InvoiceNoCell);
        Assert.Equal("J5", result.Config.InvSheet.Header.DateCell);
        Assert.Equal("J6", result.Config.InvSheet.Header.ContractNoCell);
        Assert.Equal("D4", result.Config.InvSheet.Header.BuyerNameCell);

        // Table coordinates
        Assert.Equal(13, result.Config.InvSheet.Table.StartRow);
        Assert.Equal("C", result.Config.InvSheet.Table.ItemCodeCol);

        // PKL coordinates
        Assert.Equal(12, result.Config.PklSheet.StartRow);
    }

    [Fact]
    public async Task TemplateAiParser_GenerateDummyPreviewAsync_ProducesCompleteDocumentPreview()
    {
        var templatePath = FindTemplateFilePath();
        using var stream = File.OpenRead(templatePath);

        var excelService = new ExcelImportExportService(null!, null!);
        var parserService = new TemplateAiParserService(
            new HttpClient(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            excelService,
            NullLogger<TemplateAiParserService>.Instance);

        var config = new DocumentTemplateConfig();
        var preview = await parserService.GenerateDummyPreviewAsync(stream, config);

        Assert.NotNull(preview);
        Assert.NotNull(preview.Invoice);
        Assert.NotNull(preview.PackingList);

        // 3 dummy items: Standard 1200, Standard 38 (with odd carton), Go 780
        Assert.Equal("INV-SAMPLE-001", preview.Invoice.InvoiceNo);
        Assert.Equal(3, preview.Invoice.Items.Count);
        Assert.Equal(1200 + 38 + 780, preview.Invoice.TotalQuantity); // 2018

        // Packing list breakdown has odd carton for 38 pairs (36 pairs in 3 ctn, 2 pairs in 1 odd ctn)
        Assert.Contains(preview.PackingList.BreakdownItems, b => b.IsOddCarton);
        Assert.Equal(2018, preview.PackingList.TotalQuantity);
    }
}
