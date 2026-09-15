using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Controllers;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Models.Templates;
using ShoeExportInvoice.Api.Services;
using Xunit;

namespace ShoeExportInvoice.Tests;

public class MultiTenancyAndCreditTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public MultiTenancyAndCreditTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private class TestTenantService : ICurrentTenantService
    {
        public Guid CurrentId { get; set; } = Guid.NewGuid();
        public Guid TenantId => CurrentId;

        private readonly AppDbContext _db;
        public TestTenantService(AppDbContext db) => _db = db;

        public async Task<TenantWorkspace> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default)
        {
            var ws = await _db.TenantWorkspaces.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == CurrentId, cancellationToken);
            if (ws == null)
            {
                ws = new TenantWorkspace
                {
                    Id = CurrentId,
                    CompanyName = $"Tenant Company {CurrentId.ToString()[..6]}",
                    TaxCode = "1800999999",
                    AiCredits = 2
                };
                _db.TenantWorkspaces.Add(ws);
                await _db.SaveChangesAsync(cancellationToken);
            }
            return ws;
        }
    }

    [Fact]
    public async Task GlobalQueryFilter_IsolatesTemplates_BetweenTenants_ExceptSystemDefault()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // 1. Seed data using direct context without tenant filter
        using (var seedContext = new AppDbContext(_dbOptions, null))
        {
            seedContext.Database.EnsureCreated();

            // Default Template (Shared for all tenants)
            seedContext.CompanyTemplates.Add(new CompanyTemplate
            {
                Name = "Mẫu Mặc Định Hệ Thống",
                TemplateFileName = "system_default.xlsx",
                TemplateFilePath = "Templates/default.xlsx",
                ConfigJson = "{}",
                IsDefault = true,
                TenantId = null
            });

            // Template belonging to Tenant A
            seedContext.CompanyTemplates.Add(new CompanyTemplate
            {
                Name = "Mẫu Riêng Của Tenant A",
                TemplateFileName = "tenant_a.xlsx",
                TemplateFilePath = "Templates/tenant_a.xlsx",
                ConfigJson = "{}",
                IsDefault = false,
                TenantId = tenantA
            });

            // Template belonging to Tenant B
            seedContext.CompanyTemplates.Add(new CompanyTemplate
            {
                Name = "Mẫu Riêng Của Tenant B",
                TemplateFileName = "tenant_b.xlsx",
                TemplateFilePath = "Templates/tenant_b.xlsx",
                ConfigJson = "{}",
                IsDefault = false,
                TenantId = tenantB
            });

            await seedContext.SaveChangesAsync();
        }

        // 2. Query as Tenant A
        var tenantServiceA = new TestTenantService(null!) { CurrentId = tenantA };
        using (var contextA = new AppDbContext(_dbOptions, tenantServiceA))
        {
            var templatesA = await contextA.CompanyTemplates.ToListAsync();

            // Must see System Default AND Tenant A's template
            Assert.Contains(templatesA, t => t.Name == "Mẫu Mặc Định Hệ Thống");
            Assert.Contains(templatesA, t => t.Name == "Mẫu Riêng Của Tenant A");

            // Must NOT see Tenant B's template
            Assert.DoesNotContain(templatesA, t => t.Name == "Mẫu Riêng Của Tenant B");
            Assert.Equal(2, templatesA.Count);
        }

        // 3. Query as Tenant B
        var tenantServiceB = new TestTenantService(null!) { CurrentId = tenantB };
        using (var contextB = new AppDbContext(_dbOptions, tenantServiceB))
        {
            var templatesB = await contextB.CompanyTemplates.ToListAsync();

            // Must see System Default AND Tenant B's template
            Assert.Contains(templatesB, t => t.Name == "Mẫu Mặc Định Hệ Thống");
            Assert.Contains(templatesB, t => t.Name == "Mẫu Riêng Của Tenant B");

            // Must NOT see Tenant A's template
            Assert.DoesNotContain(templatesB, t => t.Name == "Mẫu Riêng Của Tenant A");
            Assert.Equal(2, templatesB.Count);
        }
    }

    [Fact]
    public async Task GlobalQueryFilter_IsolatesProductsAndFolders_BetweenTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // 1. Seed folder and product for Tenant A
        var tenantServiceA = new TestTenantService(null!) { CurrentId = tenantA };
        using (var contextA = new AppDbContext(_dbOptions, tenantServiceA))
        {
            contextA.Database.EnsureCreated();

            var folderA = new MasterDataFolder
            {
                Name = "Đối tác Khách hàng Tenant A",
                TenantId = tenantA
            };
            contextA.MasterDataFolders.Add(folderA);
            await contextA.SaveChangesAsync();

            contextA.ProductMasters.Add(new ProductMaster
            {
                StyleCode = "STYLE-A-001",
                Description = "Giày Thể Thao Tenant A",
                UnitPriceCMT = 5.0m,
                UnitPriceDAP = 15.0m,
                FolderId = folderA.Id,
                TenantId = tenantA
            });
            await contextA.SaveChangesAsync();
        }

        // 2. Query as Tenant B
        var tenantServiceB = new TestTenantService(null!) { CurrentId = tenantB };
        using (var contextB = new AppDbContext(_dbOptions, tenantServiceB))
        {
            var foldersB = await contextB.MasterDataFolders.ToListAsync();
            var productsB = await contextB.ProductMasters.ToListAsync();

            // Tenant B must not see Tenant A's folder or product
            Assert.Empty(foldersB);
            Assert.Empty(productsB);
        }
    }

    [Fact]
    public async Task AiAnalyze_DeductsCredit_WhenAvailable()
    {
        var tenantId = Guid.NewGuid();
        using var context = new AppDbContext(_dbOptions, null);
        context.Database.EnsureCreated();

        var tenantService = new TestTenantService(context) { CurrentId = tenantId };

        // Ensure workspace initialized with 2 credits
        var ws = await tenantService.GetCurrentWorkspaceAsync();
        Assert.Equal(2, ws.AiCredits);

        var excelService = new ExcelImportExportService(null!, null!);
        var parserService = new TemplateAiParserService(
            new System.Net.Http.HttpClient(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            excelService,
            NullLogger<TemplateAiParserService>.Instance);

        var templateService = new TemplateService(context, new TestWebHostEnvironment(), NullLogger<TemplateService>.Instance);

        var controller = new TemplatesController(
            templateService,
            parserService,
            NullLogger<TemplatesController>.Instance,
            tenantService,
            context);

        // Find sample template file
        var sampleTemplatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "Shipment_Template.xlsx");
        if (!File.Exists(sampleTemplatePath))
        {
            sampleTemplatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "Shipment_Template.xlsx");
        }

        using Stream stream = File.Exists(sampleTemplatePath) ? File.OpenRead(sampleTemplatePath) : new MemoryStream(new byte[100]);
        var formFile = new FormFile(stream, 0, stream.Length, "file", "Shipment_Template.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        var response = await controller.AiAnalyze(new AnalyzeTemplateForm { File = formFile });

        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.NotNull(okResult.Value);

        // Workspace credit must be deducted from 2 to 1
        var refreshedWs = await tenantService.GetCurrentWorkspaceAsync();
        Assert.Equal(1, refreshedWs.AiCredits);
    }

    [Fact]
    public async Task AiAnalyze_Returns402PaymentRequired_WhenCreditsDepleted()
    {
        var tenantId = Guid.NewGuid();
        using var context = new AppDbContext(_dbOptions, null);
        context.Database.EnsureCreated();

        var tenantService = new TestTenantService(context) { CurrentId = tenantId };

        // Deplete credits to 0
        var ws = await tenantService.GetCurrentWorkspaceAsync();
        ws.AiCredits = 0;
        await context.SaveChangesAsync();

        var excelService = new ExcelImportExportService(null!, null!);
        var parserService = new TemplateAiParserService(
            new System.Net.Http.HttpClient(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            excelService,
            NullLogger<TemplateAiParserService>.Instance);

        var templateService = new TemplateService(context, new TestWebHostEnvironment(), NullLogger<TemplateService>.Instance);

        var controller = new TemplatesController(
            templateService,
            parserService,
            NullLogger<TemplatesController>.Instance,
            tenantService,
            context);

        using var stream = new MemoryStream(new byte[100]);
        var formFile = new FormFile(stream, 0, stream.Length, "file", "Shipment_Template.xlsx");

        var response = await controller.AiAnalyze(new AnalyzeTemplateForm { File = formFile });

        var statusResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status402PaymentRequired, statusResult.StatusCode);
    }

    [Fact]
    public async Task AddDemoCredits_IncreasesCreditsSuccessfully()
    {
        var tenantId = Guid.NewGuid();
        using var context = new AppDbContext(_dbOptions, null);
        context.Database.EnsureCreated();

        var tenantService = new TestTenantService(context) { CurrentId = tenantId };
        var ws = await tenantService.GetCurrentWorkspaceAsync();
        ws.AiCredits = 0;
        await context.SaveChangesAsync();

        var templateService = new TemplateService(context, new TestWebHostEnvironment(), NullLogger<TemplateService>.Instance);
        var excelService = new ExcelImportExportService(null!, null!);
        var parserService = new TemplateAiParserService(
            new System.Net.Http.HttpClient(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            excelService,
            NullLogger<TemplateAiParserService>.Instance);

        var controller = new TemplatesController(
            templateService,
            parserService,
            NullLogger<TemplatesController>.Instance,
            tenantService,
            context);

        var addResult = await controller.AddDemoCredits();
        var okResult = Assert.IsType<OkObjectResult>(addResult);
        Assert.NotNull(okResult.Value);

        var refreshedWs = await tenantService.GetCurrentWorkspaceAsync();
        Assert.Equal(5, refreshedWs.AiCredits);
    }
}
