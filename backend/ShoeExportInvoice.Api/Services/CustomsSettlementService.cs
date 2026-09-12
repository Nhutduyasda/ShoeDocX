using System.IO;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public class CustomsSettlementService : ICustomsSettlementService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CustomsSettlementService> _logger;
    private readonly XnkOptions _options;

    public CustomsSettlementService(
        AppDbContext context,
        ILogger<CustomsSettlementService> logger,
        Microsoft.Extensions.Options.IOptions<XnkOptions>? options = null)
    {
        _context = context;
        _logger = logger;
        _options = options?.Value ?? new XnkOptions();
    }

    /// <summary>
    /// Tự động tổng hợp dữ liệu quyết toán từ các đơn hàng xuất khẩu loại hình gia công (E52) ĐÃ THÔNG QUAN trong kỳ
    /// </summary>
    public async Task<SettlementReportDto> CalculateSettlementAsync(CalculateSettlementRequestDto request)
    {
        if (request.FromDate.Date > request.ToDate.Date) throw new InvalidOperationException("Ngày bắt đầu phải trước ngày kết thúc.");
        // 1. Chuẩn hóa khoảng thời gian
        var fromDate = request.FromDate.Date;
        var toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);

        // 2. Tìm tất cả đơn hàng đã thông quan hoặc có tờ khai trong khoảng thời gian
        // Ưu tiên ngày thông quan ClearanceDate, nếu chưa có thì dùng InvoiceDate
        var query = _context.ShipmentOrders
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.Status == ShipmentStatus.Cleared)
            .Where(s => s.CustomsDeclarationType == "E52")
            .Where(s => (s.ClearanceDate ?? s.InvoiceDate) >= fromDate && (s.ClearanceDate ?? s.InvoiceDate) <= toDate);

        if (request.ContractFolderId.HasValue) query = query.Where(s => s.ContractFolderId == request.ContractFolderId);
        if (!string.IsNullOrWhiteSpace(request.ContractNo))
        {
            string contract = request.ContractNo.Trim().ToUpperInvariant();
            query = query.Where(s => s.ContractNo != null && s.ContractNo.ToUpper() == contract);
        }

        var clearedOrders = await query.ToListAsync();

        // 3. Tải toàn bộ ProductMasters để lấy thông tin Tên sản phẩm, ĐVT chuẩn
        var masterRows = await _context.ProductMasters
            .Where(p => !request.ContractFolderId.HasValue || p.FolderId == request.ContractFolderId)
            .AsNoTracking()
            .ToListAsync();
        var productMasters = masterRows.GroupBy(p => p.StyleCode.ToUpperInvariant()).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());

        // 4. Tìm kỳ quyết toán trước đó gần nhất để lấy số dư cuối kỳ làm số dư đầu kỳ này
        var previousPeriod = await _context.CustomsSettlementPeriods
            .AsNoTracking()
            .Include(p => p.Items)
            .Where(p => p.ContractFolderId == request.ContractFolderId && p.ContractNo == request.ContractNo)
            .Where(p => p.ToDate < fromDate)
            .OrderByDescending(p => p.ToDate)
            .FirstOrDefaultAsync();

        var previousClosingBalances = previousPeriod?.Items
            .ToDictionary(i => NormalizeProductCode(i.ProductCode), i => i.ClosingBalance)
            ?? new Dictionary<string, decimal>();

        // 5. Gom nhóm các mặt hàng đã xuất theo Mã hình thể (loại bỏ .G)
        var exportGroupMap = new Dictionary<string, (int TotalQty, int OrderCount, HashSet<string> DeclarationNos, string ProductName, string Unit)>();

        foreach (var order in clearedOrders)
        {
            string declNo = order.DeclarationNo ?? order.InvoiceNo;
            foreach (var item in order.Items)
            {
                string code = NormalizeProductCode(item.StyleCode);
                if (string.IsNullOrEmpty(code)) continue;

                if (!exportGroupMap.TryGetValue(code, out var existing))
                {
                    productMasters.TryGetValue(code.ToUpper(), out var pm);
                    string name = pm?.Description ?? item.FullItemCode;
                    string unit = !string.IsNullOrWhiteSpace(pm?.Unit) ? pm.Unit : "đôi";

                    existing = (0, 0, new HashSet<string>(), name, unit);
                }

                existing.TotalQty += item.Quantity;
                existing.OrderCount += 1;
                if (!string.IsNullOrEmpty(declNo))
                {
                    existing.DeclarationNos.Add(declNo);
                }

                exportGroupMap[code] = existing;
            }
        }

        // 6. Tập hợp danh sách các mã sản phẩm cần hiển thị trên báo cáo:
        // Gồm các mã có xuất trong kỳ + các mã có tồn từ kỳ trước + tất cả ProductMasters trong danh mục
        var allProductCodes = exportGroupMap.Keys
            .Union(previousClosingBalances.Keys)
            .Union(productMasters.Keys.Select(k => NormalizeProductCode(k)))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var items = new List<SettlementItemDto>();
        int stt = 1;

        foreach (var code in allProductCodes)
        {
            productMasters.TryGetValue(code.ToUpper(), out var pm);
            previousClosingBalances.TryGetValue(code, out decimal prevClosing);
            bool hasExport = exportGroupMap.TryGetValue(code, out var exp);

            decimal openingBalance = prevClosing;
            decimal inPeriodProduction = 0; // Người dùng sẽ nhập bổ sung
            decimal inPeriodExport = hasExport ? exp.TotalQty : 0;
            decimal otherExport = 0;
            decimal closingBalance = (openingBalance + inPeriodProduction) - (inPeriodExport + otherExport);

            // Bỏ qua mã nếu cả tồn đầu, nhập, xuất đều = 0 và không có trong danh mục chính
            if (openingBalance == 0 && inPeriodExport == 0 && pm == null)
            {
                continue;
            }

            items.Add(new SettlementItemDto
            {
                Id = stt++,
                ProductCode = code,
                ProductName = pm?.Description ?? (hasExport ? exp.ProductName : code),
                Unit = !string.IsNullOrWhiteSpace(pm?.Unit) ? pm.Unit : "đôi",
                HsCode = pm?.HsCode ?? "64041990",
                OpeningBalance = openingBalance,
                InPeriodProduction = inPeriodProduction,
                InPeriodExport = inPeriodExport,
                OtherExport = otherExport,
                ClosingBalance = closingBalance,
                ExportedOrderCount = hasExport ? exp.OrderCount : 0,
                RelatedDeclarationNos = hasExport ? exp.DeclarationNos.ToList() : new List<string>()
            });
        }

        // Lấy thông tin công ty từ đơn hàng gần nhất hoặc mặc định
        var latestOrder = clearedOrders.FirstOrDefault() ?? await _context.ShipmentOrders.OrderByDescending(s => s.Id).FirstOrDefaultAsync();

        return new SettlementReportDto
        {
            Year = request.Year > 0 ? request.Year : fromDate.Year,
            FromDate = fromDate,
            ToDate = request.ToDate.Date,
            ContractFolderId = request.ContractFolderId,
            ContractNo = request.ContractNo,
            CustomsOffice = latestOrder?.CustomsOffice ?? _options.DefaultCustomsOffice,
            Status = "Draft",
            CompanyName = _options.DefaultCompanyName,
            TaxCode = _options.DefaultTaxCode,
            Address = latestOrder?.Address ?? _options.DefaultAddress,
            Items = items,
            ClearedOrderCount = clearedOrders.Count
        };
    }

    private async Task<List<SettlementItemDto>> RefreshExportQuantitiesAsync(int year, DateTime from, DateTime to,
        int? folderId, string? contractNo, List<SettlementItemDto> input)
    {
        var calculated = await CalculateSettlementAsync(new CalculateSettlementRequestDto {
            Year = year, FromDate = from, ToDate = to, ContractFolderId = folderId, ContractNo = contractNo });
        if (input.GroupBy(i => NormalizeProductCode(i.ProductCode)).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Danh sách kho chứa mã trùng sau chuẩn hóa.");
        var actual = calculated.Items.ToDictionary(i => NormalizeProductCode(i.ProductCode));
        foreach (var item in input)
        {
            if (item.OpeningBalance < 0 || item.InPeriodProduction < 0 || item.OtherExport < 0)
                throw new InvalidOperationException("Số liệu kho đầu vào không được âm.");
            var code = NormalizeProductCode(item.ProductCode);
            item.InPeriodExport = actual.TryGetValue(code, out var row) ? row.InPeriodExport : 0;
            item.ClosingBalance = item.OpeningBalance + item.InPeriodProduction - item.InPeriodExport - item.OtherExport;
            actual.Remove(code);
        }
        input.AddRange(actual.Values.Where(i => i.InPeriodExport > 0));
        return input;
    }

    /// <summary>
    /// Lưu kỳ quyết toán vào CSDL
    /// </summary>
    public async Task<CustomsSettlementPeriod> SaveSettlementPeriodAsync(SaveSettlementPeriodRequestDto request)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        request.Items = await RefreshExportQuantitiesAsync(request.Year, request.FromDate, request.ToDate, request.ContractFolderId, request.ContractNo, request.Items);
        CustomsSettlementPeriod? period = null;

        if (request.Id.HasValue && request.Id.Value > 0)
        {
            period = await _context.CustomsSettlementPeriods
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == request.Id.Value);
        }

        if (period == null)
        {
            period = new CustomsSettlementPeriod
            {
                Year = request.Year,
                FromDate = request.FromDate.Date,
                ToDate = request.ToDate.Date,
                ContractFolderId = request.ContractFolderId,
            ContractNo = request.ContractNo?.Trim(),
                CustomsOffice = string.IsNullOrWhiteSpace(request.CustomsOffice) ? _options.DefaultCustomsOffice : request.CustomsOffice.Trim(),
                Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim(),
                CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? _options.DefaultCompanyName : request.CompanyName.Trim(),
                TaxCode = string.IsNullOrWhiteSpace(request.TaxCode) ? _options.DefaultTaxCode : request.TaxCode.Trim(),
                Address = string.IsNullOrWhiteSpace(request.Address) ? _options.DefaultAddress : request.Address.Trim(),
                Note = request.Note?.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.CustomsSettlementPeriods.Add(period);
        }
        else
        {
            if (period.Status == "Finalized") throw new InvalidOperationException("Kỳ quyết toán đã chốt, không thể sửa.");
            period.ContractFolderId = request.ContractFolderId;
            period.Year = request.Year;
            period.FromDate = request.FromDate.Date;
            period.ToDate = request.ToDate.Date;
            period.ContractNo = request.ContractNo?.Trim();
            period.CustomsOffice = string.IsNullOrWhiteSpace(request.CustomsOffice) ? period.CustomsOffice : request.CustomsOffice.Trim();
            period.Status = string.IsNullOrWhiteSpace(request.Status) ? period.Status : request.Status.Trim();
            period.CompanyName = request.CompanyName?.Trim();
            period.TaxCode = request.TaxCode?.Trim();
            period.Address = request.Address?.Trim();
            period.Note = request.Note?.Trim();
            period.UpdatedAt = DateTime.UtcNow;

            _context.CustomsSettlementItems.RemoveRange(period.Items);
            period.Items.Clear();
        }

        // Thêm danh sách items mới
        foreach (var item in request.Items)
        {
            decimal closing = (item.OpeningBalance + item.InPeriodProduction) - (item.InPeriodExport + item.OtherExport);

            period.Items.Add(new CustomsSettlementItem
            {
                ProductCode = item.ProductCode.Trim(),
                ProductName = item.ProductName?.Trim() ?? string.Empty,
                Unit = !string.IsNullOrWhiteSpace(item.Unit) ? item.Unit.Trim() : "đôi",
                HsCode = !string.IsNullOrWhiteSpace(item.HsCode) ? item.HsCode.Trim() : "64041990",
                OpeningBalance = item.OpeningBalance,
                InPeriodProduction = item.InPeriodProduction,
                InPeriodExport = item.InPeriodExport,
                OtherExport = item.OtherExport,
                ClosingBalance = closing,
                Note = item.Note?.Trim()
            });
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return period;
    }

    /// <summary>
    /// Lấy danh sách các kỳ quyết toán đã lưu
    /// </summary>
    public async Task<List<SettlementPeriodSummaryDto>> GetSettlementPeriodsAsync()
    {
        var list = await _context.CustomsSettlementPeriods
            .AsNoTracking()
            .Include(p => p.Items)
            .OrderByDescending(p => p.FromDate)
            .ToListAsync();

        return list.Select(p => new SettlementPeriodSummaryDto
        {
            Id = p.Id,
            Year = p.Year,
            FromDate = p.FromDate,
            ToDate = p.ToDate,
            ContractFolderId = p.ContractFolderId,
            ContractNo = p.ContractNo,
            CustomsOffice = p.CustomsOffice,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            ItemCount = p.Items.Count,
            TotalExportQuantity = p.Items.Sum(i => i.InPeriodExport),
            TotalClosingBalance = p.Items.Sum(i => i.ClosingBalance)
        }).ToList();
    }

    /// <summary>
    /// Lấy chi tiết kỳ quyết toán theo Id
    /// </summary>
    public async Task<SettlementReportDto?> GetSettlementPeriodByIdAsync(int id)
    {
        var period = await _context.CustomsSettlementPeriods
            .AsNoTracking()
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (period == null) return null;

        int idx = 1;
        return new SettlementReportDto
        {
            PeriodId = period.Id,
            Year = period.Year,
            FromDate = period.FromDate,
            ToDate = period.ToDate,
            ContractFolderId = period.ContractFolderId,
            ContractNo = period.ContractNo,
            CustomsOffice = period.CustomsOffice,
            Status = period.Status,
            CompanyName = period.CompanyName ?? _options.DefaultCompanyName,
            TaxCode = period.TaxCode ?? _options.DefaultTaxCode,
            Address = period.Address ?? _options.DefaultAddress,
            Note = period.Note,
            Items = period.Items.Select(i => new SettlementItemDto
            {
                Id = idx++,
                ProductCode = i.ProductCode,
                ProductName = i.ProductName,
                Unit = i.Unit,
                HsCode = i.HsCode,
                OpeningBalance = i.OpeningBalance,
                InPeriodProduction = i.InPeriodProduction,
                InPeriodExport = i.InPeriodExport,
                OtherExport = i.OtherExport,
                ClosingBalance = i.ClosingBalance,
                Note = i.Note
            }).ToList()
        };
    }

    /// <summary>
    /// Xuất file Excel chuẩn đối chiếu nội bộ (Cần xác minh mẫu pháp lý trước khi nộp) kèm Sheet Drill-down
    /// </summary>
    public async Task<byte[]> ExportSettlementExcelAsync(SettlementReportDto report)
    {
        report.Items = await RefreshExportQuantitiesAsync(report.Year, report.FromDate, report.ToDate, report.ContractFolderId, report.ContractNo, report.Items);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Doi_Chieu_Noi_Bo");

        // Thiết lập trang in: khổ ngang A4
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;

        // Font mặc định Times New Roman (chuẩn biểu mẫu hành chính VN)
        ws.Style.Font.FontName = "Times New Roman";
        ws.Style.Font.FontSize = 11;

        // 1. Header cơ quan & Doanh nghiệp (Rows 1-4)
        ws.Cell("A1").Value = "Tên tổ chức, cá nhân: " + (report.CompanyName ?? _options.DefaultCompanyName);
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A2").Value = "Mã số thuế: " + (report.TaxCode ?? _options.DefaultTaxCode);
        ws.Cell("A3").Value = "Chi cục Hải quan tiếp nhận: " + (report.CustomsOffice ?? _options.DefaultCustomsOffice);
        ws.Cell("A4").Value = "Số hợp đồng gia công: " + (report.ContractNo ?? "");

        ws.Cell("G1").Value = "BÁO CÁO NỘI BỘ";
        ws.Cell("G1").Style.Font.Bold = true;
        ws.Cell("G1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Range("G1:J1").Merge();

        ws.Cell("G2").Value = "Phụ lục II Thông tư số 39/2018/TT-BTC";
        ws.Cell("G2").Style.Font.Italic = true;
        ws.Cell("G2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Range("G2:J2").Merge();

        ws.Cell("G3").Value = "của Bộ Tài chính";
        ws.Cell("G3").Style.Font.Italic = true;
        ws.Cell("G3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Range("G3:J3").Merge();

        ws.Cell("G4").Value = $"Báo cáo cho năm tài chính: {report.Year}";
        ws.Cell("G4").Style.Font.Bold = true;
        ws.Cell("G4").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Range("G4:J4").Merge();

        // 2. Tiêu đề báo cáo (Rows 5-7)
        ws.Cell("A5").Value = "BÁO CÁO ĐỐI CHIẾU NỘI BỘ NHẬP – XUẤT – TỒN SẢN PHẨM";
        ws.Cell("A5").Style.Font.FontSize = 14;
        ws.Cell("A5").Style.Font.Bold = true;
        ws.Cell("A5").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range("A5:J5").Merge();

        ws.Cell("A6").Value = "Đối chiếu nội bộ — Cần xác minh mẫu pháp lý trước khi nộp";
        ws.Cell("A6").Style.Font.FontSize = 12;
        ws.Cell("A6").Style.Font.Italic = true;
        ws.Cell("A6").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range("A6:J6").Merge();

        string periodText = $"Kỳ báo cáo: Từ ngày {report.FromDate:dd/MM/yyyy} đến ngày {report.ToDate:dd/MM/yyyy}";
        if (!string.IsNullOrWhiteSpace(report.ContractNo))
        {
            periodText += $"  |  Hợp đồng gia công: {report.ContractNo}";
        }
        ws.Cell("A7").Value = periodText;
        ws.Cell("A7").Style.Font.Italic = true;
        ws.Cell("A7").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range("A7:J7").Merge();

        // 3. Tiêu đề bảng biểu (Rows 9-11)
        int headerStartRow = 9;
        
        ws.Cell(headerStartRow, 1).Value = "STT";
        ws.Range(headerStartRow, 1, headerStartRow + 1, 1).Merge();

        ws.Cell(headerStartRow, 2).Value = "Mã sản phẩm";
        ws.Range(headerStartRow, 2, headerStartRow + 1, 2).Merge();

        ws.Cell(headerStartRow, 3).Value = "Tên sản phẩm";
        ws.Range(headerStartRow, 3, headerStartRow + 1, 3).Merge();

        ws.Cell(headerStartRow, 4).Value = "Đơn vị\ntính";
        ws.Range(headerStartRow, 4, headerStartRow + 1, 4).Merge();

        ws.Cell(headerStartRow, 5).Value = "Lượng tồn\nđầu kỳ";
        ws.Range(headerStartRow, 5, headerStartRow + 1, 5).Merge();

        ws.Cell(headerStartRow, 6).Value = "Lượng nhập\ntrong kỳ\n(Sản xuất)";
        ws.Range(headerStartRow, 6, headerStartRow + 1, 6).Merge();

        ws.Cell(headerStartRow, 7).Value = "Lượng xuất trong kỳ";
        ws.Range(headerStartRow, 7, headerStartRow, 8).Merge();
        ws.Cell(headerStartRow + 1, 7).Value = "Xuất khẩu\n(Gia công E52)";
        ws.Cell(headerStartRow + 1, 8).Value = "Xuất\nkhác";

        ws.Cell(headerStartRow, 9).Value = "Lượng tồn\ncuối kỳ";
        ws.Range(headerStartRow, 9, headerStartRow + 1, 9).Merge();

        ws.Cell(headerStartRow, 10).Value = "Ghi chú";
        ws.Range(headerStartRow, 10, headerStartRow + 1, 10).Merge();

        // Dòng đánh số thứ tự cột (Row 11)
        int colNumRow = headerStartRow + 2;
        ws.Cell(colNumRow, 1).Value = "(1)";
        ws.Cell(colNumRow, 2).Value = "(2)";
        ws.Cell(colNumRow, 3).Value = "(3)";
        ws.Cell(colNumRow, 4).Value = "(4)";
        ws.Cell(colNumRow, 5).Value = "(5)";
        ws.Cell(colNumRow, 6).Value = "(6)";
        ws.Cell(colNumRow, 7).Value = "(7)";
        ws.Cell(colNumRow, 8).Value = "(8)";
        ws.Cell(colNumRow, 9).Value = "(9)=(5)+(6)-(7)-(8)";
        ws.Cell(colNumRow, 10).Value = "(10)";

        // Style cho Header (Rows 9-11)
        var headerRange = ws.Range(headerStartRow, 1, colNumRow, 10);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9"); // Slate-100
        headerRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        headerRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

        // 4. Đổ dữ liệu các dòng hàng (Rows 12+)
        int currentRow = colNumRow + 1;
        int dataStartRow = currentRow;

        for (int i = 0; i < report.Items.Count; i++)
        {
            var item = report.Items[i];

            ws.Cell(currentRow, 1).Value = i + 1;
            ws.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(currentRow, 2).SetValue(item.ProductCode);
            ws.Cell(currentRow, 2).Style.Font.FontName = "Consolas";
            ws.Cell(currentRow, 2).Style.Font.Bold = true;

            ws.Cell(currentRow, 3).SetValue(item.ProductName);

            ws.Cell(currentRow, 4).SetValue(item.Unit);
            ws.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Số lượng số dư & biến động (Format số nguyên có phân cách hàng nghìn)
            ws.Cell(currentRow, 5).SetValue(item.OpeningBalance);
            ws.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0";

            ws.Cell(currentRow, 6).SetValue(item.InPeriodProduction);
            ws.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0";

            ws.Cell(currentRow, 7).SetValue(item.InPeriodExport);
            ws.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0";

            ws.Cell(currentRow, 8).SetValue(item.OtherExport);
            ws.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0";

            // Tồn cuối kỳ: công thức Excel = E{row}+F{row}-G{row}-H{row}
            ws.Cell(currentRow, 9).FormulaA1 = $"E{currentRow}+F{currentRow}-G{currentRow}-H{currentRow}";
            ws.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0";
            ws.Cell(currentRow, 9).Style.Font.Bold = true;

            ws.Cell(currentRow, 10).SetValue(item.Note ?? string.Empty);

            currentRow++;
        }

        int dataEndRow = currentRow - 1;

        // 5. Dòng TỔNG CỘNG
        if (report.Items.Count > 0)
        {
            ws.Cell(currentRow, 1).Value = "TỔNG CỘNG";
            ws.Range(currentRow, 1, currentRow, 4).Merge();
            ws.Cell(currentRow, 1).Style.Font.Bold = true;
            ws.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(currentRow, 5).FormulaA1 = $"SUM(E{dataStartRow}:E{dataEndRow})";
            ws.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(currentRow, 5).Style.Font.Bold = true;

            ws.Cell(currentRow, 6).FormulaA1 = $"SUM(F{dataStartRow}:F{dataEndRow})";
            ws.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(currentRow, 6).Style.Font.Bold = true;

            ws.Cell(currentRow, 7).FormulaA1 = $"SUM(G{dataStartRow}:G{dataEndRow})";
            ws.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0";
            ws.Cell(currentRow, 7).Style.Font.Bold = true;

            ws.Cell(currentRow, 8).FormulaA1 = $"SUM(H{dataStartRow}:H{dataEndRow})";
            ws.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0";
            ws.Cell(currentRow, 8).Style.Font.Bold = true;

            ws.Cell(currentRow, 9).FormulaA1 = $"SUM(I{dataStartRow}:I{dataEndRow})";
            ws.Cell(currentRow, 9).Style.NumberFormat.Format = "#,##0";
            ws.Cell(currentRow, 9).Style.Font.Bold = true;

            var dataRange = ws.Range(dataStartRow, 1, currentRow, 10);
            dataRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            dataRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

            var totalRowRange = ws.Range(currentRow, 1, currentRow, 10);
            totalRowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");

            currentRow++;
        }

        // 6. Phần chữ ký (Signatures)
        currentRow += 2;
        string dateSignText = $"Ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}";
        ws.Cell(currentRow, 8).Value = dateSignText;
        ws.Cell(currentRow, 8).Style.Font.Italic = true;
        ws.Cell(currentRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(currentRow, 8, currentRow, 10).Merge();

        currentRow++;
        ws.Cell(currentRow, 2).Value = "NGƯỜI LẬP BIỂU";
        ws.Cell(currentRow, 2).Style.Font.Bold = true;
        ws.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(currentRow, 2, currentRow, 3).Merge();

        ws.Cell(currentRow, 5).Value = "KẾ TOÁN TRƯỞNG";
        ws.Cell(currentRow, 5).Style.Font.Bold = true;
        ws.Cell(currentRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(currentRow, 5, currentRow, 6).Merge();

        ws.Cell(currentRow, 8).Value = "NGƯỜI ĐẠI DIỆN THEO PHÁP LUẬT";
        ws.Cell(currentRow, 8).Style.Font.Bold = true;
        ws.Cell(currentRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(currentRow, 8, currentRow, 10).Merge();

        currentRow++;
        ws.Cell(currentRow, 2).Value = "(Ký, ghi rõ họ tên)";
        ws.Cell(currentRow, 2).Style.Font.Italic = true;
        ws.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(currentRow, 2, currentRow, 3).Merge();

        ws.Cell(currentRow, 5).Value = "(Ký, ghi rõ họ tên)";
        ws.Cell(currentRow, 5).Style.Font.Italic = true;
        ws.Cell(currentRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(currentRow, 5, currentRow, 6).Merge();

        ws.Cell(currentRow, 8).Value = "(Ký, ghi rõ họ tên, đóng dấu)";
        ws.Cell(currentRow, 8).Style.Font.Italic = true;
        ws.Cell(currentRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(currentRow, 8, currentRow, 10).Merge();

        // 7. Căn chỉnh độ rộng cột Sheet 1
        ws.Column(1).Width = 6;    // STT
        ws.Column(2).Width = 18;   // Mã SP
        ws.Column(3).Width = 35;   // Tên SP
        ws.Column(4).Width = 8;    // ĐVT
        ws.Column(5).Width = 15;   // Tồn đầu
        ws.Column(6).Width = 15;   // Nhập SX
        ws.Column(7).Width = 16;   // Xuất E52
        ws.Column(8).Width = 12;   // Xuất khác
        ws.Column(9).Width = 15;   // Tồn cuối
        ws.Column(10).Width = 18;  // Ghi chú

        // ==========================================
        // SHEET 2: BẢNG KÊ CHI TIẾT TỜ KHAI (DRILL-DOWN)
        // ==========================================
        var ws2 = workbook.Worksheets.Add("Bang_Ke_Chi_Tiet_E52");
        ws2.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws2.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws2.Style.Font.FontName = "Times New Roman";
        ws2.Style.Font.FontSize = 11;

        ws2.Cell("A1").Value = "Tên tổ chức, cá nhân: " + (report.CompanyName ?? _options.DefaultCompanyName);
        ws2.Cell("A1").Style.Font.Bold = true;
        ws2.Cell("A2").Value = "Mã số thuế: " + (report.TaxCode ?? _options.DefaultTaxCode);
        ws2.Cell("A3").Value = "Chi cục HQ: " + (report.CustomsOffice ?? _options.DefaultCustomsOffice);

        ws2.Cell("A5").Value = "BẢNG KÊ CHI TIẾT TỜ KHAI HẢI QUAN XUẤT KHẨU GIA CÔNG (E52) ĐÃ THÔNG QUAN";
        ws2.Cell("A5").Style.Font.FontSize = 13;
        ws2.Cell("A5").Style.Font.Bold = true;
        ws2.Cell("A5").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws2.Range("A5:J5").Merge();

        ws2.Cell("A6").Value = $"Kỳ báo cáo: Từ ngày {report.FromDate:dd/MM/yyyy} đến ngày {report.ToDate:dd/MM/yyyy}";
        ws2.Cell("A6").Style.Font.Italic = true;
        ws2.Cell("A6").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws2.Range("A6:J6").Merge();

        int s2HeaderRow = 8;
        ws2.Cell(s2HeaderRow, 1).Value = "STT";
        ws2.Cell(s2HeaderRow, 2).Value = "Mã sản phẩm (Hình thể)";
        ws2.Cell(s2HeaderRow, 3).Value = "Số tờ khai hải quan";
        ws2.Cell(s2HeaderRow, 4).Value = "Ngày thông quan";
        ws2.Cell(s2HeaderRow, 5).Value = "Số hóa đơn (INV)";
        ws2.Cell(s2HeaderRow, 6).Value = "Số hợp đồng";
        ws2.Cell(s2HeaderRow, 7).Value = "Mã hàng chi tiết / Diễn giải";
        ws2.Cell(s2HeaderRow, 8).Value = "Số lượng xuất (đôi)";
        ws2.Cell(s2HeaderRow, 9).Value = "Đơn giá CMT ($)";
        ws2.Cell(s2HeaderRow, 10).Value = "Đơn giá DAP ($)";

        var s2HeaderRange = ws2.Range(s2HeaderRow, 1, s2HeaderRow, 10);
        s2HeaderRange.Style.Font.Bold = true;
        s2HeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        s2HeaderRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        s2HeaderRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        s2HeaderRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        s2HeaderRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

        // Đóng băng dòng tiêu đề Sheet 2 (Freeze Panes)
        ws2.SheetView.FreezeRows(s2HeaderRow);

        var fromDate = report.FromDate.Date;
        var toDate = report.ToDate.Date.AddDays(1).AddTicks(-1);

        var queryOrders = _context.ShipmentOrders
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.Status == ShipmentStatus.Cleared)
            .Where(s => s.CustomsDeclarationType == "E52")
            .Where(s => (s.ClearanceDate ?? s.InvoiceDate) >= fromDate && (s.ClearanceDate ?? s.InvoiceDate) <= toDate);

        if (report.ContractFolderId.HasValue) queryOrders = queryOrders.Where(s => s.ContractFolderId == report.ContractFolderId);
        if (!string.IsNullOrWhiteSpace(report.ContractNo))
        {
            string contract = report.ContractNo.Trim().ToUpperInvariant();
            queryOrders = queryOrders.Where(s => s.ContractNo != null && s.ContractNo.ToUpper() == contract);
        }

        var clearedOrders = await queryOrders.ToListAsync();

        int s2Row = s2HeaderRow + 1;
        int s2StartDataRow = s2Row;
        int s2Stt = 1;

        foreach (var order in clearedOrders.OrderBy(o => o.ClearanceDate ?? o.InvoiceDate))
        {
            foreach (var item in order.Items)
            {
                string norm = NormalizeProductCode(item.StyleCode);
                ws2.Cell(s2Row, 1).Value = s2Stt++;
                ws2.Cell(s2Row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws2.Cell(s2Row, 2).SetValue(norm);
                ws2.Cell(s2Row, 2).Style.Font.FontName = "Consolas";
                ws2.Cell(s2Row, 2).Style.Font.Bold = true;

                ws2.Cell(s2Row, 3).SetValue(order.DeclarationNo ?? "N/A");
                ws2.Cell(s2Row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var clDate = order.ClearanceDate ?? order.InvoiceDate;
                ws2.Cell(s2Row, 4).SetValue(clDate.ToString("dd/MM/yyyy HH:mm"));
                ws2.Cell(s2Row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws2.Cell(s2Row, 5).SetValue(order.InvoiceNo);
                ws2.Cell(s2Row, 6).SetValue(order.ContractNo ?? string.Empty);
                ws2.Cell(s2Row, 7).SetValue(item.FullItemCode);

                ws2.Cell(s2Row, 8).SetValue(item.Quantity);
                ws2.Cell(s2Row, 8).Style.NumberFormat.Format = "#,##0";

                ws2.Cell(s2Row, 9).SetValue(item.UnitPriceCMT);
                ws2.Cell(s2Row, 9).Style.NumberFormat.Format = "#,##0.00";

                ws2.Cell(s2Row, 10).SetValue(item.UnitPriceDAP);
                ws2.Cell(s2Row, 10).Style.NumberFormat.Format = "#,##0.00";

                s2Row++;
            }
        }

        int s2EndDataRow = s2Row - 1;
        if (s2EndDataRow >= s2StartDataRow)
        {
            ws2.Cell(s2Row, 1).Value = "TỔNG CỘNG";
            ws2.Range(s2Row, 1, s2Row, 7).Merge();
            ws2.Cell(s2Row, 1).Style.Font.Bold = true;
            ws2.Cell(s2Row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws2.Cell(s2Row, 8).FormulaA1 = $"SUM(H{s2StartDataRow}:H{s2EndDataRow})";
            ws2.Cell(s2Row, 8).Style.NumberFormat.Format = "#,##0";
            ws2.Cell(s2Row, 8).Style.Font.Bold = true;

            var s2DataRange = ws2.Range(s2StartDataRow, 1, s2Row, 10);
            s2DataRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            s2DataRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

            var s2TotalRange = ws2.Range(s2Row, 1, s2Row, 10);
            s2TotalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
        }

        ws2.Column(1).Width = 6;
        ws2.Column(2).Width = 18;
        ws2.Column(3).Width = 18;
        ws2.Column(4).Width = 18;
        ws2.Column(5).Width = 20;
        ws2.Column(6).Width = 16;
        ws2.Column(7).Width = 28;
        ws2.Column(8).Width = 16;
        ws2.Column(9).Width = 14;
        ws2.Column(10).Width = 14;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Xuất file Excel đối chiếu nội bộ theo ID kỳ đã lưu
    /// </summary>
    public async Task<byte[]> ExportSettlementExcelByIdAsync(int id)
    {
        var report = await GetSettlementPeriodByIdAsync(id);
        if (report == null)
        {
            throw new ArgumentException($"Không tìm thấy kỳ quyết toán ID {id}");
        }
        return await ExportSettlementExcelAsync(report);
    }

    /// <summary>
    /// Trả về danh sách chi tiết các tờ khai tạo nên số lượng xuất của mã sản phẩm (Drill-down)
    /// </summary>
    public async Task<List<SettlementDrillDownItemDto>> GetDrillDownAsync(
        string productCode,
        DateTime fromDate,
        DateTime toDate,
        string? contractNo = null)
    {
        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1).AddTicks(-1);
        string targetNormalized = NormalizeProductCode(productCode);

        var query = _context.ShipmentOrders
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.Status == ShipmentStatus.Cleared)
            .Where(s => s.CustomsDeclarationType == "E52")
            .Where(s => (s.ClearanceDate ?? s.InvoiceDate) >= from && (s.ClearanceDate ?? s.InvoiceDate) <= to);

        if (!string.IsNullOrWhiteSpace(contractNo))
        {
            string contract = contractNo.Trim().ToUpperInvariant();
            query = query.Where(s => s.ContractNo != null && s.ContractNo.ToUpper() == contract);
        }

        var orders = await query.ToListAsync();
        var result = new List<SettlementDrillDownItemDto>();

        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                string norm = NormalizeProductCode(item.StyleCode);
                if (norm.Equals(targetNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new SettlementDrillDownItemDto
                    {
                        OrderId = order.Id,
                        DeclarationNo = order.DeclarationNo ?? "N/A",
                        ClearanceDate = order.ClearanceDate ?? order.InvoiceDate,
                        InvoiceNo = order.InvoiceNo,
                        ContractNo = order.ContractNo,
                        ProductCode = norm,
                        FullItemCode = item.FullItemCode,
                        Quantity = item.Quantity,
                        UnitPriceCMT = item.UnitPriceCMT,
                        UnitPriceDAP = item.UnitPriceDAP,
                        CustomerName = order.CustomerName
                    });
                }
            }
        }

        return result.OrderByDescending(r => r.ClearanceDate).ToList();
    }

    public static string NormalizeProductCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        string clean = code.Trim().ToUpperInvariant();

        // 1. Loại bỏ hậu tố .G
        if (clean.EndsWith(".G"))
        {
            clean = clean[..^2].Trim();
        }

        // 2. Loại bỏ các hậu tố PO: -PO..., /PO..., .PO..., KM3.PO5.26, v.v.
        var poRegex = new System.Text.RegularExpressions.Regex(@"([\.\-\/\s]+(KM3[\.\-\/])?PO[\w\.\-]+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        clean = poRegex.Replace(clean, "").Trim();

        int km3PoIdx = clean.IndexOf("KM3.PO", StringComparison.OrdinalIgnoreCase);
        if (km3PoIdx > 0)
        {
            clean = clean[..km3PoIdx].TrimEnd('.', '-', '/', ' ');
        }

        // 3. Sau khi bỏ PO, nếu còn sót .G thì bỏ tiếp
        if (clean.EndsWith(".G"))
        {
            clean = clean[..^2].Trim();
        }

        return clean;
    }

    /// <summary>
    /// Thống kê phân tích kim ngạch, sản lượng, doanh thu CMT và phân bổ luồng tờ khai theo 12 tháng
    /// </summary>
    public async Task<AnalyticsExportStatsDto> GetExportAnalyticsAsync(int year)
    {
        var fromDate = new DateTime(year, 1, 1);
        var toDate = new DateTime(year, 12, 31, 23, 59, 59);

        var clearedOrders = await _context.ShipmentOrders
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.Status == ShipmentStatus.Cleared)
            .Where(s => (s.ClearanceDate ?? s.InvoiceDate) >= fromDate && (s.ClearanceDate ?? s.InvoiceDate) <= toDate)
            .ToListAsync();

        var masterRows = await _context.ProductMasters
            .AsNoTracking()
            .ToListAsync();
        var productMasters = masterRows.GroupBy(p => p.StyleCode.ToUpperInvariant()).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());

        // Thống kê 12 tháng
        var monthlyList = new List<MonthlyExportStatDto>();
        for (int m = 1; m <= 12; m++)
        {
            var monthOrders = clearedOrders
                .Where(o => (o.ClearanceDate ?? o.InvoiceDate).Month == m)
                .ToList();

            int mQty = monthOrders.SelectMany(o => o.Items).Sum(i => i.Quantity);
            decimal mDap = monthOrders.Sum(o => o.CustomsTotalDap ?? o.Items.Sum(i => i.Quantity * i.UnitPriceDAP));
            decimal mCmt = monthOrders.Sum(o => o.CustomsTotalCmt ?? o.Items.Sum(i => i.Quantity * i.UnitPriceCMT));

            monthlyList.Add(new MonthlyExportStatDto
            {
                Month = m,
                MonthName = $"Tháng {m}",
                Quantity = mQty,
                TotalDap = mDap,
                TotalCmt = mCmt,
                OrderCount = monthOrders.Count
            });
        }

        int totalQty = clearedOrders.SelectMany(o => o.Items).Sum(i => i.Quantity);
        decimal totalDap = clearedOrders.Sum(o => o.CustomsTotalDap ?? o.Items.Sum(i => i.Quantity * i.UnitPriceDAP));
        decimal totalCmt = clearedOrders.Sum(o => o.CustomsTotalCmt ?? o.Items.Sum(i => i.Quantity * i.UnitPriceCMT));

        // Top 5 mã giày xuất khẩu nhiều nhất
        var styleGroup = clearedOrders
            .SelectMany(o => o.Items)
            .GroupBy(i => NormalizeProductCode(i.StyleCode))
            .Select(g =>
            {
                string code = g.Key;
                productMasters.TryGetValue(code.ToUpper(), out var pm);
                int qty = g.Sum(x => x.Quantity);
                decimal dap = g.Sum(x => x.Quantity * x.UnitPriceDAP);
                decimal cmt = g.Sum(x => x.Quantity * x.UnitPriceCMT);
                return new
                {
                    StyleCode = code,
                    ProductName = pm?.Description ?? code,
                    Quantity = qty,
                    TotalDap = dap,
                    TotalCmt = cmt
                };
            })
            .OrderByDescending(s => s.Quantity)
            .Take(5)
            .ToList();

        var topStyles = new List<TopExportStyleDto>();
        int rank = 1;
        foreach (var s in styleGroup)
        {
            decimal pct = totalQty > 0 ? Math.Round((decimal)s.Quantity * 100 / totalQty, 1) : 0;
            topStyles.Add(new TopExportStyleDto
            {
                Rank = rank++,
                StyleCode = s.StyleCode,
                ProductName = s.ProductName,
                Quantity = s.Quantity,
                TotalDap = s.TotalDap,
                TotalCmt = s.TotalCmt,
                Percentage = pct
            });
        }

        // Tỷ lệ luồng thông quan: 1: Xanh, 2: Vàng, 3: Đỏ
        int green = clearedOrders.Count(o => o.CustomsChannel == 1);
        int yellow = clearedOrders.Count(o => o.CustomsChannel == 2);
        int red = clearedOrders.Count(o => o.CustomsChannel == 3);
        int totalDeclarations = clearedOrders.Count;
        if (green == 0 && yellow == 0 && red == 0 && totalDeclarations > 0)
        {
            // Mặc định phân bổ nếu chưa có phân luồng cụ thể
            // Missing channel remains unknown.
        }

        return new AnalyticsExportStatsDto
        {
            Year = year,
            TotalQuantity = totalQty,
            TotalDap = totalDap,
            TotalCmt = totalCmt,
            ClearedOrderCount = clearedOrders.Count,
            MonthlyStats = monthlyList,
            TopStyles = topStyles,
            ChannelStats = new CustomsChannelStatDto
            {
                GreenCount = green,
                YellowCount = yellow,
                RedCount = red,
                TotalDeclarations = totalDeclarations
            }
        };
    }

    /// <summary>
    /// Nạp và đọc file Excel số liệu kho định kỳ (Tồn đầu kỳ & Nhập sản xuất trong kỳ),
    /// tự động tìm cột theo tiêu đề hoặc vị trí và ghép với danh sách xuất khẩu đã tổng hợp.
    /// </summary>
    public async Task<WarehouseImportResultDto> ImportWarehouseExcelAsync(Stream stream, List<SettlementItemDto> currentItems)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws == null)
        {
            throw new ArgumentException("File Excel không có trang tính (worksheet) nào.");
        }

        var rows = new List<WarehouseDataRowDto>();
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

        if (lastRow == 0)
        {
            return new WarehouseImportResultDto { Items = currentItems };
        }

        // 1. Tìm dòng header thông minh
        int headerRow = 1;
        int codeCol = 1;
        int openingCol = 2;
        int productionCol = 3;
        bool foundHeader = false;

        for (int r = 1; r <= Math.Min(15, lastRow); r++)
        {
            for (int c = 1; c <= lastCol; c++)
            {
                string text = ws.Cell(r, c).GetString().Trim().ToLowerInvariant();
                if (text.Contains("mã") || text.Contains("style") || text.Contains("product") || text.Contains("mặt hàng"))
                {
                    codeCol = c;
                    headerRow = r;
                    foundHeader = true;
                }
                else if (text.Contains("tồn đầu") || text.Contains("đầu kỳ") || text.Contains("opening") || (text.Contains("tồn") && !text.Contains("cuối")))
                {
                    openingCol = c;
                }
                else if (text.Contains("nhập sx") || text.Contains("nhập kho") || text.Contains("sản xuất") || text.Contains("nhập trong") || text.Contains("production"))
                {
                    productionCol = c;
                }
            }

            if (foundHeader) break;
        }

        // 2. Đọc các dòng dữ liệu
        int startDataRow = foundHeader ? headerRow + 1 : 1;
        for (int r = startDataRow; r <= lastRow; r++)
        {
            string rawCode = ws.Cell(r, codeCol).GetString().Trim();
            if (string.IsNullOrWhiteSpace(rawCode)) continue;

            // Bỏ qua nếu dòng này là dòng tổng cộng
            if (rawCode.Equals("TỔNG CỘNG", StringComparison.OrdinalIgnoreCase) ||
                rawCode.Equals("TOTAL", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            decimal opening = 0;
            decimal production = 0;

            if (openingCol > 0 && openingCol <= lastCol)
            {
                var cellVal = ws.Cell(r, openingCol);
                if (cellVal.DataType == XLDataType.Number)
                {
                    opening = Convert.ToDecimal(cellVal.GetDouble());
                }
                else
                {
                    var text = cellVal.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        opening = 0;
                    }
                    else if (!CustomsDeclarationService.TryParseCustomsDecimal(text, out opening))
                    {
                        throw new InvalidOperationException($"Số tồn đầu không hợp lệ tại dòng {r}, cột {openingCol}.");
                    }
                }
            }

            if (productionCol > 0 && productionCol <= lastCol)
            {
                var cellVal = ws.Cell(r, productionCol);
                if (cellVal.DataType == XLDataType.Number)
                {
                    production = Convert.ToDecimal(cellVal.GetDouble());
                }
                else
                {
                    var text = cellVal.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        production = 0;
                    }
                    else if (!CustomsDeclarationService.TryParseCustomsDecimal(text, out production))
                    {
                        throw new InvalidOperationException($"Số nhập sản xuất không hợp lệ tại dòng {r}, cột {productionCol}.");
                    }
                }
            }

            rows.Add(new WarehouseDataRowDto
            {
                ProductCode = rawCode,
                OpeningBalance = Math.Max(0, opening),
                InPeriodProduction = Math.Max(0, production)
            });
        }

        return await MatchWarehouseRowsAsync(rows, currentItems);
    }

    /// <summary>
    /// Đối soát và ghép dữ liệu kho vào danh sách quyết toán:
    /// - Khớp mã: cập nhật Tồn đầu & Nhập SX
    /// - Mã có ở kho nhưng XNK chưa xuất: thêm mới với InPeriodExport = 0
    /// - Tự động tính toán lại Tồn cuối và phát hiện âm tồn
    /// </summary>
    public async Task<WarehouseImportResultDto> MatchWarehouseRowsAsync(List<WarehouseDataRowDto> rows, List<SettlementItemDto> currentItems)
    {
        var masterRows = await _context.ProductMasters
            .AsNoTracking()
            .ToListAsync();
        var productMasters = masterRows.GroupBy(p => p.StyleCode.ToUpperInvariant()).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());

        // Gom nhóm các dòng kho theo mã chuẩn hóa
        var warehouseMap = new Dictionary<string, (decimal Opening, decimal Production)>();
        foreach (var r in rows)
        {
            string norm = NormalizeProductCode(r.ProductCode);
            if (string.IsNullOrWhiteSpace(norm)) continue;

            if (!warehouseMap.TryGetValue(norm, out var existing))
            {
                existing = (0, 0);
            }
            existing.Opening += r.OpeningBalance;
            existing.Production += r.InPeriodProduction;
            warehouseMap[norm] = existing;
        }

        var resultItems = currentItems.Select(i => new SettlementItemDto
        {
            Id = i.Id,
            ProductCode = i.ProductCode,
            ProductName = i.ProductName,
            Unit = i.Unit,
            HsCode = i.HsCode,
            OpeningBalance = i.OpeningBalance,
            InPeriodProduction = i.InPeriodProduction,
            InPeriodExport = i.InPeriodExport,
            OtherExport = i.OtherExport,
            ClosingBalance = i.ClosingBalance,
            Note = i.Note,
            ExportedOrderCount = i.ExportedOrderCount,
            RelatedDeclarationNos = i.RelatedDeclarationNos
        }).ToList();

        var existingMap = resultItems.ToDictionary(i => NormalizeProductCode(i.ProductCode), i => i);
        int matchedCount = 0;
        int addedFromWarehouse = 0;
        var warnings = new List<string>();

        foreach (var kvp in warehouseMap)
        {
            string code = kvp.Key;
            var (whOpening, whProd) = kvp.Value;

            if (existingMap.TryGetValue(code, out var item))
            {
                item.OpeningBalance = whOpening;
                item.InPeriodProduction = whProd;
                matchedCount++;
            }
            else
            {
                // Mã kho có nhưng chưa có xuất khẩu trong kỳ -> Thêm vào bảng với xuất khẩu = 0
                productMasters.TryGetValue(code.ToUpper(), out var pm);
                var newItem = new SettlementItemDto
                {
                    Id = resultItems.Count + 1,
                    ProductCode = code,
                    ProductName = pm?.Description ?? code,
                    Unit = !string.IsNullOrWhiteSpace(pm?.Unit) ? pm.Unit : "đôi",
                    HsCode = pm?.HsCode ?? "64041990",
                    OpeningBalance = whOpening,
                    InPeriodProduction = whProd,
                    InPeriodExport = 0,
                    OtherExport = 0,
                    ClosingBalance = whOpening + whProd,
                    Note = "Số liệu nhập từ kho (Chưa xuất khẩu)",
                    ExportedOrderCount = 0,
                    RelatedDeclarationNos = new List<string>()
                };
                resultItems.Add(newItem);
                existingMap[code] = newItem;
                addedFromWarehouse++;
            }
        }

        // Tự động tính lại Tồn cuối kỳ cho toàn bộ danh sách và kiểm tra cảnh báo
        int negativeCount = 0;
        foreach (var item in resultItems)
        {
            item.ClosingBalance = (item.OpeningBalance + item.InPeriodProduction) - (item.InPeriodExport + item.OtherExport);
            if (item.ClosingBalance < 0)
            {
                negativeCount++;
                warnings.Add($"Mã {item.ProductCode}: Thiếu hụt tồn kho {Math.Abs(item.ClosingBalance):#,##0} đôi (Xuất: {item.InPeriodExport:#,##0}, Tồn đầu + Nhập: {item.OpeningBalance + item.InPeriodProduction:#,##0})");
            }
        }

        // Sắp xếp lại thứ tự theo mã SP
        resultItems = resultItems.OrderBy(i => i.ProductCode).ToList();
        for (int i = 0; i < resultItems.Count; i++)
        {
            resultItems[i].Id = i + 1;
        }

        return new WarehouseImportResultDto
        {
            MatchedCount = matchedCount,
            AddedFromWarehouseCount = addedFromWarehouse,
            TotalRows = warehouseMap.Count,
            NegativeItemCount = negativeCount,
            Items = resultItems,
            Warnings = warnings
        };
    }
}
