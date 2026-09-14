using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Controllers;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;
using Xunit;

namespace ShoeExportInvoice.Tests;

public class CustomsArchiveTreeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public CustomsArchiveTreeTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
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
    public async Task GetArchiveTree_ReturnsHierarchicalStructure_Partner_Year_Contract_Channels()
    {
        using var context = new AppDbContext(_dbOptions);

        // 1. Tạo 2 đối tác gốc (Partner 1: Kingmaker III, Partner 2: Đối tác B)
        var partner1 = new MasterDataFolder
        {
            Name = "Kingmaker III",
            CustomerName = "CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR",
            ContractNo = "KM-HANEW/01-2025"
        };
        var partner2 = new MasterDataFolder
        {
            Name = "Đối tác B - ABC Footwear",
            CustomerName = "CÔNG TY TNHH ABC FOOTWEAR (VIỆT NAM)",
            ContractNo = "ABC-2026/01"
        };
        context.MasterDataFolders.AddRange(partner1, partner2);
        await context.SaveChangesAsync();

        // 2. Tạo các đơn hàng
        // Đơn 1 Kingmaker: Năm 2026, HĐ KM-HANEW/01-2025, Chờ đối soát (Exported, không có tờ khai)
        var kmOrder1 = new ShipmentOrder
        {
            InvoiceNo = "KMHD-NEW2026-0239",
            ContractFolderId = partner1.Id,
            ContractNo = "KM-HANEW/01-2025",
            CustomerName = partner1.CustomerName,
            InvoiceDate = new DateTime(2026, 9, 14),
            Status = ShipmentStatus.Exported
        };
        // Đơn 2 Kingmaker: Năm 2026, HĐ KM-HANEW/01-2025, Chờ đối soát (Exported, không có tờ khai)
        var kmOrder2 = new ShipmentOrder
        {
            InvoiceNo = "KMHD-NEW2026-0240",
            ContractFolderId = partner1.Id,
            ContractNo = "KM-HANEW/01-2025",
            CustomerName = partner1.CustomerName,
            InvoiceDate = new DateTime(2026, 9, 14),
            Status = ShipmentStatus.Exported
        };
        // Đơn 3 Kingmaker: Năm 2026, HĐ KM-HANEW/01-2025, Luồng 1 (Cleared, Channel 1)
        var kmOrder3 = new ShipmentOrder
        {
            InvoiceNo = "KMHD-NEW2026-0241",
            ContractFolderId = partner1.Id,
            ContractNo = "KM-HANEW/01-2025",
            CustomerName = partner1.CustomerName,
            InvoiceDate = new DateTime(2026, 8, 20),
            ClearanceDate = new DateTime(2026, 8, 24),
            DeclarationNo = "308883922820",
            CustomsChannel = 1,
            Status = ShipmentStatus.Cleared
        };
        // Đơn 4 Đối tác B: Năm 2026, HĐ ABC-2026/01, Luồng 2 (Cleared, Channel 2)
        var bOrder1 = new ShipmentOrder
        {
            InvoiceNo = "ABC-INV-2026-001",
            ContractFolderId = partner2.Id,
            ContractNo = "ABC-2026/01",
            CustomerName = partner2.CustomerName,
            InvoiceDate = new DateTime(2026, 9, 10),
            ClearanceDate = new DateTime(2026, 9, 12),
            DeclarationNo = "308889999999",
            CustomsChannel = 2,
            Status = ShipmentStatus.Cleared
        };

        context.ShipmentOrders.AddRange(kmOrder1, kmOrder2, kmOrder3, bOrder1);
        await context.SaveChangesAsync();

        var dummyEnv = new DummyWebHostEnvironment();
        var customsService = new CustomsDeclarationService(context, NullLogger<CustomsDeclarationService>.Instance, dummyEnv);
        var controller = new CustomsController(customsService, context, NullLogger<CustomsController>.Instance);

        // Act
        var actionResult = await controller.GetArchiveTree();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var tree = Assert.IsType<List<CustomsArchiveTreeNodeDto>>(okResult.Value);

        // Assert
        Assert.Equal(2, tree.Count); // 2 partners: Kingmaker III and Đối tác B

        // Kiểm tra Partner Kingmaker III
        var kmNode = tree.FirstOrDefault(n => n.PartnerFolderId == partner1.Id);
        Assert.NotNull(kmNode);
        Assert.Equal(3, kmNode.Count); // 3 đơn tổng cộng
        Assert.Single(kmNode.Children); // 1 năm (2026)

        var kmYear2026 = kmNode.Children[0];
        Assert.Equal("Năm 2026", kmYear2026.Title);
        Assert.Equal(3, kmYear2026.Count);
        Assert.Single(kmYear2026.Children); // 1 hợp đồng (KM-HANEW/01-2025)

        var kmContract = kmYear2026.Children[0];
        Assert.Equal("HĐ: KM-HANEW/01-2025", kmContract.Title);
        Assert.Equal(3, kmContract.Count);
        Assert.Equal(4, kmContract.Children.Count); // Green, Yellow, Red, Pending

        var greenNode = kmContract.Children.First(c => c.FilterType == "Green");
        Assert.Equal(1, greenNode.Count); // 1 đơn luồng xanh

        var pendingNode = kmContract.Children.First(c => c.FilterType == "Pending");
        Assert.Equal(2, pendingNode.Count); // 2 đơn chờ đối soát (0239 & 0240)

        // Kiểm tra Partner Đối tác B
        var bNode = tree.FirstOrDefault(n => n.PartnerFolderId == partner2.Id);
        Assert.NotNull(bNode);
        Assert.Equal(1, bNode.Count);

        var bYear2026 = bNode.Children[0];
        Assert.Equal(1, bYear2026.Count);
        var bContract = bYear2026.Children[0];
        Assert.Equal("HĐ: ABC-2026/01", bContract.Title);
        var bYellowNode = bContract.Children.First(c => c.FilterType == "Yellow");
        Assert.Equal(1, bYellowNode.Count);
    }

    [Fact]
    public async Task GetDeclarations_FiltersAccuratelyByPartner_Status_And_Channel()
    {
        using var context = new AppDbContext(_dbOptions);

        var partner1 = new MasterDataFolder
        {
            Name = "Kingmaker III",
            CustomerName = "CÔNG TY TNHH KINGMAKER III",
            ContractNo = "KM-HANEW/01-2025"
        };
        var partner2 = new MasterDataFolder
        {
            Name = "Đối tác B",
            CustomerName = "CÔNG TY TNHH ĐỐI TÁC B",
            ContractNo = "DTB-2026"
        };
        context.MasterDataFolders.AddRange(partner1, partner2);
        await context.SaveChangesAsync();

        var o1 = new ShipmentOrder
        {
            InvoiceNo = "KMHD-NEW2026-0239",
            ContractFolderId = partner1.Id,
            ContractNo = "KM-HANEW/01-2025",
            CustomerName = partner1.CustomerName,
            InvoiceDate = new DateTime(2026, 9, 14),
            Status = ShipmentStatus.Exported
        };
        var o2 = new ShipmentOrder
        {
            InvoiceNo = "KMHD-NEW2026-0240",
            ContractFolderId = partner1.Id,
            ContractNo = "KM-HANEW/01-2025",
            CustomerName = partner1.CustomerName,
            InvoiceDate = new DateTime(2026, 9, 14),
            Status = ShipmentStatus.Exported
        };
        var o3 = new ShipmentOrder
        {
            InvoiceNo = "KMHD-NEW2026-0241",
            ContractFolderId = partner1.Id,
            ContractNo = "KM-HANEW/01-2025",
            CustomerName = partner1.CustomerName,
            InvoiceDate = new DateTime(2026, 9, 14),
            DeclarationNo = "308883922820",
            CustomsChannel = 1,
            Status = ShipmentStatus.Cleared
        };
        var o4 = new ShipmentOrder
        {
            InvoiceNo = "DTB-INV-001",
            ContractFolderId = partner2.Id,
            ContractNo = "DTB-2026",
            CustomerName = partner2.CustomerName,
            InvoiceDate = new DateTime(2026, 9, 14),
            Status = ShipmentStatus.Exported
        };

        context.ShipmentOrders.AddRange(o1, o2, o3, o4);
        await context.SaveChangesAsync();

        var dummyEnv = new DummyWebHostEnvironment();
        var customsService = new CustomsDeclarationService(context, NullLogger<CustomsDeclarationService>.Instance, dummyEnv);
        var controller = new CustomsController(customsService, context, NullLogger<CustomsController>.Instance);

        // Test 1: Lọc nhánh "Chờ đối soát" của Kingmaker III
        var resPending = await controller.GetDeclarations(new CustomsDeclarationFilterDto
        {
            PartnerFolderId = partner1.Id,
            CustomsStatus = "Pending"
        });
        var okPending = Assert.IsType<OkObjectResult>(resPending.Result);
        var pagePending = Assert.IsType<PagedResultDto<CustomsDeclarationSummaryDto>>(okPending.Value);
        var listPending = pagePending.Items.ToList();
        Assert.Equal(2, listPending.Count);
        var invoices = listPending.Select(x => x.InvoiceNo).ToList();
        Assert.Contains("KMHD-NEW2026-0239", invoices);
        Assert.Contains("KMHD-NEW2026-0240", invoices);
        Assert.DoesNotContain("KMHD-NEW2026-0241", invoices);
        Assert.DoesNotContain("DTB-INV-001", invoices);

        // Test 2: Lọc luồng xanh của Kingmaker III
        var resGreen = await controller.GetDeclarations(new CustomsDeclarationFilterDto
        {
            PartnerFolderId = partner1.Id,
            Channel = 1,
            CustomsStatus = "Cleared"
        });
        var okGreen = Assert.IsType<OkObjectResult>(resGreen.Result);
        var pageGreen = Assert.IsType<PagedResultDto<CustomsDeclarationSummaryDto>>(okGreen.Value);
        var listGreen = pageGreen.Items.ToList();
        Assert.Single(listGreen);
        Assert.Equal("KMHD-NEW2026-0241", listGreen[0].InvoiceNo);

        // Test 3: Lọc theo từ khóa keyword
        var resKw = await controller.GetDeclarations(new CustomsDeclarationFilterDto
        {
            Keyword = "DTB-INV"
        });
        var okKw = Assert.IsType<OkObjectResult>(resKw.Result);
        var pageKw = Assert.IsType<PagedResultDto<CustomsDeclarationSummaryDto>>(okKw.Value);
        var listKw = pageKw.Items.ToList();
        Assert.Single(listKw);
        Assert.Equal("DTB-INV-001", listKw[0].InvoiceNo);
    }
}
