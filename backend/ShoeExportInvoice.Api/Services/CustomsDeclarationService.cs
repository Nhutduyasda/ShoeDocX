using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public class CustomsDeclarationService : ICustomsDeclarationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CustomsDeclarationService> _logger;
    private readonly IWebHostEnvironment _environment;

    public CustomsDeclarationService(
        AppDbContext context,
        ILogger<CustomsDeclarationService> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _environment = environment;

        // Đảm bảo đăng ký CodePagesEncodingProvider để đọc các file Excel 97-2003 (.xls) legacy encoding
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Bóc tách thông tin tờ khai VNACCS từ file .xls / .xlsx
    /// </summary>
    public CustomsDeclarationParsedDto ParseDeclarationFile(Stream fileStream, string fileName)
    {
        using var reader = ExcelReaderFactory.CreateReader(fileStream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false
            }
        });

        if (dataSet.Tables.Count == 0)
        {
            throw new InvalidOperationException("File Excel không chứa bảng dữ liệu nào.");
        }

        // Ưu tiên sheet có tên 'TKX' (Tờ khai xuất), nếu không lấy sheet đầu tiên
        DataTable table = dataSet.Tables.Cast<DataTable>()
            .FirstOrDefault(t => t.TableName.IndexOf("TKX", StringComparison.OrdinalIgnoreCase) >= 0)
            ?? dataSet.Tables[0];

        var result = new CustomsDeclarationParsedDto
        {
            FileName = fileName
        };

        int rowCount = table.Rows.Count;
        int colCount = table.Columns.Count;

        string GetCell(int r, int c)
        {
            if (r < 0 || r >= rowCount || c < 0 || c >= colCount) return string.Empty;
            var val = table.Rows[r][c];
            return val == null || val == DBNull.Value ? string.Empty : val.ToString()?.Trim() ?? string.Empty;
        }

        string FindValueNear(int r, int c, string labelPrefix = "")
        {
            // Kiểm tra nội dung trong cùng ô sau dấu hai chấm
            string current = GetCell(r, c);
            if (!string.IsNullOrEmpty(labelPrefix) && current.Contains(labelPrefix, StringComparison.OrdinalIgnoreCase))
            {
                int colonIdx = current.IndexOf(':');
                if (colonIdx >= 0 && colonIdx < current.Length - 1)
                {
                    string after = current[(colonIdx + 1)..].Trim();
                    if (!string.IsNullOrEmpty(after)) return after;
                }
            }

            // Kiểm tra các cột tiếp theo cùng hàng
            for (int offset = 1; offset <= 6; offset++)
            {
                string next = GetCell(r, c + offset);
                if (!string.IsNullOrEmpty(next)) return next;
            }

            // Kiểm tra hàng tiếp theo cùng cột
            string below = GetCell(r + 1, c);
            if (!string.IsNullOrEmpty(below)) return below;

            return string.Empty;
        }

        // 1. Quét toàn bộ bảng để lấy các trường thông tin Header pháp lý của tờ khai
        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < colCount; c++)
            {
                string cell = GetCell(r, c);
                if (string.IsNullOrWhiteSpace(cell)) continue;

                // Số tờ khai (11 hoặc 12 chữ số)
                if (string.IsNullOrEmpty(result.DeclarationNo) &&
                    (cell.Contains("Số tờ khai", StringComparison.OrdinalIgnoreCase) || cell.Contains("Số TK", StringComparison.OrdinalIgnoreCase)))
                {
                    string candidate = FindValueNear(r, c, "Số tờ khai");
                    var match = Regex.Match(candidate, @"\b(\d{11,12})\b");
                    if (match.Success)
                    {
                        result.DeclarationNo = match.Groups[1].Value;
                    }
                    else
                    {
                        var cellMatch = Regex.Match(cell, @"\b(\d{11,12})\b");
                        if (cellMatch.Success) result.DeclarationNo = cellMatch.Groups[1].Value;
                    }
                }

                // Số hóa đơn (Invoice No)
                if (string.IsNullOrEmpty(result.InvoiceNo) &&
                    (cell.Contains("Số hóa đơn", StringComparison.OrdinalIgnoreCase) || cell.Contains("Số hoá đơn", StringComparison.OrdinalIgnoreCase)))
                {
                    string candidate = FindValueNear(r, c, "Số hóa đơn");
                    var match = Regex.Match(candidate, @"(KMHD-[A-Za-z0-9-]+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        result.InvoiceNo = match.Groups[1].Value;
                    }
                    else if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        result.InvoiceNo = candidate.Split(' ', '\r', '\n', '\t')[0].Trim();
                    }
                }

                // Mã loại hình (vd: E52, B11)
                if (string.IsNullOrEmpty(result.CustomsDeclarationType) &&
                    cell.Contains("Mã loại hình", StringComparison.OrdinalIgnoreCase))
                {
                    string candidate = FindValueNear(r, c, "Mã loại hình");
                    var match = Regex.Match(candidate, @"\b([A-Z]\d{2})\b");
                    if (match.Success) result.CustomsDeclarationType = match.Groups[1].Value;
                    else if (!string.IsNullOrWhiteSpace(candidate)) result.CustomsDeclarationType = candidate.Substring(0, Math.Min(5, candidate.Length)).Trim();
                }

                // Phân loại kiểm tra (Luồng: 1 = Xanh, 2 = Vàng, 3 = Đỏ)
                if (!result.CustomsChannel.HasValue &&
                    (cell.Contains("phân loại kiểm tra", StringComparison.OrdinalIgnoreCase) || cell.Contains("Mã phân loại", StringComparison.OrdinalIgnoreCase)))
                {
                    string candidate = FindValueNear(r, c);
                    var match = Regex.Match(candidate, @"\b([123])\b");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int ch))
                    {
                        result.CustomsChannel = ch;
                    }
                    else if (candidate.Contains("xanh", StringComparison.OrdinalIgnoreCase)) result.CustomsChannel = 1;
                    else if (candidate.Contains("vàng", StringComparison.OrdinalIgnoreCase)) result.CustomsChannel = 2;
                    else if (candidate.Contains("đỏ", StringComparison.OrdinalIgnoreCase)) result.CustomsChannel = 3;
                }

                // Ngày đăng ký / thông quan
                if (!result.ClearanceDate.HasValue &&
                    (cell.Contains("Ngày đăng ký", StringComparison.OrdinalIgnoreCase) || cell.Contains("Ngày thông quan", StringComparison.OrdinalIgnoreCase)))
                {
                    string candidate = FindValueNear(r, c, "Ngày đăng ký");
                    if (TryParseCustomsDate(candidate, out var dt))
                    {
                        result.ClearanceDate = dt;
                    }
                    else if (TryParseCustomsDate(cell, out var cellDt))
                    {
                        result.ClearanceDate = cellDt;
                    }
                }

                // Số lượng kiện hàng (Package Qty kèm "PK")
                if (result.PackageQty == 0 &&
                    (cell.Contains("Số lượng kiện", StringComparison.OrdinalIgnoreCase) || (cell.Contains("Số lượng", StringComparison.OrdinalIgnoreCase) && cell.Contains("PK", StringComparison.OrdinalIgnoreCase))))
                {
                    var match = Regex.Match(cell, @"(\d+)\s*PK", RegexOptions.IgnoreCase);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int qty))
                    {
                        result.PackageQty = qty;
                    }
                    else
                    {
                        string candidate = FindValueNear(r, c);
                        var matchCand = Regex.Match(candidate, @"(\d+)\s*(?:PK)?", RegexOptions.IgnoreCase);
                        if (matchCand.Success && int.TryParse(matchCand.Groups[1].Value, out int candQty))
                        {
                            result.PackageQty = candQty;
                        }
                    }
                }

                // Tổng trọng lượng Gross (Gross weight)
                if (result.GrossWeight == 0 &&
                    (cell.Contains("trọng lượng hàng (Gross)", StringComparison.OrdinalIgnoreCase) || cell.Contains("Gross weight", StringComparison.OrdinalIgnoreCase)))
                {
                    string candidate = FindValueNear(r, c);
                    var match = Regex.Match(candidate, @"([0-9,.]+)\s*(?:KGM|KG)?", RegexOptions.IgnoreCase);
                    if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal gw))
                    {
                        result.GrossWeight = gw;
                    }
                }

                // Tổng trị giá hóa đơn (DAP)
                if (result.TotalDap == 0 &&
                    cell.Contains("Tổng trị giá hóa đơn", StringComparison.OrdinalIgnoreCase))
                {
                    string candidate = FindValueNear(r, c);
                    var match = Regex.Match(candidate, @"([0-9,.]+)", RegexOptions.IgnoreCase);
                    if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal dap))
                    {
                        result.TotalDap = dap;
                    }
                }

                // Tổng trị giá gia công (CMT) nằm trong "Ký hiệu và số hiệu" hoặc ô ghi chú
                if (result.TotalCmt == 0 &&
                    (cell.Contains("GIA CONG", StringComparison.OrdinalIgnoreCase) || cell.Contains("GIA CÔNG", StringComparison.OrdinalIgnoreCase)))
                {
                    var match = Regex.Match(cell, @"(?:TONG|TỔNG)?\s*(?:TRI\s*GIA|TRỊ\s*GIÁ|TIEN|TIỀN)?\s*GIA\s*C[OÔ]NG\s*[:=]?\s*([0-9,.]+)", RegexOptions.IgnoreCase);
                    if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cmt))
                    {
                        result.TotalCmt = cmt;
                    }
                }

                // Chi cục hải quan tiếp nhận
                if (string.IsNullOrEmpty(result.CustomsOffice) &&
                    (cell.Contains("tiếp nhận tờ khai", StringComparison.OrdinalIgnoreCase) || cell.Contains("Chi cục Hải quan", StringComparison.OrdinalIgnoreCase)))
                {
                    string candidate = FindValueNear(r, c);
                    result.CustomsOffice = candidate;
                }
            }
        }

        // 2. Quét bóc tách danh sách các dòng hàng (bắt đầu bằng <01>, <02>, <03>,...)
        var items = new List<CustomsDeclarationItemDto>();
        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < colCount; c++)
            {
                string cell = GetCell(r, c);
                var lineMatch = Regex.Match(cell, @"^<0*(\d+)>$");
                if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out int lineNum))
                {
                    var item = ParseItemBlock(table, r, c, lineNum, rowCount, colCount);
                    if (item != null && !string.IsNullOrEmpty(item.StyleCode))
                    {
                        items.Add(item);
                    }
                }
            }
        }

        result.Items = items;

        // Nếu TotalDap hoặc TotalCmt chưa lấy được từ header, tính tổng từ các dòng hàng
        if (result.TotalDap == 0 && items.Count > 0)
        {
            result.TotalDap = items.Sum(i => i.AmountDap > 0 ? i.AmountDap : (i.Quantity * i.UnitPriceDap));
        }

        if (result.TotalCmt == 0 && items.Count > 0)
        {
            result.TotalCmt = items.Sum(i => i.Quantity * i.UnitPriceCmt);
        }

        return result;
    }

    /// <summary>
    /// Bóc tách chi tiết 1 block dòng hàng xuất phát từ ô chứa <01>, <02>...
    /// </summary>
    private CustomsDeclarationItemDto? ParseItemBlock(DataTable table, int startRow, int startCol, int lineNum, int totalRows, int totalCols)
    {
        string GetCell(int r, int c)
        {
            if (r < 0 || r >= totalRows || c < 0 || c >= totalCols) return string.Empty;
            var val = table.Rows[r][c];
            return val == null || val == DBNull.Value ? string.Empty : val.ToString()?.Trim() ?? string.Empty;
        }

        var item = new CustomsDeclarationItemDto
        {
            LineNumber = lineNum
        };

        // Quét trong phạm vi 4 hàng và 15 cột xung quanh startRow để lấy các giá trị
        int maxR = Math.Min(totalRows - 1, startRow + 3);

        string allBlockText = "";
        for (int r = startRow; r <= maxR; r++)
        {
            for (int c = startCol; c < totalCols; c++)
            {
                string val = GetCell(r, c);
                if (string.IsNullOrEmpty(val)) continue;

                allBlockText += " | " + val;

                // 1. Mã HS Code (8 số, thường bắt đầu bằng 64 cho giày dép)
                if (string.IsNullOrEmpty(item.HsCode))
                {
                    var hsMatch = Regex.Match(val, @"\b(640[0-9]{5})\b");
                    if (hsMatch.Success)
                    {
                        item.HsCode = hsMatch.Groups[1].Value;
                    }
                }

                // 2. Mô tả hàng hóa chứa format: StyleCode (PO)#&Giày... (Đơn giá gia công: X USD/đôi)
                if (string.IsNullOrEmpty(item.StyleCode) &&
                    (val.Contains("#&", StringComparison.Ordinal) || val.Contains("Đơn giá gia công", StringComparison.OrdinalIgnoreCase) || val.Contains("KM3.PO", StringComparison.OrdinalIgnoreCase)))
                {
                    item.RawDescription = val;
                    ExtractStyleCodeAndCmt(val, item);
                }

                // 3. Đơn vị tính
                if (val.Equals("PRS", StringComparison.OrdinalIgnoreCase) ||
                    val.Equals("PCE", StringComparison.OrdinalIgnoreCase) ||
                    val.Equals("đôi", StringComparison.OrdinalIgnoreCase))
                {
                    item.Unit = val;
                }
            }
        }

        // Quét số lượng, đơn giá DAP, trị giá DAP
        for (int r = startRow; r <= maxR; r++)
        {
            for (int c = startCol; c < totalCols; c++)
            {
                string val = GetCell(r, c);
                if (string.IsNullOrEmpty(val)) continue;

                // Nếu chưa tách được mô tả thì thử tìm lại
                if (string.IsNullOrEmpty(item.StyleCode) && val.Length >= 5)
                {
                    var styleMatch = Regex.Match(val, @"^([A-Z0-9]{4,6}-[A-Z0-9]{2,4}(?:\.G)?)", RegexOptions.IgnoreCase);
                    if (styleMatch.Success)
                    {
                        item.RawDescription = val;
                        ExtractStyleCodeAndCmt(val, item);
                    }
                }

                // Số lượng
                if (item.Quantity == 0 && int.TryParse(val.Replace(",", "").Replace(".", ""), out int q) && q > 0 && q < 1000000)
                {
                    // Đảm bảo không nhầm với HS Code
                    if (val.Length != 8 && val.Length != 10 && val.Length != 12)
                    {
                        // Kiểm tra xem hàng/cột có nhãn số lượng không
                        item.Quantity = q;
                    }
                }

                // Đơn giá DAP
                if (item.UnitPriceDap == 0 && decimal.TryParse(val.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) && price > 0.5m && price < 500m)
                {
                    if (price != item.Quantity && price != item.AmountDap)
                    {
                        item.UnitPriceDap = price;
                    }
                }

                // Trị giá DAP (Amount)
                if (item.AmountDap == 0 && decimal.TryParse(val.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amt) && amt >= 10m)
                {
                    if (amt != item.Quantity && amt != item.UnitPriceDap)
                    {
                        item.AmountDap = amt;
                    }
                }
            }
        }

        // Tự động suy luận: nếu có AmountDap và Quantity thì tính ra UnitPriceDap nếu thiếu
        if (item.UnitPriceDap == 0 && item.Quantity > 0 && item.AmountDap > 0)
        {
            item.UnitPriceDap = Math.Round(item.AmountDap / item.Quantity, 4);
        }
        else if (item.AmountDap == 0 && item.Quantity > 0 && item.UnitPriceDap > 0)
        {
            item.AmountDap = Math.Round(item.Quantity * item.UnitPriceDap, 2);
        }

        return item;
    }

    private void ExtractStyleCodeAndCmt(string text, CustomsDeclarationItemDto item)
    {
        // 1. Tách StyleCode:
        // Format: "40700-007 (KM3.PO5.26)#&Giày..." hoặc "45428-2LX.G #&Giày..."
        var matchStyle = Regex.Match(text, @"^([A-Za-z0-9_.-]+)");
        if (matchStyle.Success)
        {
            string code = matchStyle.Groups[1].Value.Trim();
            if (code.EndsWith(".G", StringComparison.OrdinalIgnoreCase))
            {
                item.StyleCode = code[..^2].Trim();
                item.ProcessType = ProcessType.GoKhongMay;
            }
            else
            {
                item.StyleCode = code;
                item.ProcessType = ProcessType.Standard;
            }
        }

        // 2. Tách Đơn giá CMT:
        // Format: "(Đơn giá gia công: 3.00 USD/đôi)" hoặc "Don gia gia cong: 3.50 USD"
        var matchCmt = Regex.Match(text, @"(?:gia|giá)?\s*c[oô]ng\s*[:=]?\s*([0-9,.]+)", RegexOptions.IgnoreCase);
        if (matchCmt.Success && decimal.TryParse(matchCmt.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cmt))
        {
            item.UnitPriceCmt = cmt;
        }
    }

    private static bool TryParseCustomsDate(string input, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var formats = new[]
        {
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "d/M/yyyy H:m:s",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd"
        };

        // Trích xuất chuỗi có định dạng ngày tháng
        var dateMatch = Regex.Match(input, @"\b(\d{1,2}/\d{1,2}/\d{4}(?:\s+\d{1,2}:\d{1,2}(?::\d{1,2})?)?)\b");
        if (dateMatch.Success)
        {
            string clean = dateMatch.Groups[1].Value;
            if (DateTime.TryParseExact(clean, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }
        }

        return DateTime.TryParse(input, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out date);
    }

    /// <summary>
    /// Đối soát chéo 2 chiều giữa dữ liệu tờ khai hải quan và đơn hàng trong CSDL
    /// </summary>
    public async Task<CustomsReconciliationResultDto> ReconcileAsync(CustomsDeclarationParsedDto declaration, int? specificOrderId = null)
    {
        var result = new CustomsReconciliationResultDto
        {
            Declaration = declaration
        };

        // Tìm đơn hàng tương ứng trong CSDL
        ShipmentOrder? order = null;
        if (specificOrderId.HasValue && specificOrderId.Value > 0)
        {
            order = await _context.ShipmentOrders
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == specificOrderId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(declaration.InvoiceNo))
        {
            string searchInvoice = declaration.InvoiceNo.Trim().ToUpperInvariant();
            order = await _context.ShipmentOrders
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.InvoiceNo.ToUpper() == searchInvoice);
        }

        if (order == null)
        {
            result.IsOrderFound = false;
            result.IsFullyMatched = false;
            result.Message = $"Không tìm thấy đơn hàng tương ứng với Số hóa đơn '{declaration.InvoiceNo}' trong cơ sở dữ liệu. Vui lòng kiểm tra lại số hóa đơn hoặc chọn đơn hàng thủ công.";
            return result;
        }

        result.IsOrderFound = true;
        result.MatchedOrder = new MatchedOrderSummaryDto
        {
            Id = order.Id,
            InvoiceNo = order.InvoiceNo,
            InvoiceDate = order.InvoiceDate,
            PoSuffix = order.PoSuffix,
            ContractNo = order.ContractNo,
            CustomerName = order.CustomerName,
            TotalQuantity = order.Items.Sum(i => i.Quantity),
            TotalAmountDap = order.Items.Sum(i => i.Quantity * i.UnitPriceDAP),
            TotalAmountCmt = order.Items.Sum(i => i.Quantity * i.UnitPriceCMT),
            CurrentStatus = order.Status
        };

        // Gom nhóm các mặt hàng của đơn hàng theo StyleCode (chuẩn hóa không phân biệt hoa thường)
        var invoiceItemsGrouped = order.Items
            .GroupBy(i => NormalizeStyleCode(i.StyleCode))
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    StyleCode = g.First().StyleCode,
                    ProcessType = g.First().ProcessType,
                    Quantity = g.Sum(x => x.Quantity),
                    PriceDap = g.First().UnitPriceDAP,
                    PriceCmt = g.First().UnitPriceCMT
                }
            );

        // Gom nhóm các mặt hàng trên tờ khai
        var customsItemsGrouped = declaration.Items
            .GroupBy(i => NormalizeStyleCode(i.StyleCode))
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    StyleCode = g.First().StyleCode,
                    ProcessType = g.First().ProcessType,
                    Quantity = g.Sum(x => x.Quantity),
                    PriceDap = g.First().UnitPriceDap,
                    PriceCmt = g.First().UnitPriceCmt
                }
            );

        // Tập hợp tất cả các StyleCode có mặt ở cả 2 bên
        var allStyleCodes = invoiceItemsGrouped.Keys
            .Union(customsItemsGrouped.Keys)
            .OrderBy(k => k)
            .ToList();

        var comparisonRows = new List<CustomsComparisonRowDto>();
        var discrepancies = new List<string>();
        int idx = 1;

        foreach (var key in allStyleCodes)
        {
            bool hasInvoice = invoiceItemsGrouped.TryGetValue(key, out var invItem);
            bool hasCustoms = customsItemsGrouped.TryGetValue(key, out var cusItem);

            string styleCodeDisplay = (hasCustoms ? cusItem?.StyleCode : invItem?.StyleCode) ?? key;
            int invQty = hasInvoice && invItem != null ? invItem.Quantity : 0;
            int cusQty = hasCustoms && cusItem != null ? cusItem.Quantity : 0;
            decimal invDap = hasInvoice && invItem != null ? invItem.PriceDap : 0m;
            decimal cusDap = hasCustoms && cusItem != null ? cusItem.PriceDap : 0m;
            decimal invCmt = hasInvoice && invItem != null ? invItem.PriceCmt : 0m;
            decimal cusCmt = hasCustoms && cusItem != null ? cusItem.PriceCmt : 0m;

            var row = new CustomsComparisonRowDto
            {
                Index = idx++,
                StyleCode = styleCodeDisplay,
                ProcessType = (hasCustoms ? cusItem?.ProcessType : invItem?.ProcessType) ?? ProcessType.Standard,
                InvoiceQuantity = invQty,
                CustomsQuantity = cusQty,
                InvoicePriceDap = invDap,
                CustomsPriceDap = cusDap,
                InvoicePriceCmt = invCmt,
                CustomsPriceCmt = cusCmt
            };

            if (!hasInvoice)
            {
                row.StatusText = "Thừa trên tờ khai (không có trong Invoice)";
                discrepancies.Add($"Mã {styleCodeDisplay}: Có trên tờ khai ({cusQty} đôi) nhưng không có trong Invoice nội bộ.");
            }
            else if (!hasCustoms)
            {
                row.StatusText = "Thiếu trên tờ khai (chưa khai báo hải quan)";
                discrepancies.Add($"Mã {styleCodeDisplay}: Có trong Invoice ({invQty} đôi) nhưng không tìm thấy trên tờ khai hải quan.");
            }
            else
            {
                var issues = new List<string>();
                if (row.DifferenceQuantity != 0)
                {
                    issues.Add($"Lệch {(row.DifferenceQuantity > 0 ? "+" : "")}{row.DifferenceQuantity} đôi");
                    discrepancies.Add($"Mã {styleCodeDisplay}: Sai lệch số lượng (Tờ khai: {cusQty} đôi, Invoice: {invQty} đôi).");
                }

                if (!row.IsPriceMatched)
                {
                    issues.Add($"Lệch giá DAP (${cusDap:F2} vs ${invDap:F2})");
                    discrepancies.Add($"Mã {styleCodeDisplay}: Lệch đơn giá DAP (Tờ khai: ${cusDap:F2}, Invoice: ${invDap:F2}).");
                }

                row.StatusText = issues.Count == 0 ? "Khớp 100%" : string.Join(" • ", issues);
            }

            comparisonRows.Add(row);
        }

        result.ComparisonRows = comparisonRows;

        // So sánh tổng quát
        int invoiceTotalQty = order.Items.Sum(i => i.Quantity);
        int customsTotalQty = declaration.TotalItemQuantity > 0 ? declaration.TotalItemQuantity : declaration.Items.Sum(i => i.Quantity);
        result.TotalQuantityMatched = invoiceTotalQty == customsTotalQty;

        decimal invoiceTotalDap = order.Items.Sum(i => i.Quantity * i.UnitPriceDAP);
        decimal customsTotalDap = declaration.TotalDap > 0 ? declaration.TotalDap : declaration.Items.Sum(i => i.Quantity * i.UnitPriceDap);
        result.TotalDapMatched = Math.Abs(invoiceTotalDap - customsTotalDap) <= 0.05m;

        decimal invoiceTotalCmt = order.Items.Sum(i => i.Quantity * i.UnitPriceCMT);
        decimal customsTotalCmt = declaration.TotalCmt > 0 ? declaration.TotalCmt : declaration.Items.Sum(i => i.Quantity * i.UnitPriceCmt);
        result.TotalCmtMatched = customsTotalCmt == 0 || Math.Abs(invoiceTotalCmt - customsTotalCmt) <= 0.05m;

        // Đơn hàng được coi là khớp 100% khi:
        // Tất cả các dòng so sánh đều khớp (không có dòng thiếu/thừa/lệch) và tổng số lượng khớp
        result.IsFullyMatched = comparisonRows.All(r => r.IsMatched) && result.TotalQuantityMatched && result.TotalDapMatched;
        result.Discrepancies = discrepancies;

        if (result.IsFullyMatched)
        {
            result.Message = $"✔ Đối soát hoàn tất: Dữ liệu Tờ khai hải quan và Hóa đơn {order.InvoiceNo} khớp 100% ({invoiceTotalQty:N0} đôi, {comparisonRows.Count} dòng hàng).";
        }
        else
        {
            result.Message = $"⚠ Cảnh báo sai lệch: Phát hiện {discrepancies.Count} điểm không trùng khớp giữa Tờ khai và Hóa đơn {order.InvoiceNo}. Vui lòng kiểm tra chi tiết bảng đối soát.";
        }

        return result;
    }

    private static string NormalizeStyleCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        string clean = code.Trim().ToUpperInvariant();
        if (clean.EndsWith(".G"))
        {
            clean = clean[..^2].Trim();
        }
        return clean;
    }

    /// <summary>
    /// Xác nhận đồng bộ thông tin hải quan vào đơn hàng và lưu trữ file tờ khai
    /// </summary>
    public async Task<ShipmentOrder> ConfirmSyncAsync(
        int orderId,
        ConfirmCustomsSyncRequestDto request,
        Stream? fileStream = null,
        string? originalFileName = null)
    {
        var order = await _context.ShipmentOrders
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == orderId)
            ?? throw new KeyNotFoundException($"Không tìm thấy đơn hàng #{orderId} trong cơ sở dữ liệu.");

        // Lưu file đính kèm vào thư mục data/customs nếu có
        string? savedFileName = null;
        if (fileStream != null && !string.IsNullOrWhiteSpace(originalFileName))
        {
            string storageDir = Path.Combine(_environment.ContentRootPath, "data", "customs");
            if (!Directory.Exists(storageDir))
            {
                Directory.CreateDirectory(storageDir);
            }

            string safeOriginal = Path.GetFileName(originalFileName).Replace(" ", "_");
            savedFileName = $"TK_{request.DeclarationNo}_{orderId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{safeOriginal}";
            string fullPath = Path.Combine(storageDir, savedFileName);

            using var targetStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(targetStream);
        }

        // Cập nhật thông tin hải quan vào ShipmentOrder
        order.DeclarationNo = request.DeclarationNo;
        order.ClearanceDate = request.ClearanceDate ?? DateTime.UtcNow;
        order.CustomsDeclarationType = request.CustomsDeclarationType;
        order.CustomsChannel = request.CustomsChannel;
        order.CustomsOffice = request.CustomsOffice;
        order.CustomsPackageQty = request.CustomsPackageQty;
        order.CustomsGrossWeight = request.CustomsGrossWeight;
        order.CustomsTotalDap = request.CustomsTotalDap;
        order.CustomsTotalCmt = request.CustomsTotalCmt;

        if (!string.IsNullOrEmpty(savedFileName))
        {
            order.CustomsAttachmentFileName = savedFileName;
        }

        // Cập nhật trạng thái
        order.Status = request.IsFullyMatched
            ? ShipmentStatus.Cleared
            : ShipmentStatus.Discrepancy;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Đã đồng bộ thông tin hải quan cho đơn hàng #{OrderId} (Status: {Status})", orderId, order.Status);

        return order;
    }

    /// <summary>
    /// Lấy file tờ khai đã đính kèm theo đơn hàng
    /// </summary>
    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetAttachmentAsync(int orderId)
    {
        var order = await _context.ShipmentOrders.FindAsync(orderId);
        if (order == null || string.IsNullOrEmpty(order.CustomsAttachmentFileName))
        {
            return null;
        }

        string filePath = Path.Combine(_environment.ContentRootPath, "data", "customs", order.CustomsAttachmentFileName);
        if (!File.Exists(filePath))
        {
            return null;
        }

        byte[] bytes = await File.ReadAllBytesAsync(filePath);
        string contentType = order.CustomsAttachmentFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "application/vnd.ms-excel";

        return (bytes, contentType, order.CustomsAttachmentFileName);
    }
}
