using System.Text.Json;
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
            var controller = new TemplatesController(service, NullLogger<TemplatesController>.Instance);

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
}
