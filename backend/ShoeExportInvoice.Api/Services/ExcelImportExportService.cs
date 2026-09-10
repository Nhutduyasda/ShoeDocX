using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Common;
using System.Globalization;

namespace ShoeExportInvoice.Api.Services;

public class ExcelImportExportService : IExcelImportExportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ExcelImportExportService> _logger;

    public ExcelImportExportService(AppDbContext context, ILogger<ExcelImportExportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string GetTemplatePath()
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Templates", "Shipment_Template.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "Shipment_Template.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "ShoeExportInvoice.Api", "Templates", "Shipment_Template.xlsx"),
            Path.Combine(AppContext.BaseDirectory, "Templates", "KM3-26-DH233.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "KM3-26-DH233.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "ShoeExportInvoice.Api", "Templates", "KM3-26-DH233.xlsx")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("Không tìm thấy file mẫu tại Templates/Shipment_Template.xlsx");
    }

    /// <summary>
    /// Xuất hóa đơn Commercial Invoice và Packing List trực tiếp từ file mẫu Shipment_Template.xlsx
    /// Tuyệt đối KHÔNG tạo new XLWorkbook(), giữ nguyên 100% format, header, footer, style và formulas.
    /// </summary>
    public async Task<byte[]> ExportShipmentToExcelAsync(ShipmentExportModel model)
    {
        var templatePath = GetTemplatePath();
        _logger.LogInformation("Mở trực tiếp file mẫu xuất hóa đơn: {TemplatePath}", templatePath);

        using var workbook = new XLWorkbook(templatePath);

        var invSheet = workbook.Worksheet("INV") 
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.ToUpper().Contains("INV"))
            ?? throw new InvalidOperationException("Không tìm thấy sheet 'INV' trong file mẫu.");

        var pklSheet = workbook.Worksheet("PKL") 
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.ToUpper().Contains("PKL"))
            ?? throw new InvalidOperationException("Không tìm thấy sheet 'PKL' trong file mẫu.");

        // ==========================================
        // 1. CẬP NHẬT HEADER INV
        // Ô J4 (Invoice No), J5 (Date), J6 (Hợp đồng)
        // ==========================================
        invSheet.Cell("J4").SetValue(model.InvoiceNo);
        invSheet.Cell("J5").SetValue(model.InvoiceDate.ToString("MMM dd,yyyy", CultureInfo.InvariantCulture).ToUpper());
        invSheet.Cell("J6").SetValue(model.ContractNo);

        if (!string.IsNullOrWhiteSpace(model.CustomerName))
        {
            invSheet.Cell("D4").SetValue(model.CustomerName);
        }
        if (!string.IsNullOrWhiteSpace(model.Address))
        {
            invSheet.Cell("D5").SetValue(model.Address);
        }
        if (!string.IsNullOrWhiteSpace(model.DeliveryTerms))
        {
            invSheet.Cell("J7").SetValue(model.DeliveryTerms);
        }
        if (!string.IsNullOrWhiteSpace(model.PaymentTerms))
        {
            invSheet.Cell("J8").SetValue(model.PaymentTerms);
        }

        // Cập nhật Header PKL (H4 và H5 trong mẫu tự động liên kết INV!J4, INV!J5)
        if (pklSheet.Cell("G6").GetString().Contains("Hợp đồng"))
        {
            pklSheet.Cell("G6").SetValue($"Hợp đồng: {model.ContractNo}");
        }

        var items = model.Items;
        int itemCount = items.Count;

        // ==========================================
        // 2. ĐIỀN DỮ LIỆU SHEET INV (bắt đầu từ dòng 13)
        // Mẫu gốc có 29 dòng dữ liệu (từ 13 đến 41), dòng 42 là TỔNG CỘNG
        // ==========================================
        const int invStartRow = 13;
        const int invDefaultTemplateRows = 29; // 13 đến 41

        if (itemCount > invDefaultTemplateRows)
        {
            int extraRows = itemCount - invDefaultTemplateRows;
            int insertAt = invStartRow + invDefaultTemplateRows - 1; // dòng 41
            invSheet.Row(insertAt).InsertRowsBelow(extraRows);

            // Copy style dòng mẫu 13 xuống các dòng mới
            var templateRow = invSheet.Row(invStartRow);
            for (int r = insertAt + 1; r <= insertAt + extraRows; r++)
            {
                var newRow = invSheet.Row(r);
                newRow.Height = templateRow.Height;
                for (int col = 1; col <= 11; col++)
                {
                    newRow.Cell(col).Style = templateRow.Cell(col).Style;
                }
            }
        }
        else if (itemCount > 0 && itemCount < invDefaultTemplateRows)
        {
            // Xóa bớt các dòng mẫu thừa để bảng co lại gọn gàng, viền và footer tự động nâng lên
            int deleteStart = invStartRow + itemCount;
            int deleteEnd = invStartRow + invDefaultTemplateRows - 1;
            invSheet.Rows(deleteStart, deleteEnd).Delete();
        }

        // Điền dữ liệu các mặt hàng vào Sheet INV
        for (int i = 0; i < itemCount; i++)
        {
            int row = invStartRow + i;
            var item = items[i];

            // B: Công thức STT theo mẫu
            invSheet.Cell(row, 2).FormulaA1 = $"IF(C{row}=\"\",\"\",SUBTOTAL(103,$C$13:C{row}))";

            // C: Mã hàng (Mã hình thể + PO Suffix nếu có)
            var itemCode = !string.IsNullOrWhiteSpace(item.FullItemCode)
                ? item.FullItemCode
                : (!string.IsNullOrWhiteSpace(model.PoSuffix) ? $"{item.StyleCode} {model.PoSuffix}" : item.StyleCode);
            invSheet.Cell(row, 3).SetValue(itemCode);

            // D: Mô tả hàng hóa
            invSheet.Cell(row, 4).SetValue(item.Description);

            // E: Số lượng
            invSheet.Cell(row, 5).SetValue(item.Quantity);

            // F: ĐVT
            invSheet.Cell(row, 6).SetValue(!string.IsNullOrWhiteSpace(item.Unit) ? item.Unit : "đôi");

            // G: Đơn giá CMT
            invSheet.Cell(row, 7).SetValue(item.UnitPriceCMT);

            // H: Đơn giá DAP
            invSheet.Cell(row, 8).SetValue(item.UnitPriceDAP);

            // I: Thành tiền CMT = G * E
            invSheet.Cell(row, 9).FormulaA1 = $"G{row}*E{row}";

            // J: Thành tiền DAP = H * E
            invSheet.Cell(row, 10).FormulaA1 = $"H{row}*E{row}";

            // K: Số thùng carton = E / PairPerCarton
            int pairCtn = item.PairPerCarton > 0 ? item.PairPerCarton : 12;
            invSheet.Cell(row, 11).FormulaA1 = $"E{row}/{pairCtn}";
        }

        // Cập nhật công thức dòng TỔNG CỘNG INV
        if (itemCount > 0)
        {
            int invLastDataRow = invStartRow + itemCount - 1;
            int invTotalRow = invLastDataRow + 1;

            invSheet.Cell(invTotalRow, 2).SetValue("TỔNG CỘNG:");
            invSheet.Cell(invTotalRow, 5).FormulaA1 = $"SUM(E13:E{invLastDataRow})";
            invSheet.Cell(invTotalRow, 9).FormulaA1 = $"SUM(I13:I{invLastDataRow})";
            invSheet.Cell(invTotalRow, 10).FormulaA1 = $"SUM(J13:J{invLastDataRow})";

            // Chuyển đổi Tổng số tiền DAP sang chữ tiếng Việt
            decimal totalDapAmount = model.Items.Sum(x => x.Quantity * x.UnitPriceDAP);
            string textInWords = VietnameseNumberToWordsHelper.ToVietnameseWords(totalDapAmount);
            UpdateWordsCellInInvSheet(invSheet, invTotalRow, textInWords);
        }

        // ==========================================
        // 3. ĐIỀN DỮ LIỆU SHEET PKL (bắt đầu từ dòng 12)
        // Mẫu gốc có 58 dòng dữ liệu (từ 12 đến 69), dòng 70 là TỔNG CỘNG
        // ==========================================
        const int pklStartRow = 12;
        const int pklDefaultTemplateRows = 58; // 12 đến 69

        if (itemCount > pklDefaultTemplateRows)
        {
            int extraRows = itemCount - pklDefaultTemplateRows;
            int insertAt = pklStartRow + pklDefaultTemplateRows - 1; // dòng 69
            pklSheet.Row(insertAt).InsertRowsBelow(extraRows);

            // Copy style dòng mẫu 12 xuống các dòng mới
            var templateRow = pklSheet.Row(pklStartRow);
            for (int r = insertAt + 1; r <= insertAt + extraRows; r++)
            {
                var newRow = pklSheet.Row(r);
                newRow.Height = templateRow.Height;
                for (int col = 1; col <= 11; col++)
                {
                    newRow.Cell(col).Style = templateRow.Cell(col).Style;
                }
            }
        }
        else if (itemCount > 0 && itemCount < pklDefaultTemplateRows)
        {
            // Xóa bớt các dòng mẫu thừa để bảng co lại gọn gàng
            int deleteStart = pklStartRow + itemCount;
            int deleteEnd = pklStartRow + pklDefaultTemplateRows - 1;
            pklSheet.Rows(deleteStart, deleteEnd).Delete();
        }

        // Điền dữ liệu vào Sheet PKL
        for (int i = 0; i < itemCount; i++)
        {
            int row = pklStartRow + i;
            var item = items[i];
            int pairCtn = item.PairPerCarton > 0 ? item.PairPerCarton : 12;

            // A: Số kiện (Carton Range): IF(F<=0,"",(SUM($F$11:F_prev)+1)&"-"&SUM($F$11:F_curr))
            int prevRow = row - 1;
            pklSheet.Cell(row, 1).FormulaA1 = $"IF(F{row}<=0,\"\",(SUM($F$11:F{prevRow})+1)&\"-\"&SUM($F$11:F{row}))";

            // B: Mã hàng (Full Item Code)
            var itemCode = !string.IsNullOrWhiteSpace(item.FullItemCode)
                ? item.FullItemCode
                : (!string.IsNullOrWhiteSpace(model.PoSuffix) ? $"{item.StyleCode} {model.PoSuffix}" : item.StyleCode);
            pklSheet.Cell(row, 2).SetValue(itemCode);

            // C: Mô tả hàng hóa
            pklSheet.Cell(row, 3).SetValue(item.Description);

            // D: Số lượng (Quantity)
            pklSheet.Cell(row, 4).SetValue(item.Quantity);

            // E: ĐVT
            pklSheet.Cell(row, 5).SetValue(!string.IsNullOrWhiteSpace(item.Unit) ? item.Unit : "đôi");

            // F: Số kiện: =IF(D<=0,0,IF(D<PairPerCtn,1,D/PairPerCtn))
            pklSheet.Cell(row, 6).FormulaA1 = $"IF(D{row}<=0,0,IF(D{row}<{pairCtn},1,D{row}/{pairCtn}))";

            // I: Tỷ lệ thùng
            pklSheet.Cell(row, 9).FormulaA1 = $"D{row}/{pairCtn}";

            // G: N.W (KGS) = I * 3.2
            pklSheet.Cell(row, 7).FormulaA1 = $"I{row}*3.2";

            // H: G.W (KGS) = ROUNDUP(G + F * 0.1, 0)
            pklSheet.Cell(row, 8).FormulaA1 = $"ROUNDUP(G{row}+F{row}*0.1,0)";

            // J: Tham chiếu số kiện
            pklSheet.Cell(row, 10).FormulaA1 = $"F{row}";
        }

        // Cập nhật công thức dòng TỔNG CỘNG PKL
        if (itemCount > 0)
        {
            int pklLastDataRow = pklStartRow + itemCount - 1;
            int pklTotalRow = pklLastDataRow + 1;

            pklSheet.Cell(pklTotalRow, 2).SetValue("TỔNG CỘNG:");
            pklSheet.Cell(pklTotalRow, 4).FormulaA1 = $"SUM(D11:D{pklLastDataRow})";
            pklSheet.Cell(pklTotalRow, 6).FormulaA1 = $"SUM(F11:F{pklLastDataRow})";
            pklSheet.Cell(pklTotalRow, 7).FormulaA1 = $"SUM(G11:G{pklLastDataRow})";
            pklSheet.Cell(pklTotalRow, 8).FormulaA1 = $"ROUNDUP(G{pklTotalRow}+F{pklTotalRow}*0.1,0)";
        }

        // Lưu ra MemoryStream và trả về file cho người dùng
        using var ms = new MemoryStream();
        await Task.Run(() => workbook.SaveAs(ms));
        return ms.ToArray();
    }

    /// <summary>
    /// Tính toán phân rã kiện chẵn/lẻ (quy tắc 12 đôi/thùng) và dải số kiện lũy kế (Continuous Range)
    /// </summary>
    public PklPreviewResponseDto CalculatePklBreakdown(CreateShipmentRequestDto request)
    {
        var styleCodes = request.Items
            .Select(x => x.StyleCode.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        Dictionary<string, ProductMaster>? products = null;
        if (_context != null && styleCodes.Count > 0)
        {
            products = _context.ProductMasters
                .Where(p => styleCodes.Contains(p.StyleCode.ToUpper()))
                .ToDictionary(p => p.StyleCode.ToUpper(), p => p);
        }

        var result = new PklPreviewResponseDto
        {
            InvoiceNo = request.InvoiceNo,
            PoSuffix = request.PoSuffix,
            BreakdownItems = new List<PklBreakdownItemDto>()
        };

        int currentCarton = 1;

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0) continue;

            ProductMaster? pm = null;
            if (products != null)
            {
                products.TryGetValue(item.StyleCode.Trim().ToUpperInvariant(), out pm);
            }

            if (pm != null)
            {
                if (string.IsNullOrWhiteSpace(item.Description))
                    item.Description = pm.Description;
                if (!item.UnitPriceCMT.HasValue || item.UnitPriceCMT == 0)
                    item.UnitPriceCMT = pm.UnitPriceCMT;
                if (!item.UnitPriceDAP.HasValue || item.UnitPriceDAP == 0)
                    item.UnitPriceDAP = pm.UnitPriceDAP;
                if (string.IsNullOrWhiteSpace(item.Unit))
                    item.Unit = pm.Unit;
            }

            int pairPerCarton = (item.PairPerCarton.HasValue && item.PairPerCarton.Value > 0)
                ? item.PairPerCarton.Value
                : (pm != null && pm.PairPerCarton > 0 ? pm.PairPerCarton : 12);
            item.PairPerCarton = pairPerCarton;

            int fullCartons = item.Quantity / pairPerCarton;
            int oddPairs = item.Quantity % pairPerCarton;

            string fullItemCode = !string.IsNullOrWhiteSpace(item.FullItemCode)
                ? item.FullItemCode
                : (item.ProcessType == ProcessType.GoKhongMay
                    ? $"{item.StyleCode}.G {request.PoSuffix}".Trim()
                    : $"{item.StyleCode} {request.PoSuffix}".Trim());

            string desc = item.Description ?? string.Empty;
            string procTypeName = item.ProcessType == ProcessType.GoKhongMay ? "Gò không may" : "Thành phẩm";

            // 1. Thùng chẵn (Full Cartons)
            if (fullCartons > 0)
            {
                int from = currentCarton;
                int to = currentCarton + fullCartons - 1;
                currentCarton = to + 1;
                int qty = fullCartons * pairPerCarton;
                decimal netWeight = Math.Round(((decimal)qty / (decimal)pairPerCarton) * 3.2m, 2);
                decimal grossWeight = Math.Ceiling(netWeight + ((decimal)fullCartons * 0.1m));

                result.BreakdownItems.Add(new PklBreakdownItemDto
                {
                    StyleCode = item.StyleCode,
                    FullItemCode = fullItemCode,
                    Description = desc,
                    ProcessType = item.ProcessType,
                    ProcessTypeName = procTypeName,
                    CartonRange = $"{from}-{to}",
                    FromCarton = from,
                    ToCarton = to,
                    CartonCount = fullCartons,
                    PairsPerCarton = pairPerCarton,
                    StandardPairPerCarton = pairPerCarton,
                    Quantity = qty,
                    IsOddCarton = false,
                    NetWeight = netWeight,
                    GrossWeight = grossWeight
                });
            }

            // 2. Thùng lẻ (Odd Cartons)
            if (oddPairs > 0)
            {
                int from = currentCarton;
                int to = currentCarton;
                currentCarton = to + 1;
                int qty = oddPairs;
                decimal netWeight = Math.Round(((decimal)qty / (decimal)pairPerCarton) * 3.2m, 2);
                decimal grossWeight = Math.Ceiling(netWeight + (1m * 0.1m));

                result.BreakdownItems.Add(new PklBreakdownItemDto
                {
                    StyleCode = item.StyleCode,
                    FullItemCode = fullItemCode,
                    Description = desc,
                    ProcessType = item.ProcessType,
                    ProcessTypeName = procTypeName,
                    CartonRange = $"{from}-{to}",
                    FromCarton = from,
                    ToCarton = to,
                    CartonCount = 1,
                    PairsPerCarton = oddPairs,
                    StandardPairPerCarton = pairPerCarton,
                    Quantity = qty,
                    IsOddCarton = true,
                    NetWeight = netWeight,
                    GrossWeight = grossWeight
                });
            }
        }

        result.TotalQuantity = result.BreakdownItems.Sum(x => x.Quantity);
        result.TotalCartons = result.BreakdownItems.Sum(x => x.CartonCount);
        result.TotalNetWeight = result.BreakdownItems.Sum(x => x.NetWeight);
        result.TotalGrossWeight = result.BreakdownItems.Sum(x => x.GrossWeight);

        return result;
    }

    /// <summary>
    /// Xuất hóa đơn Commercial Invoice và Packing List đa sheet hoàn chỉnh trực tiếp từ file mẫu
    /// Giữ nguyên 100% cấu trúc 3 sheet (INV, PKL, Sheet2) và bảo toàn mọi công thức, viền kẻ, style.
    /// </summary>
    public async Task<byte[]> ExportShipmentMultiSheetExcelAsync(CreateShipmentRequestDto request)
    {
        var templatePath = GetTemplatePath();
        _logger?.LogInformation("Mở trực tiếp file mẫu xuất hóa đơn đa sheet: {TemplatePath}", templatePath);

        // Nạp thông tin sản phẩm thiếu từ Database nếu có DbContext
        Dictionary<string, ProductMaster>? dbProducts = null;
        if (_context != null)
        {
            var styleCodes = request.Items.Select(x => x.StyleCode.Trim().ToUpperInvariant()).Distinct().ToList();
            dbProducts = await _context.ProductMasters
                .Where(p => styleCodes.Contains(p.StyleCode.ToUpper()))
                .ToDictionaryAsync(p => p.StyleCode.ToUpper(), p => p);
        }

        foreach (var item in request.Items)
        {
            ProductMaster? pm = null;
            if (dbProducts != null)
            {
                dbProducts.TryGetValue(item.StyleCode.Trim().ToUpperInvariant(), out pm);
            }

            if (pm != null)
            {
                if (string.IsNullOrWhiteSpace(item.Description))
                    item.Description = pm.Description;

                // Áp đơn giá Gò nếu là hàng Gò không may và PM có đơn giá Gò riêng
                bool isGo = item.ProcessType == ProcessType.GoKhongMay;
                if (!item.UnitPriceCMT.HasValue || item.UnitPriceCMT == 0)
                {
                    item.UnitPriceCMT = (isGo && pm.UnitPriceCMT_Go.HasValue && pm.UnitPriceCMT_Go.Value > 0)
                        ? pm.UnitPriceCMT_Go.Value
                        : pm.UnitPriceCMT;
                }
                if (!item.UnitPriceDAP.HasValue || item.UnitPriceDAP == 0)
                {
                    item.UnitPriceDAP = (isGo && pm.UnitPriceDAP_Go.HasValue && pm.UnitPriceDAP_Go.Value > 0)
                        ? pm.UnitPriceDAP_Go.Value
                        : pm.UnitPriceDAP;
                }
                if (string.IsNullOrWhiteSpace(item.Unit))
                    item.Unit = pm.Unit;
            }

            int pairPerCarton = (item.PairPerCarton.HasValue && item.PairPerCarton.Value > 0)
                ? item.PairPerCarton.Value
                : (pm != null && pm.PairPerCarton > 0 ? pm.PairPerCarton : 12);
            item.PairPerCarton = pairPerCarton;
        }

        using var workbook = new XLWorkbook(templatePath);

        var invSheet = workbook.Worksheet("INV") 
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.ToUpper().Contains("INV"))
            ?? throw new InvalidOperationException("Không tìm thấy sheet 'INV' trong file mẫu.");

        var pklSheet = workbook.Worksheet("PKL") 
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.ToUpper().Contains("PKL"))
            ?? throw new InvalidOperationException("Không tìm thấy sheet 'PKL' trong file mẫu.");

        var sheet2 = workbook.Worksheet("Sheet2")
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Sheet2", StringComparison.OrdinalIgnoreCase));

        // ==========================================
        // 1. CẬP NHẬT HEADER INV
        // ==========================================
        invSheet.Cell("J4").SetValue(request.InvoiceNo);
        invSheet.Cell("J5").SetValue(request.InvoiceDate.ToString("MMM dd,yyyy", CultureInfo.InvariantCulture).ToUpper());
        invSheet.Cell("J6").SetValue(request.ContractNo);

        if (!string.IsNullOrWhiteSpace(request.CustomerName))
            invSheet.Cell("D4").SetValue(request.CustomerName);
        if (!string.IsNullOrWhiteSpace(request.Address))
            invSheet.Cell("D5").SetValue(request.Address);
        if (!string.IsNullOrWhiteSpace(request.DeliveryTerms))
            invSheet.Cell("J7").SetValue(request.DeliveryTerms);
        if (!string.IsNullOrWhiteSpace(request.PaymentTerms))
            invSheet.Cell("J8").SetValue(request.PaymentTerms);

        // Cập nhật Header PKL
        if (pklSheet.Cell("G6").GetString().Contains("Hợp đồng"))
        {
            pklSheet.Cell("G6").SetValue($"Hợp đồng: {request.ContractNo}");
        }

        // ==========================================
        // 2. ĐIỀN DỮ LIỆU SHEET INV (từ dòng 13)
        // ==========================================
        var items = request.Items;
        int invItemCount = items.Count;
        const int invStartRow = 13;
        const int invDefaultTemplateRows = 29; // Dòng 13 đến 41

        if (invItemCount > invDefaultTemplateRows)
        {
            int extraRows = invItemCount - invDefaultTemplateRows;
            int insertAt = invStartRow + invDefaultTemplateRows - 1; // dòng 41
            invSheet.Row(insertAt).InsertRowsBelow(extraRows);

            var templateRow = invSheet.Row(invStartRow);
            for (int r = insertAt + 1; r <= insertAt + extraRows; r++)
            {
                var newRow = invSheet.Row(r);
                newRow.Height = templateRow.Height;
                for (int col = 1; col <= 11; col++)
                {
                    newRow.Cell(col).Style = templateRow.Cell(col).Style;
                }
            }
        }
        else if (invItemCount > 0 && invItemCount < invDefaultTemplateRows)
        {
            int deleteStart = invStartRow + invItemCount;
            int deleteEnd = invStartRow + invDefaultTemplateRows - 1;
            invSheet.Rows(deleteStart, deleteEnd).Delete();
        }

        for (int i = 0; i < invItemCount; i++)
        {
            int row = invStartRow + i;
            var item = items[i];

            invSheet.Cell(row, 2).FormulaA1 = $"IF(C{row}=\"\",\"\",SUBTOTAL(103,$C$13:C{row}))";

            var fullCode = !string.IsNullOrWhiteSpace(item.FullItemCode)
                ? item.FullItemCode
                : (item.ProcessType == ProcessType.GoKhongMay
                    ? $"{item.StyleCode}.G {request.PoSuffix}".Trim()
                    : $"{item.StyleCode} {request.PoSuffix}".Trim());

            invSheet.Cell(row, 3).SetValue(fullCode);
            invSheet.Cell(row, 4).SetValue(item.Description ?? string.Empty);
            invSheet.Cell(row, 5).SetValue(item.Quantity);
            invSheet.Cell(row, 6).SetValue(!string.IsNullOrWhiteSpace(item.Unit) ? item.Unit : "đôi");
            invSheet.Cell(row, 7).SetValue(item.UnitPriceCMT ?? 0m);
            invSheet.Cell(row, 8).SetValue(item.UnitPriceDAP ?? 0m);
            int pairCtn = (item.PairPerCarton.HasValue && item.PairPerCarton.Value > 0) ? item.PairPerCarton.Value : 12;
            invSheet.Cell(row, 9).FormulaA1 = $"G{row}*E{row}";
            invSheet.Cell(row, 10).FormulaA1 = $"H{row}*E{row}";
            invSheet.Cell(row, 11).FormulaA1 = $"E{row}/{pairCtn}";
        }

        int invLastDataRow = invItemCount > 0 ? (invStartRow + invItemCount - 1) : invStartRow;
        int invTotalRow = invLastDataRow + 1;
        invSheet.Cell(invTotalRow, 2).SetValue("TỔNG CỘNG:");
        invSheet.Cell(invTotalRow, 5).FormulaA1 = $"SUM(E13:E{invLastDataRow})";
        invSheet.Cell(invTotalRow, 9).FormulaA1 = $"SUM(I13:I{invLastDataRow})";
        invSheet.Cell(invTotalRow, 10).FormulaA1 = $"SUM(J13:J{invLastDataRow})";

        // Chuyển đổi Tổng số tiền DAP sang chữ tiếng Việt
        decimal totalDapAmount = items.Sum(x => (decimal)x.Quantity * (x.UnitPriceDAP ?? 0m));
        string textInWords = VietnameseNumberToWordsHelper.ToVietnameseWords(totalDapAmount);
        UpdateWordsCellInInvSheet(invSheet, invTotalRow, textInWords);

        // ==========================================
        // 3. ĐIỀN DỮ LIỆU SHEET PKL (từ dòng 12)
        // ==========================================
        var pklPreview = CalculatePklBreakdown(request);
        var pklItems = pklPreview.BreakdownItems;
        int pklCount = pklItems.Count;
        const int pklStartRow = 12;
        const int pklDefaultTemplateRows = 58; // Dòng 12 đến 69

        if (pklCount > pklDefaultTemplateRows)
        {
            int extraRows = pklCount - pklDefaultTemplateRows;
            int insertAt = pklStartRow + pklDefaultTemplateRows - 1; // dòng 69
            pklSheet.Row(insertAt).InsertRowsBelow(extraRows);

            var templateRow = pklSheet.Row(pklStartRow);
            for (int r = insertAt + 1; r <= insertAt + extraRows; r++)
            {
                var newRow = pklSheet.Row(r);
                newRow.Height = templateRow.Height;
                for (int col = 1; col <= 10; col++)
                {
                    newRow.Cell(col).Style = templateRow.Cell(col).Style;
                }
            }
        }
        else if (pklCount > 0 && pklCount < pklDefaultTemplateRows)
        {
            int deleteStart = pklStartRow + pklCount;
            int deleteEnd = pklStartRow + pklDefaultTemplateRows - 1;
            pklSheet.Rows(deleteStart, deleteEnd).Delete();
        }

        for (int i = 0; i < pklCount; i++)
        {
            int row = pklStartRow + i;
            var pklItem = pklItems[i];
            int prevRow = row - 1;
            int pairCtn = pklItem.StandardPairPerCarton > 0 ? pklItem.StandardPairPerCarton : 12;

            pklSheet.Cell(row, 1).FormulaA1 = $"IF(F{row}<=0,\"\",(SUM($F$11:F{prevRow})+1)&\"-\"&SUM($F$11:F{row}))";
            pklSheet.Cell(row, 2).SetValue(pklItem.FullItemCode);
            pklSheet.Cell(row, 3).SetValue(pklItem.Description);
            pklSheet.Cell(row, 4).SetValue(pklItem.Quantity);
            pklSheet.Cell(row, 5).SetValue("đôi");
            pklSheet.Cell(row, 6).FormulaA1 = $"IF(D{row}<=0,0,IF(D{row}<{pairCtn},1,D{row}/{pairCtn}))";
            pklSheet.Cell(row, 7).FormulaA1 = $"I{row}*3.2";
            pklSheet.Cell(row, 8).FormulaA1 = $"ROUNDUP(G{row}+F{row}*0.1,0)";
            pklSheet.Cell(row, 9).FormulaA1 = $"D{row}/{pairCtn}";
            pklSheet.Cell(row, 10).FormulaA1 = $"F{row}";
        }

        int pklLastDataRow = pklCount > 0 ? (pklStartRow + pklCount - 1) : pklStartRow;
        int pklTotalRow = pklLastDataRow + 1;
        pklSheet.Cell(pklTotalRow, 2).SetValue("TỔNG CỘNG:");
        pklSheet.Cell(pklTotalRow, 4).FormulaA1 = $"SUM(D11:D{pklLastDataRow})";
        pklSheet.Cell(pklTotalRow, 6).FormulaA1 = $"SUM(F11:F{pklLastDataRow})";
        pklSheet.Cell(pklTotalRow, 7).FormulaA1 = $"SUM(G11:G{pklLastDataRow})";
        pklSheet.Cell(pklTotalRow, 8).FormulaA1 = $"ROUNDUP(G{pklTotalRow}+F{pklTotalRow}*0.1,0)";

        // ==========================================
        // 4. CẬP NHẬT SHEET2 (Master Data — TOÀN BỘ danh mục)
        // Quy tắc: Sheet2 LUÔN chứa TẤT CẢ ProductMaster trong hệ thống,
        // bất kể file đang là Gò hay Thành hình, để đảm bảo VLOOKUP hoạt động đầy đủ.
        // ==========================================
        if (sheet2 != null)
        {
            // Load toàn bộ danh mục từ DB
            var allProducts = _context != null
                ? await _context.ProductMasters
                    .AsNoTracking()
                    .OrderBy(p => p.StyleCode)
                    .ToListAsync()
                : new List<ProductMaster>();

            int sheet2Row = 1;

            foreach (var pm in allProducts)
            {
                // Dòng 1: Mã Thành hình (Standard)
                var standardCode = $"{pm.StyleCode} {request.PoSuffix}".Trim();
                sheet2.Cell(sheet2Row, 1).SetValue(pm.StyleCode);
                sheet2.Cell(sheet2Row, 2).SetValue(request.PoSuffix ?? string.Empty);
                sheet2.Cell(sheet2Row, 3).SetValue(standardCode);
                sheet2.Cell(sheet2Row, 4).SetValue(pm.UnitPriceCMT);
                sheet2.Cell(sheet2Row, 5).SetValue(pm.UnitPriceDAP);
                sheet2.Cell(sheet2Row, 6).SetValue(pm.Description);
                sheet2.Cell(sheet2Row, 7).SetValue(!string.IsNullOrWhiteSpace(pm.Unit) ? pm.Unit : "PR");
                sheet2.Cell(sheet2Row, 8).SetValue(pm.HsCode);
                sheet2Row++;

                // Dòng 2: Mã Gò không may (nếu có đơn giá Gò riêng)
                // Luôn xuất cả dòng Gò để VLOOKUP bằng mã .G luôn tìm được kết quả
                var goCode = $"{pm.StyleCode}.G {request.PoSuffix}".Trim();
                decimal goCmt = (pm.UnitPriceCMT_Go.HasValue && pm.UnitPriceCMT_Go.Value > 0)
                    ? pm.UnitPriceCMT_Go.Value
                    : pm.UnitPriceCMT;
                decimal goDap = (pm.UnitPriceDAP_Go.HasValue && pm.UnitPriceDAP_Go.Value > 0)
                    ? pm.UnitPriceDAP_Go.Value
                    : pm.UnitPriceDAP;

                sheet2.Cell(sheet2Row, 1).SetValue($"{pm.StyleCode}.G");
                sheet2.Cell(sheet2Row, 2).SetValue(request.PoSuffix ?? string.Empty);
                sheet2.Cell(sheet2Row, 3).SetValue(goCode);
                sheet2.Cell(sheet2Row, 4).SetValue(goCmt);
                sheet2.Cell(sheet2Row, 5).SetValue(goDap);
                sheet2.Cell(sheet2Row, 6).SetValue(pm.Description);
                sheet2.Cell(sheet2Row, 7).SetValue(!string.IsNullOrWhiteSpace(pm.Unit) ? pm.Unit : "PR");
                sheet2.Cell(sheet2Row, 8).SetValue(pm.HsCode);
                sheet2Row++;
            }

            // Xóa các dòng cũ thừa (nếu lần này ít dòng hơn lần trước)
            int oldLastRow = sheet2.LastRowUsed()?.RowNumber() ?? 0;
            if (oldLastRow > sheet2Row - 1 && sheet2Row > 1)
            {
                sheet2.Rows(sheet2Row, oldLastRow).Delete();
            }
        }

        using var ms = new MemoryStream();
        await Task.Run(() => workbook.SaveAs(ms));
        return ms.ToArray();
    }

    /// <summary>
    /// Xuất danh mục sản phẩm hiện có trong hệ thống ra file Excel dựa trên mẫu chuẩn công ty
    /// </summary>
    public async Task<byte[]> ExportProductMastersToExcelAsync()
    {
        var products = await _context.ProductMasters
            .AsNoTracking()
            .OrderBy(p => p.StyleCode)
            .ToListAsync();

        var exportModel = new ShipmentExportModel
        {
            InvoiceNo = "KMHD-NEW2026-0233",
            InvoiceDate = DateTime.UtcNow,
            ContractNo = "KM-HANEW/01-2025",
            PoSuffix = "(KM3.PO5.26)",
            Items = products.Select(p => new ShipmentExportItemModel
            {
                StyleCode = p.StyleCode,
                FullItemCode = $"{p.StyleCode} (KM3.PO5.26)",
                Description = p.Description,
                Quantity = p.PairPerCarton * 100, // Số lượng mẫu 100 thùng chuẩn
                Unit = p.Unit,
                UnitPriceCMT = p.UnitPriceCMT,
                UnitPriceDAP = p.UnitPriceDAP,
                PairPerCarton = p.PairPerCarton
            }).ToList()
        };

        return await ExportShipmentToExcelAsync(exportModel);
    }

    /// <summary>
    /// Import danh mục sản phẩm thông minh:
    /// Tự động nhận diện cả file mẫu chuẩn (ProductMaster_Template) lẫn file hóa đơn thực tế (KM3-26-DH233.xlsx / sheet INV)
    /// </summary>
    public async Task<ImportResultDto> ImportProductMastersFromExcelAsync(Stream fileStream, bool updateExisting = true)
    {
        var result = new ImportResultDto();

        using var workbook = new XLWorkbook(fileStream);

        // 1. Chọn sheet thích hợp: Ưu tiên sheet có tên "INV", "PRODUCT", "MASTER", hoặc sheet hiển thị đầu tiên
        var worksheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("INV", StringComparison.OrdinalIgnoreCase))
                     ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("INV", StringComparison.OrdinalIgnoreCase))
                     ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("PRODUCT", StringComparison.OrdinalIgnoreCase))
                     ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("MASTER", StringComparison.OrdinalIgnoreCase))
                     ?? workbook.Worksheets.FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible)
                     ?? workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            result.Errors.Add(new ImportErrorDetail { RowNumber = 0, Message = "File Excel không chứa bất kỳ bảng tính nào." });
            return result;
        }

        _logger.LogInformation("Import danh mục từ sheet: '{SheetName}'", worksheet.Name);

        // 2. Tìm dòng tiêu đề (Header Row) và ánh xạ cột tự động
        int headerRow = 1;
        int styleCol = 1, descCol = 2, cmtCol = 3, dapCol = 4, hsCol = -1, unitCol = -1, pairCol = -1;
        bool foundHeader = false;

        for (int r = 1; r <= 20; r++)
        {
            for (int c = 1; c <= 15; c++)
            {
                var txt = worksheet.Cell(r, c).GetString().Trim().ToUpper();
                if (txt.Contains("MÃ HÀNG") || txt.Contains("STYLE") || txt.Contains("MÃ HÌNH THỂ"))
                {
                    headerRow = r;
                    foundHeader = true;
                    break;
                }
            }
            if (foundHeader) break;
        }

        // Tìm cột "THÀNH TIỀN" để tránh nhầm lẫn với "ĐƠN GIÁ" khi có ô merge
        int totalAmountCol = -1;
        for (int c = 1; c <= 15; c++)
        {
            var h = worksheet.Cell(headerRow, c).GetString().Trim().ToUpper();
            if (h.Contains("THÀNH TIỀN") || h.Contains("TOTAL"))
            {
                totalAmountCol = c;
            }
        }

        // Ánh xạ các cột dựa trên nội dung dòng tiêu đề (và dòng phụ nếu tiêu đề nhiều tầng)
        for (int c = 1; c <= 15; c++)
        {
            var h1 = worksheet.Cell(headerRow, c).GetString().Trim().ToUpper();
            var h2 = worksheet.Cell(headerRow + 1, c).GetString().Trim().ToUpper();
            var combined = $"{h1} {h2}";

            if (combined.Contains("MÃ HÀNG") || combined.Contains("STYLE") || combined.Contains("MÃ HÌNH THỂ"))
            {
                styleCol = c;
            }
            else if (combined.Contains("MÔ TẢ") || combined.Contains("DESCRIPTION"))
            {
                descCol = c;
            }
            else if (combined.Contains("CMT") && (totalAmountCol == -1 || c < totalAmountCol))
            {
                cmtCol = c;
            }
            else if (combined.Contains("DAP") && (totalAmountCol == -1 || c < totalAmountCol))
            {
                dapCol = c;
            }
            else if (combined.Contains("ĐVT") || combined.Contains("UNIT") || combined.Contains("ĐƠN VỊ"))
            {
                unitCol = c;
            }
            else if (combined.Contains("HS") || combined.Contains("MÃ HS"))
            {
                hsCol = c;
            }
            else if (combined.Contains("PAIR") || combined.Contains("CTN") || combined.Contains("THÙNG") || combined.Contains("SỐ ĐÔI"))
            {
                pairCol = c;
            }
        }

        // Xác định dòng bắt đầu dữ liệu
        int dataStartRow = headerRow + 1;
        if (worksheet.Cell(headerRow + 1, cmtCol).GetString().ToUpper().Contains("CMT") ||
            worksheet.Cell(headerRow + 1, dapCol).GetString().ToUpper().Contains("DAP"))
        {
            dataStartRow = headerRow + 2;
        }

        int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < dataStartRow)
        {
            result.Errors.Add(new ImportErrorDetail { RowNumber = 0, Message = "Bảng tính không có dữ liệu hàng hóa." });
            return result;
        }

        var existingProducts = await _context.ProductMasters.ToDictionaryAsync(p => p.StyleCode.ToUpper(), p => p);

        for (int r = dataStartRow; r <= lastRow; r++)
        {
            var row = worksheet.Row(r);
            int rowNumber = r;

            try
            {
                var rawStyleCode = row.Cell(styleCol).GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(rawStyleCode)) continue;

                // Dừng lại nếu gặp dòng TỔNG CỘNG
                if (rawStyleCode.ToUpper().Contains("TỔNG CỘNG") || rawStyleCode.ToUpper().Contains("TOTAL")) break;
                if (row.Cell(2).GetString().ToUpper().Contains("TỔNG CỘNG")) break;

                // Chuẩn hóa mã hình thể gốc
                var cleanStyleCode = CleanStyleCode(rawStyleCode);
                if (string.IsNullOrWhiteSpace(cleanStyleCode))
                {
                    result.Errors.Add(new ImportErrorDetail
                    {
                        RowNumber = rowNumber,
                        StyleCode = rawStyleCode,
                        Message = "Không thể trích xuất mã hình thể gốc hợp lệ."
                    });
                    continue;
                }

                var description = row.Cell(descCol).GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(description))
                {
                    result.Errors.Add(new ImportErrorDetail
                    {
                        RowNumber = rowNumber,
                        StyleCode = cleanStyleCode,
                        Message = "Mô tả hàng hải quan bị trống."
                    });
                    continue;
                }

                decimal cmt = ParseDecimal(row.Cell(cmtCol));
                decimal dap = ParseDecimal(row.Cell(dapCol));

                string hsCode = hsCol > 0 ? row.Cell(hsCol).GetString()?.Trim() ?? "64041990" : "64041990";
                if (string.IsNullOrWhiteSpace(hsCode)) hsCode = "64041990";

                string unit = unitCol > 0 ? row.Cell(unitCol).GetString()?.Trim() ?? "đôi" : "đôi";
                if (string.IsNullOrWhiteSpace(unit)) unit = "đôi";

                int pairCtn = 12;
                if (pairCol > 0)
                {
                    var pairText = row.Cell(pairCol).GetString()?.Trim();
                    if (int.TryParse(pairText, out var pVal) && pVal > 0)
                    {
                        pairCtn = pVal;
                    }
                }

                result.TotalRows++;

                if (existingProducts.TryGetValue(cleanStyleCode, out var existing))
                {
                    if (updateExisting)
                    {
                        existing.Description = description;
                        if (cmt > 0) existing.UnitPriceCMT = cmt;
                        if (dap > 0) existing.UnitPriceDAP = dap;
                        existing.HsCode = hsCode;
                        existing.Unit = unit;
                        existing.PairPerCarton = pairCtn;
                        existing.UpdatedAt = DateTime.UtcNow;
                        result.UpdatedCount++;
                    }
                    else
                    {
                        result.Errors.Add(new ImportErrorDetail
                        {
                            RowNumber = rowNumber,
                            StyleCode = cleanStyleCode,
                            Message = "Mã hình thể đã tồn tại trong hệ thống (chọn ghi đè để cập nhật)."
                        });
                    }
                }
                else
                {
                    var newProduct = new ProductMaster
                    {
                        StyleCode = cleanStyleCode,
                        Description = description,
                        UnitPriceCMT = cmt,
                        UnitPriceDAP = dap,
                        HsCode = hsCode,
                        Unit = unit,
                        PairPerCarton = pairCtn,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ProductMasters.Add(newProduct);
                    existingProducts[cleanStyleCode] = newProduct;
                    result.CreatedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý dòng {RowNumber}", rowNumber);
                result.Errors.Add(new ImportErrorDetail
                {
                    RowNumber = rowNumber,
                    Message = $"Lỗi xử lý: {ex.Message}"
                });
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    private static string CleanStyleCode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var text = raw.Trim();

        // Loại bỏ phần hậu tố PO trong ngoặc đơn, ví dụ "42072-030 (KM3.PO5.26)" -> "42072-030"
        int parenIdx = text.IndexOf('(');
        if (parenIdx > 0)
        {
            text = text.Substring(0, parenIdx).Trim();
        }

        // Loại bỏ hậu tố quy trình công nghệ .G (Gò không may) hoặc .M (May), ví dụ "45428-2LX.G" -> "45428-2LX"
        if (text.EndsWith(".G", StringComparison.OrdinalIgnoreCase) || 
            text.EndsWith(".M", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(0, text.Length - 2).Trim();
        }

        return text.ToUpperInvariant();
    }

    private static decimal ParseDecimal(IXLCell cell, decimal defaultValue = 0)
    {
        if (cell == null || cell.IsEmpty()) return defaultValue;

        try
        {
            if (cell.DataType == XLDataType.Number)
            {
                return (decimal)cell.GetDouble();
            }

            var str = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(str)) return defaultValue;

            str = str.Replace("$", "").Replace("USD", "").Replace("đ", "").Trim();
            str = str.Replace(',', '.');

            if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                return val;
            }
        }
        catch
        {
            // ignored
        }

        return defaultValue;
    }

    public byte[] GenerateProductMasterTemplate()
    {
        var templatePath = GetTemplatePath();
        if (File.Exists(templatePath))
        {
            return File.ReadAllBytes(templatePath);
        }

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("ProductMaster_Template");

        var headers = new[]
        {
            "Mã hình thể gốc (Style Code) *",
            "Mô tả hàng hải quan (Description) *",
            "Đơn giá CMT (USD)",
            "Đơn giá DAP (USD)",
            "Mã HS Code",
            "Đơn vị tính",
            "Số đôi / Thùng (Pair/CTN)"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(79, 70, 229);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        var sampleRows = new[]
        {
            new object[] { "42072-030", "Giày thể thao nữ buộc dây đế cao su", 2.45, 18.50, "64041990", "đôi", 12 },
            new object[] { "45428-2LX", "Giày chạy bộ nam cổ thấp", 2.80, 21.00, "64041990", "đôi", 12 },
            new object[] { "51200-1BK", "Giày búp bê nữ có quai", 1.95, 15.20, "64041990", "đôi", 12 }
        };

        for (int r = 0; r < sampleRows.Length; r++)
        {
            for (int c = 0; c < sampleRows[r].Length; c++)
            {
                var cell = ws.Cell(r + 2, c + 1);
                var val = sampleRows[r][c];

                if (val is double d)
                {
                    cell.Value = d;
                    cell.Style.NumberFormat.Format = "#,##0.00";
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
                else if (val is int n)
                {
                    cell.Value = n;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                else
                {
                    cell.Value = val.ToString();
                    if (c == 0 || c == 4 || c == 5)
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
            }
        }

        ws.Columns().AdjustToContents();
        ws.Row(1).Height = 26;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private void UpdateWordsCellInInvSheet(IXLWorksheet invSheet, int invTotalRow, string textInWords)
    {
        // 1. Tìm trong phạm vi 1-6 dòng ngay dưới dòng TỔNG CỘNG (thường nằm ở cột B, cách tổng cộng 1-2 dòng)
        IXLCell? wordsCell = null;
        int maxRow = invTotalRow + 6;
        for (int r = invTotalRow + 1; r <= maxRow; r++)
        {
            for (int c = 1; c <= 6; c++)
            {
                var cell = invSheet.Cell(r, c);
                if (!cell.HasFormula)
                {
                    var val = cell.GetString();
                    if (val.Contains("Bằng chữ", StringComparison.OrdinalIgnoreCase))
                    {
                        wordsCell = cell;
                        break;
                    }
                }
            }
            if (wordsCell != null) break;
        }

        // 2. Nếu chưa thấy ở các dòng ngay dưới tổng cộng, quét các ô không có công thức trong sheet INV
        if (wordsCell == null)
        {
            for (int r = 1; r <= 80; r++)
            {
                for (int c = 1; c <= 6; c++)
                {
                    var cell = invSheet.Cell(r, c);
                    if (!cell.HasFormula)
                    {
                        var val = cell.GetString();
                        if (val.Contains("Bằng chữ", StringComparison.OrdinalIgnoreCase))
                        {
                            wordsCell = cell;
                            break;
                        }
                    }
                }
                if (wordsCell != null) break;
            }
        }

        if (wordsCell != null)
        {
            wordsCell.SetValue(textInWords);
        }
        else
        {
            // Nếu không tìm thấy, gán tại ô B(invTotalRow + 2)
            invSheet.Cell(invTotalRow + 2, 2).SetValue(textInWords);
        }
    }

    /// <summary>
    /// Xuất 2 file Excel độc lập (1 cho Gò không may, 1 cho Thành hình)
    /// và đóng gói thành 1 file ZIP để tải về.
    /// </summary>
    public async Task<byte[]> ExportSplitToZipAsync(
        CreateShipmentRequestDto goRequest,
        string goFileName,
        CreateShipmentRequestDto standardRequest,
        string standardFileName)
    {
        _logger?.LogInformation("Bắt đầu xuất 2 file tách: {GoFile} & {StdFile}", goFileName, standardFileName);

        // Xuất song song 2 file để tăng tốc
        var goTask = ExportShipmentMultiSheetExcelAsync(goRequest);
        var standardTask = ExportShipmentMultiSheetExcelAsync(standardRequest);

        await Task.WhenAll(goTask, standardTask);

        var goBytes = await goTask;
        var standardBytes = await standardTask;

        // Đóng gói thành ZIP
        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var goEntry = archive.CreateEntry(goFileName, System.IO.Compression.CompressionLevel.Optimal);
            using (var entryStream = goEntry.Open())
            {
                await entryStream.WriteAsync(goBytes);
            }

            var stdEntry = archive.CreateEntry(standardFileName, System.IO.Compression.CompressionLevel.Optimal);
            using (var entryStream = stdEntry.Open())
            {
                await entryStream.WriteAsync(standardBytes);
            }
        }

        zipStream.Position = 0;
        return zipStream.ToArray();
    }
}
