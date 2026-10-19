using ClosedXML.Excel;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public static class Gate5BFixtures
{
    public static byte[] Workbook(int sheets = 2, int rows = 1, bool filled = false)
    {
        using var workbook = new XLWorkbook();
        for (var n = 1; n <= sheets; n++)
        {
            var ws = workbook.AddWorksheet("Data" + n);
            ws.Cell("A1").Value = "Printed total"; ws.Cell("B1").Value = filled ? 6.5 * rows : 0;
            ws.Cell("A2").Value = "Printed total"; ws.Cell("B2").Value = 0;
            var labels = new[] { "Item ID", "Quantity", "Price", "Amount" };
            for (var col = 1; col <= 4; col++)
            { ws.Cell(3, col).Value = labels[col - 1]; ws.Cell(3, col).Style.Font.Bold = true; }
            for (var row = 4; row < 4 + rows; row++)
            {
                ws.Cell(row, 1).Value = filled ? "000" + (row - 3) : "0000";
                for (var col = 2; col <= 4; col++) ws.Cell(row, col).Value = filled ? col == 2 ? 2.5 : col == 3 ? 3 : 7.5 : 0;
                ws.Row(row).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Range(row, 2, row, 4).Style.NumberFormat.Format = "0.00";
                ws.Cell(row, 5).FormulaA1 = $"B{row}*C{row}";
            }
            ws.Cell(4 + rows, 1).Value = "Total"; ws.Cell(4 + rows, 5).FormulaA1 = $"SUM(E4:E{3 + rows})";
            ws.Range(5 + rows, 1, 5 + rows, 3).Merge().Value = "Signature";
        }
        workbook.AddWorksheet("Reference").Cell("A1").Value = "Dữ liệu tham chiếu 00123";
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }
}
