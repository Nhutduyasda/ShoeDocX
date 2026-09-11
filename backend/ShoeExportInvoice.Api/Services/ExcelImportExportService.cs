using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Common;
using System.Globalization;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Chuẩn hóa mô tả sản phẩm cho hóa đơn INV và PKL:
    /// Bỏ phần đơn giá gia công kèm đơn vị tính "(Đơn giá gia công:... USD/đôi)" khỏi mô tả.
    /// Ví dụ: "Giày có mũ giày bằng vật liệu dệt và đế ngoài bằng plastic, mũi giày không được gắn bảo vệ (Đơn giá gia công:1.16 USD/đôi). Hàng mới 100%."
    /// -> "Giày có mũ giày bằng vật liệu dệt và đế ngoài bằng plastic, mũi giày không được gắn bảo vệ . Hàng mới 100%."
    /// </summary>
    public static string CleanDescriptionForInvAndPkl(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;

        var cleaned = Regex.Replace(
            description,
            @"\s*\([^)]*gia\s*c[oôOÔ]ng[^)]*\)",
            " ",
            RegexOptions.IgnoreCase);

        return cleaned;
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
            invSheet.Cell(row, 4).SetValue(CleanDescriptionForInvAndPkl(item.Description));

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
            pklSheet.Cell(row, 3).SetValue(CleanDescriptionForInvAndPkl(item.Description));

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

            string desc = CleanDescriptionForInvAndPkl(item.Description);
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
            var distinctCodes = request.Items
                .Select(x => (x.StyleCode ?? string.Empty).Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var lookupCodes = distinctCodes
                .Select(c => c.EndsWith(".G", StringComparison.OrdinalIgnoreCase) ? c[..^2].Trim() : c)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var list = await _context.ProductMasters
                .Where(p => lookupCodes.Contains(p.StyleCode) || distinctCodes.Contains(p.StyleCode))
                .ToListAsync();

            dbProducts = new Dictionary<string, ProductMaster>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in list)
            {
                dbProducts[p.StyleCode] = p;
            }

            var missing = distinctCodes.Where(code =>
            {
                var clean = code.EndsWith(".G", StringComparison.OrdinalIgnoreCase) ? code[..^2].Trim() : code;
                return !dbProducts.ContainsKey(code) && !dbProducts.ContainsKey(clean);
            }).ToList();

            if (missing.Count > 0)
            {
                throw new InvalidOperationException($"Không thể xuất file! Các mã sau chưa được đăng ký trong Master Data: [{string.Join(", ", missing)}]");
            }
        }

        foreach (var item in request.Items)
        {
            ProductMaster? pm = null;
            if (dbProducts != null)
            {
                var itemCode = (item.StyleCode ?? string.Empty).Trim();
                if (!dbProducts.TryGetValue(itemCode, out pm) && itemCode.EndsWith(".G", StringComparison.OrdinalIgnoreCase))
                {
                    dbProducts.TryGetValue(itemCode[..^2].Trim(), out pm);
                }
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
            invSheet.Cell(row, 4).SetValue(CleanDescriptionForInvAndPkl(item.Description));
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
            pklSheet.Cell(row, 3).SetValue(CleanDescriptionForInvAndPkl(pklItem.Description));
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
                Description = CleanDescriptionForInvAndPkl(p.Description),
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
    /// <summary>
    /// Xem trước danh mục sản phẩm từ file Excel và tự động phân tích ngữ nghĩa các cột
    /// </summary>
    public async Task<ImportPreviewResponseDto> PreviewProductMastersFromExcelAsync(Stream fileStream, int? folderId = null)
    {
        MasterDataFolder? targetFolder = null;
        if (folderId.HasValue && _context != null)
        {
            targetFolder = await _context.MasterDataFolders.FindAsync(folderId.Value);
        }
        int defaultPpc = targetFolder?.DefaultPairsPerCarton ?? 12;
        string defaultUnit = !string.IsNullOrWhiteSpace(targetFolder?.DefaultUnit) ? targetFolder.DefaultUnit : "PRS";

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = SelectDataWorksheet(workbook);
        if (worksheet == null)
        {
            throw new InvalidOperationException("File Excel không chứa bất kỳ bảng tính nào có dữ liệu.");
        }

        var (startRow, mapping, availableColumns, totalRows) = DetectStartRowAndColumns(worksheet);

        var previewRows = new List<PreviewRowDto>();
        int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        int previewLimit = Math.Min(startRow + 4, lastRow);

        for (int r = startRow; r <= previewLimit; r++)
        {
            var row = worksheet.Row(r);
            var rawCode = row.Cell(mapping.StyleCodeCol).GetString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(rawCode)) continue;
            if (rawCode.Contains("TỔNG CỘNG", StringComparison.OrdinalIgnoreCase) || rawCode.Contains("TOTAL", StringComparison.OrdinalIgnoreCase)) break;

            var cleanRaw = rawCode;
            int pIdx = cleanRaw.IndexOf('(');
            if (pIdx > 0) cleanRaw = cleanRaw.Substring(0, pIdx).Trim();

            bool isGo = cleanRaw.EndsWith(".G", StringComparison.OrdinalIgnoreCase);
            string baseCode = isGo ? cleanRaw.Substring(0, cleanRaw.Length - 2).Trim() : cleanRaw.Trim();

            TryExtractDecimal(row.Cell(mapping.CmtPriceCol), out var cmt, out _);
            TryExtractDecimal(row.Cell(mapping.DapPriceCol), out var dap, out _);

            string desc = mapping.DescriptionCol > 0 ? row.Cell(mapping.DescriptionCol).GetString()?.Trim() ?? "" : "";
            string hs = mapping.HsCodeCol.HasValue && mapping.HsCodeCol.Value > 0 ? row.Cell(mapping.HsCodeCol.Value).GetString()?.Trim() ?? "64041990" : "64041990";
            string unit = mapping.UnitCol.HasValue && mapping.UnitCol.Value > 0 ? row.Cell(mapping.UnitCol.Value).GetString()?.Trim() ?? defaultUnit : defaultUnit;
            int ppc = defaultPpc;
            if (mapping.PairsPerCartonCol.HasValue && mapping.PairsPerCartonCol.Value > 0 &&
                int.TryParse(row.Cell(mapping.PairsPerCartonCol.Value).GetString()?.Trim(), out var pVal) && pVal > 0)
            {
                ppc = pVal;
            }

            previewRows.Add(new PreviewRowDto
            {
                RowNumber = r,
                StyleCode = baseCode,
                UnitPriceCMT = cmt,
                UnitPriceDAP = dap,
                Description = desc,
                HsCode = string.IsNullOrWhiteSpace(hs) ? "64041990" : hs,
                Unit = string.IsNullOrWhiteSpace(unit) ? defaultUnit : unit,
                PairsPerCarton = ppc,
                IsGo = isGo
            });
        }

        return new ImportPreviewResponseDto
        {
            TotalRows = totalRows,
            StartRowIndex = startRow,
            DetectedMapping = mapping,
            AvailableColumns = availableColumns,
            PreviewRows = previewRows
        };
    }

    /// <summary>
    /// Import danh mục sản phẩm thông minh:
    /// Bộ phân tích Excel Thích ứng Thông minh (Adaptive Excel Parser) & Tự động Nhận diện Cột theo Ngữ nghĩa
    /// </summary>
    public async Task<ImportResultDto> ImportProductMastersFromExcelAsync(
        Stream fileStream,
        bool updateExisting = true,
        int? folderId = null,
        ColumnMappingOverrideDto? mappingOverride = null)
    {
        var result = new ImportResultDto();

        MasterDataFolder? targetFolder = null;
        if (folderId.HasValue && _context != null)
        {
            targetFolder = await _context.MasterDataFolders.FindAsync(folderId.Value);
        }
        int defaultPpc = targetFolder?.DefaultPairsPerCarton ?? 12;
        string defaultUnit = !string.IsNullOrWhiteSpace(targetFolder?.DefaultUnit) ? targetFolder.DefaultUnit : "PRS";

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = SelectDataWorksheet(workbook);

        if (worksheet == null)
        {
            result.Success = false;
            result.Message = "File Excel không chứa bất kỳ bảng tính nào có dữ liệu.";
            result.Errors.Add(new ImportErrorDetail { RowNumber = 0, Message = result.Message });
            return result;
        }

        _logger.LogInformation("Import danh mục Master Data từ sheet: '{SheetName}'", worksheet.Name);

        var (startRow, mapping, _, _) = DetectStartRowAndColumns(worksheet, mappingOverride);

        int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < startRow)
        {
            result.Success = false;
            result.Message = "Bảng tính không có dữ liệu hàng hóa.";
            result.Errors.Add(new ImportErrorDetail { RowNumber = 0, Message = result.Message });
            return result;
        }

        var existingProducts = await _context.ProductMasters.ToDictionaryAsync(p => p.StyleCode.ToUpperInvariant(), p => p);

        for (int r = startRow; r <= lastRow; r++)
        {
            var row = worksheet.Row(r);

            // Bỏ qua nếu toàn bộ dòng trống
            bool isRowEmpty = true;
            for (int c = 1; c <= Math.Min(12, worksheet.LastColumnUsed()?.ColumnNumber() ?? 8); c++)
            {
                if (!row.Cell(c).IsEmpty() && !string.IsNullOrWhiteSpace(row.Cell(c).GetString()))
                {
                    isRowEmpty = false;
                    break;
                }
            }
            if (isRowEmpty) continue;

            var rawCodeStr = row.Cell(mapping.StyleCodeCol).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(rawCodeStr))
            {
                result.Errors.Add(new ImportErrorDetail
                {
                    RowNumber = r,
                    StyleCode = "N/A",
                    Message = $"Dòng thiếu mã sản phẩm (Cột {GetColumnLetter(mapping.StyleCodeCol)} bị trống)."
                });
                continue;
            }

            // Dừng lại nếu gặp dòng tổng cộng
            if (rawCodeStr.Contains("TỔNG CỘNG", StringComparison.OrdinalIgnoreCase) ||
                rawCodeStr.Contains("TOTAL", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            result.TotalRowsRead++;

            try
            {
                // Parse đơn giá CMT
                if (!TryExtractDecimal(row.Cell(mapping.CmtPriceCol), out var cmtPrice, out var cmtError))
                {
                    result.Errors.Add(new ImportErrorDetail
                    {
                        RowNumber = r,
                        StyleCode = rawCodeStr,
                        Message = $"Đơn giá CMT không hợp lệ: {cmtError}"
                    });
                    continue;
                }

                // Parse đơn giá DAP
                if (!TryExtractDecimal(row.Cell(mapping.DapPriceCol), out var dapPrice, out var dapError))
                {
                    result.Errors.Add(new ImportErrorDetail
                    {
                        RowNumber = r,
                        StyleCode = rawCodeStr,
                        Message = $"Đơn giá DAP/FOB không hợp lệ: {dapError}"
                    });
                    continue;
                }

                // Mô tả hải quan
                var customsDescription = mapping.DescriptionCol > 0 ? row.Cell(mapping.DescriptionCol).GetString()?.Trim() ?? string.Empty : string.Empty;

                // Đơn vị tính: mặc định từ folder hoặc "PRS"
                var unit = mapping.UnitCol.HasValue && mapping.UnitCol.Value > 0 ? row.Cell(mapping.UnitCol.Value).GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(unit)) unit = defaultUnit;

                // Mã HS: mặc định "64041990"
                var hsCode = mapping.HsCodeCol.HasValue && mapping.HsCodeCol.Value > 0 ? row.Cell(mapping.HsCodeCol.Value).GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(hsCode)) hsCode = "64041990";

                // Quy cách đóng gói (PairsPerCarton): mặc định từ folder (12 hoặc 24)
                int pairCtn = defaultPpc;
                if (mapping.PairsPerCartonCol.HasValue && mapping.PairsPerCartonCol.Value > 0)
                {
                    var pairText = row.Cell(mapping.PairsPerCartonCol.Value).GetString()?.Trim();
                    if (int.TryParse(pairText, out var pVal) && pVal > 0)
                    {
                        pairCtn = pVal;
                    }
                }

                // Tách mã sản phẩm và nhận diện công đoạn Gò (.G)
                var cleanRaw = rawCodeStr;
                int parenIdx = cleanRaw.IndexOf('(');
                if (parenIdx > 0)
                {
                    cleanRaw = cleanRaw.Substring(0, parenIdx).Trim();
                }

                bool isGo = cleanRaw.EndsWith(".G", StringComparison.OrdinalIgnoreCase);
                string baseCode = isGo
                    ? cleanRaw.Substring(0, cleanRaw.Length - 2).Trim()
                    : cleanRaw.Trim();

                if (string.IsNullOrWhiteSpace(baseCode))
                {
                    result.Errors.Add(new ImportErrorDetail
                    {
                        RowNumber = r,
                        StyleCode = rawCodeStr,
                        Message = "Không thể trích xuất mã hình thể hợp lệ."
                    });
                    continue;
                }

                var lookupKey = baseCode.ToUpperInvariant();

                // Xác định mô tả: nếu trống thì fallback theo tên mặc định
                if (string.IsNullOrWhiteSpace(customsDescription))
                {
                    customsDescription = $"Giày xuất khẩu {baseCode}";
                }

                if (isGo)
                {
                    // Hàng Gò không may (có hậu tố .G)
                    if (existingProducts.TryGetValue(lookupKey, out var existing))
                    {
                        existing.UnitPriceCMT_Go = cmtPrice;
                        existing.UnitPriceDAP_Go = dapPrice;
                        existing.HasGoOption = true;
                        if (folderId.HasValue) existing.FolderId = folderId.Value;
                        if (!string.IsNullOrWhiteSpace(customsDescription) && (string.IsNullOrWhiteSpace(existing.Description) || updateExisting))
                        {
                            existing.Description = customsDescription;
                        }
                        if (!string.IsNullOrWhiteSpace(hsCode)) existing.HsCode = hsCode;
                        if (!string.IsNullOrWhiteSpace(unit)) existing.Unit = unit;
                        existing.UpdatedAt = DateTime.UtcNow;
                        result.UpdatedCount++;
                    }
                    else
                    {
                        var newProduct = new ProductMaster
                        {
                            StyleCode = baseCode,
                            Description = customsDescription,
                            UnitPriceCMT = 0,
                            UnitPriceDAP = 0,
                            UnitPriceCMT_Go = cmtPrice,
                            UnitPriceDAP_Go = dapPrice,
                            HasGoOption = true,
                            HsCode = hsCode,
                            Unit = unit,
                            PairPerCarton = pairCtn,
                            FolderId = folderId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.ProductMasters.Add(newProduct);
                        existingProducts[lookupKey] = newProduct;
                        result.CreatedCount++;
                    }
                }
                else
                {
                    // Hàng tiêu chuẩn Thành hình (không có đuôi .G)
                    if (existingProducts.TryGetValue(lookupKey, out var existing))
                    {
                        if (updateExisting)
                        {
                            existing.UnitPriceCMT = cmtPrice;
                            existing.UnitPriceDAP = dapPrice;
                            if (folderId.HasValue) existing.FolderId = folderId.Value;
                            if (!string.IsNullOrWhiteSpace(customsDescription)) existing.Description = customsDescription;
                            if (!string.IsNullOrWhiteSpace(hsCode)) existing.HsCode = hsCode;
                            if (!string.IsNullOrWhiteSpace(unit)) existing.Unit = unit;
                            existing.UpdatedAt = DateTime.UtcNow;
                            result.UpdatedCount++;
                        }
                        else
                        {
                            result.Errors.Add(new ImportErrorDetail
                            {
                                RowNumber = r,
                                StyleCode = baseCode,
                                Message = "Mã hình thể đã tồn tại trong hệ thống (chọn ghi đè để cập nhật)."
                            });
                        }
                    }
                    else
                    {
                        var newProduct = new ProductMaster
                        {
                            StyleCode = baseCode,
                            Description = customsDescription,
                            UnitPriceCMT = cmtPrice,
                            UnitPriceDAP = dapPrice,
                            HsCode = hsCode,
                            Unit = unit,
                            PairPerCarton = pairCtn,
                            FolderId = folderId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.ProductMasters.Add(newProduct);
                        existingProducts[lookupKey] = newProduct;
                        result.CreatedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý dòng {RowNumber} ({StyleCode})", r, rawCodeStr);
                result.Errors.Add(new ImportErrorDetail
                {
                    RowNumber = r,
                    StyleCode = rawCodeStr,
                    Message = $"Lỗi xử lý: {ex.Message}"
                });
            }
        }

        await _context.SaveChangesAsync();

        result.ImportedCount = result.CreatedCount + result.UpdatedCount;
        result.Success = result.ImportedCount > 0;
        result.Message = result.Errors.Count == 0
            ? $"Đã import thành công {result.ImportedCount} mã sản phẩm vào Master Data!"
            : $"Đã xử lý {result.ImportedCount}/{result.TotalRowsRead} mã sản phẩm. Có {result.Errors.Count} dòng bị lỗi.";

        _logger.LogInformation("Hoàn tất Import: {TotalRead} dòng đọc, {Imported} import thành công, {Errors} lỗi",
            result.TotalRowsRead, result.ImportedCount, result.Errors.Count);

        return result;
    }

    private static bool IsPossibleStyleCode(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return false;
        val = val.Trim();
        int pIdx = val.IndexOf('(');
        if (pIdx > 0) val = val.Substring(0, pIdx).Trim();

        // Không nhận các từ khóa tiêu đề hoặc từ thuần túy làm mã
        if (val.Equals("STT", StringComparison.OrdinalIgnoreCase) ||
            val.Equals("MÃ", StringComparison.OrdinalIgnoreCase) ||
            val.Equals("STYLE", StringComparison.OrdinalIgnoreCase) ||
            val.Equals("CODE", StringComparison.OrdinalIgnoreCase) ||
            val.Equals("CMT", StringComparison.OrdinalIgnoreCase) ||
            val.Equals("FOB", StringComparison.OrdinalIgnoreCase) ||
            val.Equals("DAP", StringComparison.OrdinalIgnoreCase) ||
            val.Equals("TOTAL", StringComparison.OrdinalIgnoreCase) ||
            val.Equals("TỔNG CỘNG", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Mã sản phẩm thực tế: có dấu gạch ngang (42072-030, YL3564-100, P-TEST-24, 45428-2LX.G)
        return Regex.IsMatch(val, @"^[A-Z0-9]+(-[A-Z0-9]+)+(\.G)?$", RegexOptions.IgnoreCase);
    }

    private static string GetColumnLetter(int colNumber)
    {
        string letter = "";
        while (colNumber > 0)
        {
            int rem = (colNumber - 1) % 26;
            letter = (char)('A' + rem) + letter;
            colNumber = (colNumber - 1) / 26;
        }
        return letter;
    }

    private static IXLWorksheet? SelectDataWorksheet(XLWorkbook workbook)
    {
        var candidateSheets = workbook.Worksheets.Where(w => w.Visibility == XLWorksheetVisibility.Visible).ToList();
        if (candidateSheets.Count == 0) candidateSheets = workbook.Worksheets.ToList();
        if (candidateSheets.Count == 0) return null;

        foreach (var sheet in candidateSheets)
        {
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow > 0)
            {
                int maxCols = Math.Min(15, sheet.LastColumnUsed()?.ColumnNumber() ?? 0);
                for (int r = 1; r <= Math.Min(10, lastRow); r++)
                {
                    for (int c = 1; c <= maxCols; c++)
                    {
                        if (IsPossibleStyleCode(sheet.Cell(r, c).GetString()))
                        {
                            return sheet;
                        }
                    }
                }
            }
        }

        return workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Sheet2", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("MASTER", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("PRODUCT", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("DANH MUC", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Sheet1", StringComparison.OrdinalIgnoreCase))
            ?? candidateSheets.FirstOrDefault();
    }

    private static (int startRow, DetectedMappingDto mapping, List<ExcelColumnInfoDto> availableColumns, int totalRows)
        DetectStartRowAndColumns(IXLWorksheet worksheet, ColumnMappingOverrideDto? mappingOverride = null)
    {
        int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        int lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        // BƯỚC 1: Xác định dòng bắt đầu dữ liệu (StartRow)
        int startRow = 1;
        bool foundStyleCode = false;

        for (int r = 1; r <= Math.Min(8, lastRow); r++)
        {
            for (int c = 1; c <= lastCol; c++)
            {
                var text = worksheet.Cell(r, c).GetString()?.Trim();
                if (IsPossibleStyleCode(text))
                {
                    startRow = r;
                    foundStyleCode = true;
                    break;
                }
            }
            if (foundStyleCode) break;
        }

        if (!foundStyleCode)
        {
            bool col4IsNum = decimal.TryParse(worksheet.Cell(1, 4).GetString().Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out _);
            startRow = col4IsNum ? 1 : 2;
        }

        int headerRow = startRow > 1 ? startRow - 1 : 0;

        // Xây dựng danh sách AvailableColumns
        var availableColumns = new List<ExcelColumnInfoDto>();
        for (int c = 1; c <= lastCol; c++)
        {
            var headerText = headerRow > 0 ? worksheet.Cell(headerRow, c).GetString()?.Trim() : null;
            var samples = new List<string>();
            for (int r = startRow; r <= Math.Min(startRow + 2, lastRow); r++)
            {
                var s = worksheet.Cell(r, c).GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    samples.Add(s);
                }
            }

            availableColumns.Add(new ExcelColumnInfoDto
            {
                Index = c,
                ColumnLetter = GetColumnLetter(c),
                HeaderName = string.IsNullOrWhiteSpace(headerText) ? null : headerText,
                SampleValues = samples
            });
        }

        // BƯỚC 2: Tự động nhận diện vai trò của từng Cột (Semantic Column Detection)
        var mapping = new DetectedMappingDto();

        // 1. StyleCodeCol
        if (mappingOverride?.StyleCodeCol > 0)
        {
            mapping.StyleCodeCol = mappingOverride.StyleCodeCol.Value;
        }
        else
        {
            int detectedStyleCol = 1;
            for (int c = 1; c <= lastCol; c++)
            {
                int matchCount = 0;
                for (int r = startRow; r <= Math.Min(startRow + 4, lastRow); r++)
                {
                    if (IsPossibleStyleCode(worksheet.Cell(r, c).GetString()))
                    {
                        matchCount++;
                    }
                }
                if (matchCount >= 1)
                {
                    detectedStyleCol = c;
                    break;
                }
            }
            mapping.StyleCodeCol = detectedStyleCol;
        }

        // 2. PoSuffixCol
        if (mappingOverride?.PoSuffixCol > 0)
        {
            mapping.PoSuffixCol = mappingOverride.PoSuffixCol.Value;
        }
        else
        {
            for (int c = 1; c <= lastCol; c++)
            {
                if (c == mapping.StyleCodeCol) continue;
                var sample = worksheet.Cell(startRow, c).GetString()?.Trim() ?? "";
                if (sample.Contains('(') && (sample.Contains("KM", StringComparison.OrdinalIgnoreCase) || sample.Contains("PO", StringComparison.OrdinalIgnoreCase)))
                {
                    mapping.PoSuffixCol = c;
                    break;
                }
            }
        }

        // 3. CmtPriceCol
        if (mappingOverride?.CmtPriceCol > 0)
        {
            mapping.CmtPriceCol = mappingOverride.CmtPriceCol.Value;
        }
        else
        {
            var cmtHeaderCol = availableColumns.FirstOrDefault(col =>
                col.HeaderName != null && col.HeaderName.Contains("CMT", StringComparison.OrdinalIgnoreCase));

            if (cmtHeaderCol != null)
            {
                mapping.CmtPriceCol = cmtHeaderCol.Index;
            }
            else
            {
                int detectedCmt = 4;
                for (int c = 1; c <= lastCol; c++)
                {
                    if (c == mapping.StyleCodeCol || c == mapping.PoSuffixCol) continue;
                    if (TryExtractDecimal(worksheet.Cell(startRow, c), out var pVal, out _) && pVal > 0.1m && pVal < 30m)
                    {
                        detectedCmt = c;
                        break;
                    }
                }
                mapping.CmtPriceCol = detectedCmt;
            }
        }

        // 4. DapPriceCol (FOB / DAP)
        if (mappingOverride?.DapPriceCol > 0)
        {
            mapping.DapPriceCol = mappingOverride.DapPriceCol.Value;
        }
        else
        {
            var dapHeaderCol = availableColumns.FirstOrDefault(col =>
                col.HeaderName != null && (col.HeaderName.Contains("FOB", StringComparison.OrdinalIgnoreCase) || col.HeaderName.Contains("DAP", StringComparison.OrdinalIgnoreCase)));

            if (dapHeaderCol != null)
            {
                mapping.DapPriceCol = dapHeaderCol.Index;
            }
            else
            {
                int detectedDap = mapping.CmtPriceCol + 1;
                for (int c = mapping.CmtPriceCol + 1; c <= lastCol; c++)
                {
                    if (TryExtractDecimal(worksheet.Cell(startRow, c), out var pVal, out _) && pVal > 0)
                    {
                        detectedDap = c;
                        break;
                    }
                }
                mapping.DapPriceCol = detectedDap;
            }
        }

        // 5. DescriptionCol
        if (mappingOverride?.DescriptionCol > 0)
        {
            mapping.DescriptionCol = mappingOverride.DescriptionCol.Value;
        }
        else
        {
            var descKeywords = new[] { "giày", "giay", "mũ giày", "mu giay", "đế", "de", "dệt", "det", "da", "usd/đôi", "usd/doi", "hàng mới", "hang moi", "shoe", "athletic", "rubber", "textile" };

            int bestDescCol = 0;
            int maxKeywordMatches = 0;
            double maxAvgLength = 0;

            for (int c = 1; c <= lastCol; c++)
            {
                if (c == mapping.StyleCodeCol || c == mapping.PoSuffixCol || c == mapping.CmtPriceCol || c == mapping.DapPriceCol)
                    continue;

                int keywordMatches = 0;
                int totalLength = 0;
                int sampleCount = 0;

                for (int r = startRow; r <= Math.Min(startRow + 4, lastRow); r++)
                {
                    var text = worksheet.Cell(r, c).GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        totalLength += text.Length;
                        sampleCount++;
                        foreach (var kw in descKeywords)
                        {
                            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                            {
                                keywordMatches++;
                            }
                        }
                    }
                }

                double avgLen = sampleCount > 0 ? (double)totalLength / sampleCount : 0;

                if (keywordMatches > maxKeywordMatches || (keywordMatches == maxKeywordMatches && avgLen > maxAvgLength))
                {
                    maxKeywordMatches = keywordMatches;
                    maxAvgLength = avgLen;
                    bestDescCol = c;
                }
            }

            mapping.DescriptionCol = bestDescCol > 0 ? bestDescCol : 6;
        }

        // 6. HsCodeCol
        if (mappingOverride?.HsCodeCol > 0)
        {
            mapping.HsCodeCol = mappingOverride.HsCodeCol.Value;
        }
        else
        {
            for (int c = 1; c <= lastCol; c++)
            {
                if (c == mapping.StyleCodeCol || c == mapping.PoSuffixCol || c == mapping.CmtPriceCol || c == mapping.DapPriceCol || c == mapping.DescriptionCol)
                    continue;

                var text = worksheet.Cell(startRow, c).GetString()?.Trim() ?? "";
                if (Regex.IsMatch(text, @"^64\d{6,8}$") || text.StartsWith("6404") || text.StartsWith("6403") || text.StartsWith("6402"))
                {
                    mapping.HsCodeCol = c;
                    break;
                }
            }
        }

        // 7. UnitCol
        if (mappingOverride?.UnitCol > 0)
        {
            mapping.UnitCol = mappingOverride.UnitCol.Value;
        }
        else
        {
            var unitKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PR", "PRS", "ĐÔI", "DOI", "CẶP", "CAP", "PAIR", "PAIRS" };
            for (int c = 1; c <= lastCol; c++)
            {
                if (c == mapping.StyleCodeCol || c == mapping.PoSuffixCol || c == mapping.CmtPriceCol || c == mapping.DapPriceCol || c == mapping.DescriptionCol)
                    continue;

                var text = worksheet.Cell(startRow, c).GetString()?.Trim() ?? "";
                if (unitKeywords.Contains(text))
                {
                    mapping.UnitCol = c;
                    break;
                }
            }
        }

        // 8. PairsPerCartonCol
        if (mappingOverride?.PairsPerCartonCol > 0)
        {
            mapping.PairsPerCartonCol = mappingOverride.PairsPerCartonCol.Value;
        }
        else
        {
            for (int c = 1; c <= lastCol; c++)
            {
                var h = availableColumns.FirstOrDefault(x => x.Index == c)?.HeaderName ?? "";
                if (h.Contains("CTN", StringComparison.OrdinalIgnoreCase) || h.Contains("THÙNG", StringComparison.OrdinalIgnoreCase) || h.Contains("CARTON", StringComparison.OrdinalIgnoreCase))
                {
                    mapping.PairsPerCartonCol = c;
                    break;
                }
            }
        }

        int totalRows = Math.Max(0, lastRow - startRow + 1);

        return (startRow, mapping, availableColumns, totalRows);
    }

    private static bool TryExtractDecimal(IXLCell? cell, out decimal value, out string? errorMessage)
    {
        value = 0m;
        errorMessage = null;
        if (cell == null || cell.IsEmpty()) return true;

        try
        {
            // Nếu ô là lỗi công thức trong Excel (như #N/A, #VALUE!, #REF!, v.v.) thì mặc định giá = 0
            if (cell.DataType == XLDataType.Error)
            {
                value = 0m;
                return true;
            }

            if (cell.DataType == XLDataType.Number)
            {
                value = (decimal)cell.GetDouble();
                return true;
            }

            var str = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(str)) return true;

            // Xử lý các lỗi công thức dạng chuỗi hoặc ký hiệu rỗng/chưa có giá: #N/A, N/A, NA, -, --, v.v.
            if (str.Equals("#N/A", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("NA", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("#VALUE!", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("#REF!", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("#NAME?", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("#NULL!", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("#NUM!", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("#DIV/0!", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("-") || str.Equals("--") || str.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                value = 0m;
                return true;
            }

            str = str.Replace("$", "")
                     .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("đ", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("VND", "", StringComparison.OrdinalIgnoreCase)
                     .Trim();

            if (str.Contains(',') && str.Contains('.'))
            {
                str = str.Replace(",", "");
            }
            else if (str.Contains(','))
            {
                str = str.Replace(',', '.');
            }

            if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                value = parsed;
                return true;
            }

            errorMessage = $"Giá trị '{cell.GetString()}' không phải là định dạng số hợp lệ.";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
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
        if (TryExtractDecimal(cell, out var val, out _))
        {
            return val;
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
