using System.IO;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;
using Xunit;

namespace ShoeExportInvoice.Tests;

public class CustomsSettlementTests : IDisposable
{
    [Fact]
    public async Task SaveSettlement_RejectsOverlappingPeriodForSameContract()
    {
        using var context = new AppDbContext(_dbOptions);
        var folder = new MasterDataFolder { Name = "Contract A", ContractNo = "A" };
        context.Add(folder);
        await context.SaveChangesAsync();
        var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);

        await service.SaveSettlementPeriodAsync(new SaveSettlementPeriodRequestDto
        {
            Year = 2026, FromDate = new DateTime(2026, 1, 1), ToDate = new DateTime(2026, 1, 31),
            ContractFolderId = folder.Id, ContractNo = "A", Status = "Draft"
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveSettlementPeriodAsync(
            new SaveSettlementPeriodRequestDto
            {
                Year = 2026, FromDate = new DateTime(2026, 1, 15), ToDate = new DateTime(2026, 2, 15),
                ContractFolderId = folder.Id, ContractNo = "A", Status = "Draft"
            }));
        Assert.Contains("trùng", error.Message);
    }

    [Fact]
    public async Task Settlement_CanOnlyBeFinalizedThroughDedicatedOperation()
    {
        using var context = new AppDbContext(_dbOptions);
        var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);
        var request = new SaveSettlementPeriodRequestDto
        {
            Year = 2026,
            FromDate = new DateTime(2026, 3, 1),
            ToDate = new DateTime(2026, 3, 31),
            Status = "Finalized",
            Items = new List<SettlementItemDto>
            {
                new() { ProductCode = "SP-01", ProductName = "Sản phẩm", Unit = "đôi", OpeningBalance = 1 }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveSettlementPeriodAsync(request));

        request.Status = "Draft";
        var draft = await service.SaveSettlementPeriodAsync(request);
        var finalized = await service.FinalizeSettlementPeriodAsync(draft.Id);
        Assert.Equal("Finalized", finalized.Status);

        request.Id = draft.Id;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveSettlementPeriodAsync(request));
    }
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public CustomsSettlementTests()
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
    public async Task CalculateSettlement_ShouldAggregateClearedE52OrdersOnly()
    {
        using (var context = new AppDbContext(_dbOptions))
        {
            // Seed Master Data
            context.ProductMasters.Add(new ProductMaster
            {
                StyleCode = "SHOE-001",
                Description = "Running Shoes Air",
                Unit = "PRS",
                HsCode = "64041990"
            });

            // 1. Cleared E52 order (SHOULD be included)
            var order1 = new ShipmentOrder
            {
                ContractNo = "HD-2026",
                InvoiceNo = "INV-001",
                CustomerName = "Target Corp",
                CustomsDeclarationType = "E52",
                Status = ShipmentStatus.Cleared,
                InvoiceDate = new DateTime(2026, 3, 15),
                ClearanceDate = new DateTime(2026, 3, 16),
                DeclarationNo = "308883922820",
                Items = new List<ShipmentOrderItem>
                {
                    new ShipmentOrderItem { StyleCode = "SHOE-001", FullItemCode = "SHOE-001-BLK", Quantity = 100, UnitPriceCMT = 5, UnitPriceDAP = 10 }
                }
            };

            // 2. Draft E52 order (without declaration, SHOULD NOT be included)
            var order2 = new ShipmentOrder
            {
                ContractNo = "HD-2026",
                InvoiceNo = "INV-002",
                CustomerName = "Target Corp",
                CustomsDeclarationType = "E52",
                Status = ShipmentStatus.Draft,
                InvoiceDate = new DateTime(2026, 3, 20),
                Items = new List<ShipmentOrderItem>
                {
                    new ShipmentOrderItem { StyleCode = "SHOE-001", FullItemCode = "SHOE-001-RED", Quantity = 50 }
                }
            };

            // 3. Cleared B11 order (non-E52, SHOULD NOT be included)
            var order3 = new ShipmentOrder
            {
                ContractNo = "HD-2026",
                InvoiceNo = "INV-003",
                CustomerName = "Target Corp",
                CustomsDeclarationType = "B11",
                Status = ShipmentStatus.Cleared,
                InvoiceDate = new DateTime(2026, 3, 22),
                DeclarationNo = "308883922821",
                Items = new List<ShipmentOrderItem>
                {
                    new ShipmentOrderItem { StyleCode = "SHOE-001", FullItemCode = "SHOE-001-BLU", Quantity = 200 }
                }
            };

            context.ShipmentOrders.AddRange(order1, order2, order3);
            await context.SaveChangesAsync();
        }

        using (var context = new AppDbContext(_dbOptions))
        {
            var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);
            var report = await service.CalculateSettlementAsync(new CalculateSettlementRequestDto
            {
                FromDate = new DateTime(2026, 3, 1),
                ToDate = new DateTime(2026, 3, 31),
                ContractNo = "HD-2026"
            });

            Assert.NotNull(report);
            Assert.Single(report.Items);
            var item = report.Items[0];
            Assert.Equal("SHOE-001", item.ProductCode);
            Assert.Equal(100, item.InPeriodExport);
            Assert.Equal(100, report.TotalInPeriodExport);
        }
    }

    [Fact]
    public async Task CalculateSettlement_ShouldNormalizeStyleCodesAndMergeGoAndStandard()
    {
        using (var context = new AppDbContext(_dbOptions))
        {
            // E52 order with standard and .G line items for the same style
            var order = new ShipmentOrder
            {
                ContractNo = "HD-2026",
                InvoiceNo = "INV-100",
                CustomerName = "Walmart Inc",
                CustomsDeclarationType = "E52",
                Status = ShipmentStatus.Cleared,
                InvoiceDate = new DateTime(2026, 4, 10),
                DeclarationNo = "308883922830",
                Items = new List<ShipmentOrderItem>
                {
                    new ShipmentOrderItem { StyleCode = "45428-2LX", FullItemCode = "45428-2LX Thành hình", Quantity = 300 },
                    new ShipmentOrderItem { StyleCode = "45428-2LX.G", FullItemCode = "45428-2LX Gò không may", Quantity = 200 }
                }
            };

            context.ShipmentOrders.Add(order);
            await context.SaveChangesAsync();
        }

        using (var context = new AppDbContext(_dbOptions))
        {
            var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);
            var report = await service.CalculateSettlementAsync(new CalculateSettlementRequestDto
            {
                FromDate = new DateTime(2026, 4, 1),
                ToDate = new DateTime(2026, 4, 30)
            });

            Assert.NotNull(report);
            // Both items should merge into 45428-2LX with total 500
            Assert.Single(report.Items);
            var item = report.Items[0];
            Assert.Equal("45428-2LX", item.ProductCode);
            Assert.Equal(500, item.InPeriodExport);
        }
    }

    [Fact]
    public async Task SaveAndRetrieveSettlementPeriod_ShouldPersistAndInheritBalances()
    {
        using (var seed = new AppDbContext(_dbOptions))
        {
            seed.Add(new ShipmentOrder { InvoiceNo = "INV-Q1", CustomerName = "Test", ContractNo = "HD-01",
                Status = ShipmentStatus.Cleared, CustomsDeclarationType = "E52", ClearanceDate = new DateTime(2026, 2, 1),
                Items = [new ShipmentOrderItem { StyleCode = "SP-A", Quantity = 800 }] });
            await seed.SaveChangesAsync();
        }
        // 1. Save Period 1 (Q1/2026)
        using (var context = new AppDbContext(_dbOptions))
        {
            var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);
            var saveReq1 = new SaveSettlementPeriodRequestDto
            {
                Year = 2026,
                FromDate = new DateTime(2026, 1, 1),
                ToDate = new DateTime(2026, 3, 31),
                ContractNo = "HD-01",
                Items = new List<SettlementItemDto>
                {
                    new SettlementItemDto
                    {
                        ProductCode = "SP-A",
                        ProductName = "Giày thể thao A",
                        Unit = "PRS",
                        OpeningBalance = 100,
                        InPeriodProduction = 1000,
                        InPeriodExport = 800,
                        OtherExport = 50,
                        ClosingBalance = 250 // (100 + 1000) - (800 + 50) = 250
                    }
                }
            };

            var savedPeriod = await service.SaveSettlementPeriodAsync(saveReq1);
            Assert.True(savedPeriod.Id > 0);
        }

        // 2. Query periods list
        using (var context = new AppDbContext(_dbOptions))
        {
            var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);
            var periods = await service.GetSettlementPeriodsAsync();
            Assert.Single(periods);
            Assert.Equal(2026, periods[0].Year);
            Assert.Equal(250, periods[0].TotalClosingBalance);
        }

        // 3. For next period Q2/2026, CalculateSettlement should inherit SP-A's ClosingBalance as OpeningBalance
        using (var context = new AppDbContext(_dbOptions))
        {
            // Add a cleared order in Q2
            var orderQ2 = new ShipmentOrder
            {
                ContractNo = "HD-01",
                InvoiceNo = "INV-Q2",
                CustomerName = "Walmart Inc",
                CustomsDeclarationType = "E52",
                Status = ShipmentStatus.Cleared,
                InvoiceDate = new DateTime(2026, 5, 1),
                DeclarationNo = "308883922840",
                Items = new List<ShipmentOrderItem>
                {
                    new ShipmentOrderItem { StyleCode = "SP-A", FullItemCode = "SP-A Standard", Quantity = 150 }
                }
            };
            context.ShipmentOrders.Add(orderQ2);
            await context.SaveChangesAsync();

            var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);
            var reportQ2 = await service.CalculateSettlementAsync(new CalculateSettlementRequestDto
            {
                FromDate = new DateTime(2026, 4, 1),
                ToDate = new DateTime(2026, 6, 30),
                ContractNo = "HD-01"
            });

            Assert.Single(reportQ2.Items);
            var itemA = reportQ2.Items[0];
            Assert.Equal("SP-A", itemA.ProductCode);
            // Inherited from Q1 closing balance!
            Assert.Equal(250, itemA.OpeningBalance);
            Assert.Equal(150, itemA.InPeriodExport);
            // Closing balance = (250 + 0) - (150 + 0) = 100
            Assert.Equal(100, itemA.ClosingBalance);
        }
    }

    [Fact]
    public async Task ExportSettlementExcel_ShouldGenerateInternalReportWithVerifiedQuantities()
    {
        using var context = new AppDbContext(_dbOptions);
        var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);

        var report = new SettlementReportDto
        {
            Year = 2026,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            ContractNo = "HD-TEST-01",
            CompanyName = "CÔNG TY TNHH SẢN XUẤT GIÀY SHOEDOCX",
            TaxCode = "0312345678",
            Address = "KCN VSIP Quảng Ngãi",
            Items = new List<SettlementItemDto>
            {
                new SettlementItemDto
                {
                    Id = 1,
                    ProductCode = "STYLE-X",
                    ProductName = "Sneaker X",
                    Unit = "PRS",
                    OpeningBalance = 50,
                    InPeriodProduction = 500,
                    InPeriodExport = 400,
                    OtherExport = 10,
                    ClosingBalance = 140,
                    Note = "Hàng xuất xưởng"
                }
            }
        };

        context.Add(new ShipmentOrder { InvoiceNo = "INV-REPORT", CustomerName = "Test", ContractNo = "HD-TEST-01",
            Status = ShipmentStatus.Cleared, CustomsDeclarationType = "E52", ClearanceDate = new DateTime(2026, 2, 1),
            Items = [new ShipmentOrderItem { StyleCode = "STYLE-X", Quantity = 400 }] });
        await context.SaveChangesAsync();
        var excelBytes = await service.ExportSettlementExcelAsync(report);
        Assert.NotNull(excelBytes);
        Assert.True(excelBytes.Length > 0);

        // Verify with ClosedXML parser
        using var ms = new MemoryStream(excelBytes);
        using var workbook = new XLWorkbook(ms);
        var ws = workbook.Worksheet(1);
        Assert.NotNull(ws);

        // Check Form 16 header
        Assert.Contains("BÁO CÁO NỘI BỘ", ws.Cell("G1").GetString());
        Assert.Contains("BÁO CÁO ĐỐI CHIẾU NỘI BỘ", ws.Cell("A5").GetString());

        // Check line item row (row 12)
        Assert.Equal("1", ws.Cell("A12").GetString());
        Assert.Equal("STYLE-X", ws.Cell("B12").GetString());
        Assert.Equal("Sneaker X", ws.Cell("C12").GetString());
        Assert.Equal("PRS", ws.Cell("D12").GetString());
        Assert.Equal(50, ws.Cell("E12").GetDouble());
        Assert.Equal(500, ws.Cell("F12").GetDouble());
        Assert.Equal(400, ws.Cell("G12").GetDouble());
        Assert.Equal(10, ws.Cell("H12").GetDouble());
        // Verify formula in column I (9)
        Assert.Equal("E12+F12-G12-H12", ws.Cell("I12").FormulaA1);

        // Check total row (row 13)
        Assert.Equal("TỔNG CỘNG", ws.Cell("A13").GetString());
        Assert.Equal("SUM(E12:E12)", ws.Cell("E13").FormulaA1);
        Assert.Equal("SUM(I12:I12)", ws.Cell("I13").FormulaA1);

        // Check Sheet 1 & Sheet 2
        Assert.Equal(2, workbook.Worksheets.Count);
        Assert.Equal("Doi_Chieu_Noi_Bo", ws.Name);
        var ws2 = workbook.Worksheet(2);
        Assert.Equal("Bang_Ke_Chi_Tiet_E52", ws2.Name);
        Assert.Contains("BẢNG KÊ CHI TIẾT TỜ KHAI HẢI QUAN XUẤT KHẨU GIA CÔNG (E52)", ws2.Cell("A5").GetString());
        Assert.Equal("STT", ws2.Cell("A8").GetString());
        Assert.Equal("Số tờ khai hải quan", ws2.Cell("C8").GetString());
    }

    [Theory]
    [InlineData("42072-030", "42072-030")]
    [InlineData("42072-030.G", "42072-030")]
    [InlineData("42072-030-PO5", "42072-030")]
    [InlineData("42072-030/PO5", "42072-030")]
    [InlineData("42072-030.KM3.PO5.26", "42072-030")]
    [InlineData("42072-030.KM3.PO5.26.G", "42072-030")]
    [InlineData("42072-030 KM3.PO5.26", "42072-030")]
    public void NormalizeProductCode_ShouldStripPoAndGoSuffixes(string raw, string expected)
    {
        string actual = CustomsSettlementService.NormalizeProductCode(raw);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GetExportAnalytics_ShouldCalculate12MonthsAndTopStyles()
    {
        using (var context = new AppDbContext(_dbOptions))
        {
            var order = new ShipmentOrder
            {
                ContractNo = "HD-2026",
                InvoiceNo = "INV-STAT-1",
                CustomerName = "Target Corp",
                CustomsDeclarationType = "E52",
                CustomsChannel = 1, // Luồng Xanh
                Status = ShipmentStatus.Cleared,
                InvoiceDate = new DateTime(2026, 3, 10),
                ClearanceDate = new DateTime(2026, 3, 11),
                DeclarationNo = "308880001",
                Items = new List<ShipmentOrderItem>
                {
                    new ShipmentOrderItem { StyleCode = "42072-030.KM3.PO5.26", Quantity = 500, UnitPriceCMT = 2.5m, UnitPriceDAP = 15m },
                    new ShipmentOrderItem { StyleCode = "45428-2LX.G", Quantity = 300, UnitPriceCMT = 3.0m, UnitPriceDAP = 20m }
                }
            };

            context.ShipmentOrders.Add(order);
            await context.SaveChangesAsync();
        }

        using (var context = new AppDbContext(_dbOptions))
        {
            var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);
            var stats = await service.GetExportAnalyticsAsync(2026);

            Assert.NotNull(stats);
            Assert.Equal(2026, stats.Year);
            Assert.Equal(800, stats.TotalQuantity);
            Assert.Equal(500 * 15m + 300 * 20m, stats.TotalDap);
            Assert.Equal(500 * 2.5m + 300 * 3.0m, stats.TotalCmt);
            Assert.Equal(12, stats.MonthlyStats.Count);
            Assert.Equal(800, stats.MonthlyStats[2].Quantity); // March (month 3)
            Assert.Equal(1, stats.ChannelStats.GreenCount);
            Assert.Equal(100, stats.ChannelStats.GreenPercentage);
            Assert.Equal(2, stats.TopStyles.Count);
            Assert.Equal("42072-030", stats.TopStyles[0].StyleCode);
            Assert.Equal(500, stats.TopStyles[0].Quantity);
        }
    }

    [Fact]
    public async Task GetDrillDown_ShouldReturnClearedDeclarationsOnly()
    {
        using (var context = new AppDbContext(_dbOptions))
        {
            var order1 = new ShipmentOrder
            {
                ContractNo = "HD-DRILL",
                InvoiceNo = "INV-D1",
                CustomerName = "Nike Inc",
                CustomsDeclarationType = "E52",
                Status = ShipmentStatus.Cleared,
                InvoiceDate = new DateTime(2026, 5, 10),
                ClearanceDate = new DateTime(2026, 5, 12),
                DeclarationNo = "308889999001",
                Items = new List<ShipmentOrderItem>
                {
                    new ShipmentOrderItem { StyleCode = "45428-2LX", FullItemCode = "45428-2LX Standard", Quantity = 250, UnitPriceCMT = 4.5m, UnitPriceDAP = 12.0m },
                    new ShipmentOrderItem { StyleCode = "45428-2LX.G", FullItemCode = "45428-2LX Go", Quantity = 150, UnitPriceCMT = 4.0m, UnitPriceDAP = 11.5m }
                }
            };

            var order2 = new ShipmentOrder
            {
                ContractNo = "HD-DRILL",
                InvoiceNo = "INV-D2",
                CustomerName = "Nike Inc",
                CustomsDeclarationType = "B11", // Not E52
                Status = ShipmentStatus.Cleared,
                InvoiceDate = new DateTime(2026, 5, 15),
                DeclarationNo = "308889999002",
                Items = new List<ShipmentOrderItem>
                {
                    new ShipmentOrderItem { StyleCode = "45428-2LX", FullItemCode = "45428-2LX B11", Quantity = 100 }
                }
            };

            context.ShipmentOrders.AddRange(order1, order2);
            await context.SaveChangesAsync();
        }

        using (var context = new AppDbContext(_dbOptions))
        {
            var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);
            var drillDown = await service.GetDrillDownAsync(
                "45428-2LX",
                new DateTime(2026, 5, 1),
                new DateTime(2026, 5, 31),
                "HD-DRILL"
            );

            Assert.NotNull(drillDown);
            // Should contain the 2 items from order1 (Standard and .G both normalized to 45428-2LX)
            Assert.Equal(2, drillDown.Count);
            Assert.All(drillDown, d => Assert.Equal("308889999001", d.DeclarationNo));
            Assert.Equal(400, drillDown.Sum(d => d.Quantity));
        }
    }

    [Fact]
    public async Task MatchWarehouseRows_ShouldUpdateExistingAndAddNewAndDetectNegative()
    {
        using var context = new AppDbContext(_dbOptions);
        context.ProductMasters.Add(new ProductMaster
        {
            StyleCode = "SHOE-NEW-WH",
            Description = "Warehouse Only Shoe",
            Unit = "PRS",
            HsCode = "64041990"
        });
        await context.SaveChangesAsync();

        var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);

        var currentItems = new List<SettlementItemDto>
        {
            new SettlementItemDto
            {
                Id = 1,
                ProductCode = "SHOE-001",
                ProductName = "Running Shoe",
                Unit = "PRS",
                OpeningBalance = 0,
                InPeriodProduction = 0,
                InPeriodExport = 500, // Exported 500
                OtherExport = 0,
                ClosingBalance = -500 // Initially negative
            }
        };

        var warehouseRows = new List<WarehouseDataRowDto>
        {
            // Case 1: Match SHOE-001 with 100 opening + 200 production -> Total 300 vs 500 export -> Still negative -200
            new WarehouseDataRowDto
            {
                ProductCode = "SHOE-001",
                OpeningBalance = 100,
                InPeriodProduction = 200
            },
            // Case 2: New item in warehouse that had no export in period -> Added to table with InPeriodExport = 0
            new WarehouseDataRowDto
            {
                ProductCode = "SHOE-NEW-WH",
                OpeningBalance = 50,
                InPeriodProduction = 150
            }
        };

        var result = await service.MatchWarehouseRowsAsync(warehouseRows, currentItems);

        Assert.NotNull(result);
        Assert.Equal(1, result.MatchedCount);
        Assert.Equal(1, result.AddedFromWarehouseCount);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.NegativeItemCount);

        var matchedItem = result.Items.FirstOrDefault(i => i.ProductCode == "SHOE-001");
        Assert.NotNull(matchedItem);
        Assert.Equal(100, matchedItem.OpeningBalance);
        Assert.Equal(200, matchedItem.InPeriodProduction);
        Assert.Equal(500, matchedItem.InPeriodExport);
        Assert.Equal(-200, matchedItem.ClosingBalance);
        Assert.True(matchedItem.IsNegative);
        Assert.Equal(200, matchedItem.Discrepancy);

        var newItem = result.Items.FirstOrDefault(i => i.ProductCode == "SHOE-NEW-WH");
        Assert.NotNull(newItem);
        Assert.Equal(50, newItem.OpeningBalance);
        Assert.Equal(150, newItem.InPeriodProduction);
        Assert.Equal(0, newItem.InPeriodExport);
        Assert.Equal(200, newItem.ClosingBalance);
        Assert.False(newItem.IsNegative);
        Assert.Equal(0, newItem.Discrepancy);
        Assert.Equal("Warehouse Only Shoe", newItem.ProductName);
    }

    [Fact]
    public async Task ImportWarehouseExcel_ShouldParseHeadersAndMatchData()
    {
        using var context = new AppDbContext(_dbOptions);
        var service = new CustomsSettlementService(context, NullLogger<CustomsSettlementService>.Instance);

        // Create an in-memory Excel workbook simulating Warehouse report
        using var memoryStream = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("SoLieuKho");
            ws.Cell(1, 1).Value = "Mã sản phẩm / Style";
            ws.Cell(1, 2).Value = "Tồn đầu kỳ";
            ws.Cell(1, 3).Value = "Nhập sản xuất";

            ws.Cell(2, 1).Value = "42072-030.G"; // Needs normalization
            ws.Cell(2, 2).Value = 1000;
            ws.Cell(2, 3).Value = 5000;

            ws.Cell(3, 1).Value = "TỔNG CỘNG"; // Should be ignored
            ws.Cell(3, 2).Value = 1000;
            ws.Cell(3, 3).Value = 5000;

            wb.SaveAs(memoryStream);
        }

        memoryStream.Position = 0;

        var currentItems = new List<SettlementItemDto>
        {
            new SettlementItemDto
            {
                Id = 1,
                ProductCode = "42072-030",
                ProductName = "Sandals Comfort",
                Unit = "PRS",
                OpeningBalance = 0,
                InPeriodProduction = 0,
                InPeriodExport = 4000,
                OtherExport = 0,
                ClosingBalance = -4000
            }
        };

        var result = await service.ImportWarehouseExcelAsync(memoryStream, currentItems);

        Assert.NotNull(result);
        Assert.Equal(1, result.MatchedCount);
        Assert.Equal(0, result.NegativeItemCount); // (1000 + 5000) - 4000 = 2000 >= 0

        var item = result.Items.FirstOrDefault(i => i.ProductCode == "42072-030");
        Assert.NotNull(item);
        Assert.Equal(1000, item.OpeningBalance);
        Assert.Equal(5000, item.InPeriodProduction);
        Assert.Equal(2000, item.ClosingBalance);
        Assert.False(item.IsNegative);
    }
}
