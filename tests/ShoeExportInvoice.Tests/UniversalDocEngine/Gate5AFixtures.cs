using ClosedXML.Excel;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public static class Gate5AFixtures
{
    public static byte[] Workbook(string kind, bool filled = false)
    {
        using var wb = new XLWorkbook(); var s = wb.AddWorksheet("Data");
        s.Cell("A1").Value = kind == "warehouse" ? "Số phiếu" : "Document number"; s.Cell("B1").Value = filled ? "00123" : "sample";
        s.Cell("A2").Value = kind == "warehouse" ? "So phieu" : "Document number"; s.Cell("B2").Value = filled ? "00456" : "sample";
        s.Cell("A3").Value = "Printed value"; s.Cell("B3").Value = filled ? 19.5 : 0; s.Cell("B3").Style.NumberFormat.Format = "0.00";
        var labels = kind == "warehouse" ? new[] { "Mã hàng", "Mô tả", "Số lượng", "Đơn vị" } : new[] { "Product code", "Description", "Quantity", "Amount" };
        for (int i = 0; i < 4; i++) { s.Cell(5, i + 1).Value = labels[i]; s.Cell(5, i + 1).Style.Font.Bold = true; s.Cell(6, i + 1).Style.Border.BottomBorder = XLBorderStyleValues.Thin; }
        s.Cell("A6").Value = filled ? "00042" : "00000"; s.Cell("B6").Value = filled ? "Vải tổng hợp" : "sample";
        s.Cell("C6").Value = filled ? 2 : 0; s.Cell("D6").Value = kind == "warehouse" ? "cái" : "0";
        s.Cell("C6").Style.NumberFormat.Format = "0.00"; s.Cell("E6").FormulaA1 = "C6*2";
        s.Cell("A7").Value = "Total"; s.Cell("E7").FormulaA1 = "SUM(E6:E6)";
        s.Range("A8:C8").Merge().Value = "Signature";
        wb.AddWorksheet("Reference").Cell("A1").Value = "PRIVATE SYNTHETIC REFERENCE";
        using var data = new MemoryStream(); wb.SaveAs(data); return data.ToArray();
    }
    public static void Verify(byte[] bytes)
    {
        using var wb = new XLWorkbook(new MemoryStream(bytes)); var s = wb.Worksheet("Data");
        if (s.Cell("B1").GetString() != "00123" || s.Cell("A6").GetString() != "00042" || s.Cell("C6").GetValue<decimal>() != 2.5m ||
            s.Cell("C7").GetValue<decimal>() != 3m || s.Cell("E7").FormulaA1 != "C7*2" || s.Cell("E8").FormulaA1 != "SUM(E6:E7)" ||
            s.Cell("C7").Style.NumberFormat.Format != "0.00" || !s.MergedRanges.Any(r => r.RangeAddress.ToString() == "A9:C9") ||
            wb.Worksheet("Reference").Cell("A1").GetString() != "PRIVATE SYNTHETIC REFERENCE") throw new InvalidOperationException("SYNTHETIC_EXPORT_MISMATCH");
    }
}
