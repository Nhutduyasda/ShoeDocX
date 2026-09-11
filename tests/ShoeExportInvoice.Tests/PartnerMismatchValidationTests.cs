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

public class PartnerMismatchValidationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly AppDbContext _context;
    private readonly ProductMasterService _service;
    private readonly ProductMastersController _controller;

    public PartnerMismatchValidationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(_dbOptions);
        _context.Database.EnsureCreated();

        // 1. Tạo 2 thư mục Đối tác: Kingmaker III (Id: 1) và Đối tác B - ABC Footwear (Id: 2)
        var folderA = new MasterDataFolder
        {
            Id = 1,
            Name = "Kingmaker III",
            CustomerName = "CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR",
            DeliveryAddress = "SỐ 1, ĐƯỜNG 4A, KCN VSIP, QUẢNG NGÃI",
            ContractNo = "KM-HANEW/01-2025",
            PoSuffix = "(KM3.PO5.26)",
            DefaultPairsPerCarton = 12,
            DefaultUnit = "đôi"
        };
        var folderB = new MasterDataFolder
        {
            Id = 2,
            Name = "Đối tác B - ABC Footwear",
            CustomerName = "CÔNG TY TNHH ABC FOOTWEAR (VIỆT NAM)",
            DeliveryAddress = "LÔ B, KCN VSIP, BÌNH DƯƠNG",
            ContractNo = "ABC-2026/01",
            PoSuffix = "(ABC.PO1.26)",
            DefaultPairsPerCarton = 24,
            DefaultUnit = "đôi"
        };
        _context.MasterDataFolders.AddRange(folderA, folderB);

        // 2. Tạo sản phẩm cho Đối tác A
        _context.ProductMasters.Add(new ProductMaster
        {
            Id = 10,
            StyleCode = "42072-030",
            Description = "Giày Kingmaker Chuẩn",
            UnitPriceCMT = 2.45m,
            UnitPriceDAP = 18.50m,
            HsCode = "64041990",
            Unit = "đôi",
            PairPerCarton = 12,
            FolderId = 1
        });
        _context.ProductMasters.Add(new ProductMaster
        {
            Id = 11,
            StyleCode = "45428-2LX",
            Description = "Giày Kingmaker Running",
            UnitPriceCMT = 2.80m,
            UnitPriceDAP = 21.00m,
            HsCode = "64041990",
            Unit = "đôi",
            PairPerCarton = 12,
            FolderId = 1
        });

        // 3. Tạo sản phẩm cho Đối tác B (như đề bài: YL3564-100, BM5879-464)
        _context.ProductMasters.Add(new ProductMaster
        {
            Id = 20,
            StyleCode = "YL3564-100",
            Description = "Giày thể thao ABC Running Pro",
            UnitPriceCMT = 3.20m,
            UnitPriceDAP = 25.00m,
            UnitPriceCMT_Go = 2.90m,
            UnitPriceDAP_Go = 23.50m,
            HsCode = "64041990",
            Unit = "đôi",
            PairPerCarton = 24,
            FolderId = 2
        });
        _context.ProductMasters.Add(new ProductMaster
        {
            Id = 21,
            StyleCode = "BM5879-464",
            Description = "Giày slip-on ABC Comfort Walker",
            UnitPriceCMT = 2.90m,
            UnitPriceDAP = 22.50m,
            UnitPriceCMT_Go = 2.60m,
            UnitPriceDAP_Go = 20.80m,
            HsCode = "64041990",
            Unit = "đôi",
            PairPerCarton = 24,
            FolderId = 2
        });

        _context.SaveChanges();

        _service = new ProductMasterService(_context);
        _controller = new ProductMastersController(_service, new FakeExcelService(), NullLogger<ProductMastersController>.Instance);
    }

    [Fact]
    public async Task ValidateItems_WhenAllCodesBelongToCurrentPartner_ReturnsNoMismatch()
    {
        var request = new ValidateItemsRequest
        {
            CurrentPartnerFolderId = 1,
            StyleCodes = new List<string> { "42072-030", "45428-2LX" }
        };

        var result = await _service.ValidateItemsAsync(request);

        Assert.False(result.HasMismatch);
        Assert.Null(result.SuggestedPartnerFolderId);
        Assert.Null(result.SuggestedPartnerName);
        Assert.Equal(2, result.TotalCodes);
        Assert.All(result.Details, d => Assert.True(d.IsMatchedInCurrent));
    }

    [Fact]
    public async Task ValidateItems_WhenCodesBelongToPartnerB_DetectsMismatchAndSuggestsPartnerB()
    {
        // Đang ở Kingmaker III (Id: 1) nhưng nhập mã của Đối tác B (YL3564-100, BM5879-464)
        var request = new ValidateItemsRequest
        {
            CurrentPartnerFolderId = 1,
            StyleCodes = new List<string> { "YL3564-100", "BM5879-464" }
        };

        var result = await _service.ValidateItemsAsync(request);

        Assert.True(result.HasMismatch);
        Assert.Equal(2, result.SuggestedPartnerFolderId);
        Assert.Equal("Đối tác B - ABC Footwear", result.SuggestedPartnerName);
        Assert.Equal(2, result.MatchedCountInSuggested);
        Assert.Equal(2, result.TotalCodes);

        Assert.All(result.Details, d =>
        {
            Assert.False(d.IsMatchedInCurrent);
            Assert.Equal(2, d.MatchedFolderId);
            Assert.Equal("Đối tác B - ABC Footwear", d.MatchedFolderName);
            Assert.NotNull(d.MatchedProduct);
        });

        // Kiểm tra đúng đơn giá đã được gán vào MatchedProduct
        var ylDetail = result.Details.First(d => d.NormalizedCode == "YL3564-100");
        Assert.Equal(3.20m, ylDetail.MatchedProduct!.UnitPriceCMT);
        Assert.Equal(25.00m, ylDetail.MatchedProduct!.UnitPriceDAP);
        Assert.Equal(24, ylDetail.MatchedProduct!.PairPerCarton);
    }

    [Fact]
    public async Task ValidateItems_WithParenthesesNotesAndGoSuffix_NormalizesAndMatchesCorrectly()
    {
        // Dán mã kèm ký tự chú thích ngoặc đơn hoặc .G: "BM5879-464(KM3)", "YL3564-100 (X3)", "BM5879-464.G"
        var request = new ValidateItemsRequest
        {
            CurrentPartnerFolderId = 1,
            StyleCodes = new List<string> { "BM5879-464(KM3)", "YL3564-100 (X3)", "BM5879-464.G" }
        };

        var result = await _service.ValidateItemsAsync(request);

        Assert.True(result.HasMismatch);
        Assert.Equal(2, result.SuggestedPartnerFolderId);
        Assert.Equal(3, result.MatchedCountInSuggested);
        Assert.Equal(3, result.TotalCodes);

        var first = result.Details[0];
        Assert.Equal("BM5879-464(KM3)", first.RawCode);
        Assert.Equal("BM5879-464", first.NormalizedCode);
        Assert.False(first.IsMatchedInCurrent);
        Assert.Equal(2, first.MatchedFolderId);

        var third = result.Details[2];
        Assert.Equal("BM5879-464.G", third.RawCode);
        Assert.Equal("BM5879-464.G", third.NormalizedCode);
        Assert.False(third.IsMatchedInCurrent);
        Assert.Equal(2, third.MatchedFolderId);
    }

    [Fact]
    public async Task ValidateItems_WhenCodesAreCompletelyUnknown_ReturnsNoMismatch()
    {
        var request = new ValidateItemsRequest
        {
            CurrentPartnerFolderId = 1,
            StyleCodes = new List<string> { "UNKNOWN-999", "NOT-FOUND-001" }
        };

        var result = await _service.ValidateItemsAsync(request);

        Assert.False(result.HasMismatch);
        Assert.Null(result.SuggestedPartnerFolderId);
        Assert.Equal(0, result.MatchedCountInSuggested);
        Assert.All(result.Details, d =>
        {
            Assert.False(d.IsMatchedInCurrent);
            Assert.Null(d.MatchedFolderId);
            Assert.Null(d.MatchedProduct);
        });
    }

    [Fact]
    public async Task ValidateItems_WhenMajorityBelongsToPartnerB_TriggersSuggestion()
    {
        // 1 mã thuộc Kingmaker III ("42072-030"), 2 mã thuộc Đối tác B ("YL3564-100", "BM5879-464")
        var request = new ValidateItemsRequest
        {
            CurrentPartnerFolderId = 1,
            StyleCodes = new List<string> { "42072-030", "YL3564-100", "BM5879-464" }
        };

        var result = await _service.ValidateItemsAsync(request);

        Assert.True(result.HasMismatch);
        Assert.Equal(2, result.SuggestedPartnerFolderId);
        Assert.Equal(2, result.MatchedCountInSuggested);
        Assert.Equal(3, result.TotalCodes);

        Assert.True(result.Details.First(d => d.RawCode == "42072-030").IsMatchedInCurrent);
        Assert.False(result.Details.First(d => d.RawCode == "YL3564-100").IsMatchedInCurrent);
        Assert.False(result.Details.First(d => d.RawCode == "BM5879-464").IsMatchedInCurrent);
    }

    [Fact]
    public async Task Controller_ValidateItemsEndpoint_ReturnsOkWithResult()
    {
        var request = new ValidateItemsRequest
        {
            CurrentPartnerFolderId = 1,
            StyleCodes = new List<string> { "YL3564-100" }
        };

        var actionResult = await _controller.ValidateItems(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var valResult = Assert.IsType<ValidateItemsResult>(okResult.Value);

        Assert.True(valResult.HasMismatch);
        Assert.Equal(2, valResult.SuggestedPartnerFolderId);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
