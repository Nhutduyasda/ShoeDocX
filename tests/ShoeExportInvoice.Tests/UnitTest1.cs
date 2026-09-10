using ClosedXML.Excel;
using Xunit.Abstractions;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Tests;

public class UnitTest1
{
    private readonly ITestOutputHelper _output;

    public UnitTest1(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CalculatePklBreakdown_ShouldProduceContinuousCartonRangesAndCorrectWeights()
    {
        var service = new ExcelImportExportService(null!, null!);
        var request = new CreateShipmentRequestDto
        {
            InvoiceNo = "KMHD-NEW2026-0233",
            PoSuffix = "(KM3.PO5.26)",
            Items = new List<CreateShipmentItemDto>
            {
                new() { StyleCode = "42072-030", Quantity = 36, Description = "Giày thể thao 1", PairPerCarton = 12 },
                new() { StyleCode = "45428-2LX", Quantity = 4032, Description = "Giày thể thao 2", PairPerCarton = 12 },
                new() { StyleCode = "51200-1BK", Quantity = 5, Description = "Giày thể thao 3", PairPerCarton = 12 },
                new() { StyleCode = "40700-060", Quantity = 29, Description = "Giày thể thao 4 (24+5)", PairPerCarton = 12 }
            }
        };

        var result = service.CalculatePklBreakdown(request);

        // Assertions:
        // Item 1: 36 pairs -> 3 full cartons (Range 1-3)
        // Item 2: 4032 pairs -> 336 full cartons (Range 4-339)
        // Item 3: 5 pairs -> 1 odd carton (Range 340-340)
        // Item 4: 29 pairs -> 2 full cartons (Range 341-342), 1 odd carton (Range 343-343)
        Assert.Equal(5, result.BreakdownItems.Count);

        var b0 = result.BreakdownItems[0];
        Assert.Equal("1-3", b0.CartonRange);
        Assert.Equal(1, b0.FromCarton);
        Assert.Equal(3, b0.ToCarton);
        Assert.Equal(3, b0.CartonCount);
        Assert.Equal(36, b0.Quantity);
        Assert.False(b0.IsOddCarton);
        Assert.Equal(9.60m, b0.NetWeight);
        Assert.Equal(10.0m, b0.GrossWeight); // Ceil(9.6 + 3*0.1) = Ceil(9.9) = 10

        var b1 = result.BreakdownItems[1];
        Assert.Equal("4-339", b1.CartonRange);
        Assert.Equal(4, b1.FromCarton);
        Assert.Equal(339, b1.ToCarton);
        Assert.Equal(336, b1.CartonCount);
        Assert.Equal(4032, b1.Quantity);
        Assert.False(b1.IsOddCarton);

        var b2 = result.BreakdownItems[2];
        Assert.Equal("340-340", b2.CartonRange);
        Assert.Equal(340, b2.FromCarton);
        Assert.Equal(340, b2.ToCarton);
        Assert.Equal(1, b2.CartonCount);
        Assert.Equal(5, b2.Quantity);
        Assert.True(b2.IsOddCarton);
        Assert.Equal(1.33m, b2.NetWeight); // Math.Round((5/12)*3.2, 2) = 1.33
        Assert.Equal(2.0m, b2.GrossWeight); // Ceil(1.33 + 0.1) = Ceil(1.43) = 2

        var b3 = result.BreakdownItems[3];
        Assert.Equal("341-342", b3.CartonRange);
        Assert.Equal(2, b3.CartonCount);
        Assert.Equal(24, b3.Quantity);
        Assert.False(b3.IsOddCarton);

        var b4 = result.BreakdownItems[4];
        Assert.Equal("343-343", b4.CartonRange);
        Assert.Equal(1, b4.CartonCount);
        Assert.Equal(5, b4.Quantity);
        Assert.True(b4.IsOddCarton);

        Assert.Equal(343, result.TotalCartons);
        Assert.Equal(36 + 4032 + 5 + 29, result.TotalQuantity);
    }

    [Fact]
    public async Task ExportShipmentMultiSheetExcel_ShouldPreserveThreeSheetsAndFormulas()
    {
        var service = new ExcelImportExportService(null!, null!);
        var request = new CreateShipmentRequestDto
        {
            InvoiceNo = "KMHD-TEST-2026-9999",
            InvoiceDate = new DateTime(2026, 9, 9),
            ContractNo = "KM-CONTRACT-01/2026",
            PoSuffix = "(KM3.PO5.26)",
            CustomerName = "KINGMAKER TEST CUSTOMER",
            Address = "TEST ADDRESS 123",
            DeliveryTerms = "DAP",
            PaymentTerms = "T/T",
            Items = new List<CreateShipmentItemDto>
            {
                new()
                {
                    StyleCode = "42072-030",
                    Description = "Giày thể thao nữ buộc dây",
                    Quantity = 36,
                    UnitPriceCMT = 3.2m,
                    UnitPriceDAP = 8.2m,
                    ProcessType = ProcessType.Standard,
                    PairPerCarton = 12
                },
                new()
                {
                    StyleCode = "45428-2LX",
                    Description = "Giày chạy bộ nam cổ thấp",
                    Quantity = 4032,
                    UnitPriceCMT = 3.5m,
                    UnitPriceDAP = 8.5m,
                    ProcessType = ProcessType.GoKhongMay,
                    PairPerCarton = 12
                },
                new()
                {
                    StyleCode = "51200-1BK",
                    Description = "Giày búp bê nữ có quai",
                    Quantity = 5,
                    UnitPriceCMT = 2.9m,
                    UnitPriceDAP = 7.9m,
                    ProcessType = ProcessType.Standard,
                    PairPerCarton = 12
                }
            }
        };

        var bytes = await service.ExportShipmentMultiSheetExcelAsync(request);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);

        // 1. Kiểm tra sheet INV
        var inv = wb.Worksheet("INV");
        Assert.NotNull(inv);
        Assert.Equal("KMHD-TEST-2026-9999", inv.Cell("J4").GetString());
        Assert.Equal("KM-CONTRACT-01/2026", inv.Cell("J6").GetString());
        Assert.Equal("KINGMAKER TEST CUSTOMER", inv.Cell("D4").GetString());

        // Hàng 13
        Assert.Equal("42072-030 (KM3.PO5.26)", inv.Cell("C13").GetString());
        Assert.Equal(36, inv.Cell("E13").GetDouble());
        Assert.True(inv.Cell("I13").HasFormula);
        Assert.Equal("G13*E13", inv.Cell("I13").FormulaA1);

        // Hàng 14 (Gò không may -> có đuôi .G)
        Assert.Equal("45428-2LX.G (KM3.PO5.26)", inv.Cell("C14").GetString());
        Assert.Equal(4032, inv.Cell("E14").GetDouble());

        // Hàng 15
        Assert.Equal("51200-1BK (KM3.PO5.26)", inv.Cell("C15").GetString());
        Assert.Equal(5, inv.Cell("E15").GetDouble());

        // Dòng tổng cộng INV (13 + 3 = 16)
        Assert.Equal("TỔNG CỘNG:", inv.Cell("B16").GetString());
        Assert.True(inv.Cell("E16").HasFormula);
        Assert.Equal("SUM(E13:E15)", inv.Cell("E16").FormulaA1);
        Assert.True(inv.Cell("I16").HasFormula);
        Assert.Equal("SUM(I13:I15)", inv.Cell("I16").FormulaA1);

        // 2. Kiểm tra sheet PKL
        var pkl = wb.Worksheet("PKL");
        Assert.NotNull(pkl);

        // Breakdown có 3 dòng (36 pairs -> 1 row, 4032 pairs -> 1 row, 5 pairs -> 1 row)
        // Dòng 12: 36 pairs (Kiện 1-3)
        Assert.Equal("42072-030 (KM3.PO5.26)", pkl.Cell("B12").GetString());
        Assert.Equal(36, pkl.Cell("D12").GetDouble());
        Assert.True(pkl.Cell("A12").HasFormula);
        Assert.True(pkl.Cell("F12").HasFormula);
        Assert.True(pkl.Cell("G12").HasFormula);
        Assert.True(pkl.Cell("H12").HasFormula);

        // Dòng tổng cộng PKL (12 + 3 = 15)
        Assert.Equal("TỔNG CỘNG:", pkl.Cell("B15").GetString());
        Assert.True(pkl.Cell("D15").HasFormula);
        Assert.Equal("SUM(D11:D14)", pkl.Cell("D15").FormulaA1);
        Assert.True(pkl.Cell("F15").HasFormula);
        Assert.Equal("SUM(F11:F14)", pkl.Cell("F15").FormulaA1);
        Assert.True(pkl.Cell("G15").HasFormula);
        Assert.Equal("SUM(G11:G14)", pkl.Cell("G15").FormulaA1);
        Assert.True(pkl.Cell("H15").HasFormula);
        Assert.Equal("ROUNDUP(G15+F15*0.1,0)", pkl.Cell("H15").FormulaA1);

        // 3. Kiểm tra Sheet2 (Master Data)
        var sheet2 = wb.Worksheet("Sheet2");
        Assert.NotNull(sheet2);
        Assert.Equal("42072-030", sheet2.Cell("A1").GetString());
        Assert.Equal("(KM3.PO5.26)", sheet2.Cell("B1").GetString());
        Assert.Equal("42072-030 (KM3.PO5.26)", sheet2.Cell("C1").GetString());
        Assert.Equal(3.2, sheet2.Cell("D1").GetDouble());
        Assert.Equal(8.2, sheet2.Cell("E1").GetDouble());

        Assert.Equal("45428-2LX", sheet2.Cell("A2").GetString());
        Assert.Equal("45428-2LX.G (KM3.PO5.26)", sheet2.Cell("C2").GetString());

        Assert.Equal("51200-1BK", sheet2.Cell("A3").GetString());
        Assert.Equal("51200-1BK (KM3.PO5.26)", sheet2.Cell("C3").GetString());
    }

    [Fact]
    public async Task CalculatePklBreakdown_WithDynamicPairPerCarton_CalculatesAccurately()
    {
        var service = new ExcelImportExportService(null!, null!);
        var request = new CreateShipmentRequestDto
        {
            InvoiceNo = "KMHD-DYNAMIC-CTN",
            PoSuffix = "(KM3.PO5.26)",
            Items = new List<CreateShipmentItemDto>
            {
                new()
                {
                    StyleCode = "STYLE-24PPC",
                    Description = "Giày 24 đôi/thùng",
                    Quantity = 50,
                    PairPerCarton = 24,
                    UnitPriceCMT = 4.0m,
                    UnitPriceDAP = 9.0m
                },
                new()
                {
                    StyleCode = "STYLE-10PPC",
                    Description = "Giày 10 đôi/thùng",
                    Quantity = 25,
                    PairPerCarton = 10,
                    UnitPriceCMT = 5.0m,
                    UnitPriceDAP = 10.0m
                }
            }
        };

        var result = service.CalculatePklBreakdown(request);

        // 1. Phân rã STYLE-24PPC: 50 đôi = 2 thùng chẵn (48 đôi) + 1 thùng lẻ (2 đôi)
        Assert.Equal(4, result.BreakdownItems.Count);

        var item24Full = result.BreakdownItems[0];
        Assert.Equal("1-2", item24Full.CartonRange);
        Assert.Equal(2, item24Full.CartonCount);
        Assert.Equal(24, item24Full.StandardPairPerCarton);
        Assert.Equal(24, item24Full.PairsPerCarton);
        Assert.Equal(48, item24Full.Quantity);
        Assert.False(item24Full.IsOddCarton);
        Assert.Equal(6.40m, item24Full.NetWeight); // (48/24)*3.2 = 6.40
        Assert.Equal(7.0m, item24Full.GrossWeight); // Ceil(6.40 + 2*0.1) = 7.0

        var item24Odd = result.BreakdownItems[1];
        Assert.Equal("3-3", item24Odd.CartonRange);
        Assert.Equal(1, item24Odd.CartonCount);
        Assert.Equal(24, item24Odd.StandardPairPerCarton);
        Assert.Equal(2, item24Odd.PairsPerCarton);
        Assert.Equal(2, item24Odd.Quantity);
        Assert.True(item24Odd.IsOddCarton);
        Assert.Equal(0.27m, item24Odd.NetWeight); // Math.Round((2/24)*3.2, 2) = 0.27
        Assert.Equal(1.0m, item24Odd.GrossWeight); // Ceil(0.27 + 0.1) = 1.0

        // 2. Phân rã STYLE-10PPC: 25 đôi = 2 thùng chẵn (20 đôi) + 1 thùng lẻ (5 đôi)
        var item10Full = result.BreakdownItems[2];
        Assert.Equal("4-5", item10Full.CartonRange);
        Assert.Equal(2, item10Full.CartonCount);
        Assert.Equal(10, item10Full.StandardPairPerCarton);
        Assert.Equal(10, item10Full.PairsPerCarton);
        Assert.Equal(20, item10Full.Quantity);
        Assert.False(item10Full.IsOddCarton);
        Assert.Equal(6.40m, item10Full.NetWeight); // (20/10)*3.2 = 6.40
        Assert.Equal(7.0m, item10Full.GrossWeight); // Ceil(6.40 + 2*0.1) = 7.0

        var item10Odd = result.BreakdownItems[3];
        Assert.Equal("6-6", item10Odd.CartonRange);
        Assert.Equal(1, item10Odd.CartonCount);
        Assert.Equal(10, item10Odd.StandardPairPerCarton);
        Assert.Equal(5, item10Odd.PairsPerCarton);
        Assert.Equal(5, item10Odd.Quantity);
        Assert.True(item10Odd.IsOddCarton);
        Assert.Equal(1.60m, item10Odd.NetWeight); // Math.Round((5/10)*3.2, 2) = 1.60
        Assert.Equal(2.0m, item10Odd.GrossWeight); // Ceil(1.60 + 0.1) = 2.0

        Assert.Equal(6, result.TotalCartons);
        Assert.Equal(75, result.TotalQuantity);

        // 3. Kiểm tra file Excel sinh ra với công thức động
        var excelBytes = await service.ExportShipmentMultiSheetExcelAsync(request);
        Assert.NotNull(excelBytes);

        using var ms = new MemoryStream(excelBytes);
        using var wb = new XLWorkbook(ms);

        var inv = wb.Worksheet("INV");
        Assert.Equal("E13/24", inv.Cell("K13").FormulaA1);
        Assert.Equal("E14/10", inv.Cell("K14").FormulaA1);

        var pkl = wb.Worksheet("PKL");
        // Dòng 12: STYLE-24PPC chẵn
        Assert.Equal("IF(D12<=0,0,IF(D12<24,1,D12/24))", pkl.Cell(12, 6).FormulaA1);
        Assert.Equal("D12/24", pkl.Cell(12, 9).FormulaA1);
        // Dòng 13: STYLE-24PPC lẻ
        Assert.Equal("IF(D13<=0,0,IF(D13<24,1,D13/24))", pkl.Cell(13, 6).FormulaA1);
        Assert.Equal("D13/24", pkl.Cell(13, 9).FormulaA1);

        // Dòng 14: STYLE-10PPC chẵn
        Assert.Equal("IF(D14<=0,0,IF(D14<10,1,D14/10))", pkl.Cell(14, 6).FormulaA1);
        Assert.Equal("D14/10", pkl.Cell(14, 9).FormulaA1);
        // Dòng 15: STYLE-10PPC lẻ
        Assert.Equal("IF(D15<=0,0,IF(D15<10,1,D15/10))", pkl.Cell(15, 6).FormulaA1);
        Assert.Equal("D15/10", pkl.Cell(15, 9).FormulaA1);
    }
}