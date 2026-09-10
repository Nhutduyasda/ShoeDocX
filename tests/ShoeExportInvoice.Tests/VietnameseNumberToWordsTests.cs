using ClosedXML.Excel;
using ShoeExportInvoice.Api.Common;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Tests;

public class VietnameseNumberToWordsTests
{
    [Theory]
    [InlineData(61844.40, "Bằng chữ: Sáu mươi mốt nghìn tám trăm bốn mươi bốn đô la mỹ và bốn mươi cents")]
    [InlineData(30790.80, "Bằng chữ: Ba mươi nghìn bảy trăm chín mươi đô la mỹ và tám mươi cents")]
    [InlineData(15200.00, "Bằng chữ: Mười lăm nghìn hai trăm đô la mỹ chẵn")]
    [InlineData(105.05, "Bằng chữ: Một trăm linh năm đô la mỹ và năm cents")]
    [InlineData(21.01, "Bằng chữ: Hai mươi mốt đô la mỹ và một cents")]
    [InlineData(115.15, "Bằng chữ: Một trăm mười lăm đô la mỹ và mười lăm cents")]
    [InlineData(1000000.00, "Bằng chữ: Một triệu đô la mỹ chẵn")]
    [InlineData(0.00, "Bằng chữ: Không đô la mỹ chẵn")]
    public void ToVietnameseWords_ShouldProduceExactAccountingWords(double amountDouble, string expectedWords)
    {
        decimal amount = (decimal)amountDouble;
        string actual = VietnameseNumberToWordsHelper.ToVietnameseWords(amount);
        Assert.Equal(expectedWords, actual);
    }

    [Fact]
    public async Task ExportShipmentMultiSheetExcel_ShouldUpdateBangChuCellInInvSheet()
    {
        var service = new ExcelImportExportService(null!, null!);
        var request = new CreateShipmentRequestDto
        {
            InvoiceNo = "KMHD-TEST-WORDS-001",
            InvoiceDate = new DateTime(2026, 9, 9),
            ContractNo = "KM-CONTRACT-WORDS",
            PoSuffix = "(KM3.PO5.26)",
            CustomerName = "TEST CUSTOMER WORDS",
            Items = new List<CreateShipmentItemDto>
            {
                new()
                {
                    StyleCode = "42072-030",
                    Description = "Giày thể thao",
                    Quantity = 1000,
                    UnitPriceDAP = 15.20m, // 1000 * 15.20 = 15,200.00
                    UnitPriceCMT = 5.0m,
                    PairPerCarton = 12
                }
            }
        };

        var bytes = await service.ExportShipmentMultiSheetExcelAsync(request);
        Assert.NotNull(bytes);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var invSheet = wb.Worksheet("INV");

        // Tìm ô chứa "Bằng chữ:" trong sheet INV an toàn không gọi công thức CalcEngine
        IXLCell? wordsCell = null;
        for (int r = 1; r <= 80; r++)
        {
            for (int c = 1; c <= 6; c++)
            {
                var cell = invSheet.Cell(r, c);
                if (!cell.HasFormula && cell.GetString().Contains("Bằng chữ", StringComparison.OrdinalIgnoreCase))
                {
                    wordsCell = cell;
                    break;
                }
            }
            if (wordsCell != null) break;
        }
        Assert.NotNull(wordsCell);

        string cellValue = wordsCell.GetString();
        Assert.StartsWith("Bằng chữ:", cellValue);
        Assert.Equal("Bằng chữ: Mười lăm nghìn hai trăm đô la mỹ chẵn", cellValue);
    }
}
