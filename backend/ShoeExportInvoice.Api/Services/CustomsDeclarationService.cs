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
                    // 1. Kiểm tra ngay trong cell nếu có chứa số hóa đơn sau dấu ':'
                    var cellMatch = Regex.Match(cell, @"(KMHD-[A-Za-z0-9-]+)", RegexOptions.IgnoreCase);
                    if (cellMatch.Success)
                    {
                        result.InvoiceNo = cellMatch.Groups[1].Value;
                    }
                    else
                    {
                        // 2. Quét các cột tiếp theo trên cùng hàng (tối đa 15 cột) để tìm số hóa đơn thực tế
                        // Bỏ qua mã phân loại đơn lẻ của VNACCS như "A", "B" hoặc dấu "-"
                        string foundInvoice = "";
                        for (int offset = 1; offset <= 15 && c + offset < colCount; offset++)
                        {
                            string cellVal = GetCell(r, c + offset);
                            if (string.IsNullOrWhiteSpace(cellVal) || cellVal == "-" || cellVal.Length <= 2)
                                continue;

                            // Nhận diện mã hóa đơn hợp lệ (chứa KMHD hoặc DH hoặc có độ dài thực tế >= 5)
                            var m = Regex.Match(cellVal, @"(KMHD-[A-Za-z0-9-]+)", RegexOptions.IgnoreCase);
                            if (m.Success)
                            {
                                foundInvoice = m.Groups[1].Value;
                                break;
                            }

                            if (string.IsNullOrEmpty(foundInvoice) && (cellVal.Contains("KMHD") || cellVal.Contains("DH") || cellVal.Length >= 5))
                            {
                                foundInvoice = cellVal.Split(' ', '\r', '\n', '\t')[0].Trim();
                            }
                        }

                        if (!string.IsNullOrEmpty(foundInvoice))
                        {
                            result.InvoiceNo = foundInvoice;
                        }
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
                    if (match.Success && TryParseCustomsDecimal(match.Groups[1].Value, out decimal gw))
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
                    if (match.Success && TryParseCustomsDecimal(match.Groups[1].Value, out decimal dap))
                    {
                        result.TotalDap = dap;
                    }
                }

                // Tổng trị giá gia công (CMT) nằm trong "Ký hiệu và số hiệu" hoặc ô ghi chú
                if (result.TotalCmt == 0 &&
                    (cell.Contains("GIA CONG", StringComparison.OrdinalIgnoreCase) || cell.Contains("GIA CÔNG", StringComparison.OrdinalIgnoreCase)))
                {
                    var match = Regex.Match(cell, @"(?:TONG|TỔNG)?\s*(?:TRI\s*GIA|TRỊ\s*GIÁ|TIEN|TIỀN)?\s*GIA\s*C[OÔ]NG\s*[:=]?\s*([0-9,.]+)", RegexOptions.IgnoreCase);
                    if (match.Success && TryParseCustomsDecimal(match.Groups[1].Value, out decimal cmt))
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

        // Fallback: nếu chưa lấy được InvoiceNo từ ô nhãn, quét tìm bất kỳ ô nào chứa pattern KMHD-...
        if (string.IsNullOrEmpty(result.InvoiceNo))
        {
            for (int r = 0; r < Math.Min(rowCount, 100); r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    string cell = GetCell(r, c);
                    var m = Regex.Match(cell, @"\b(KMHD-[A-Za-z0-9-]+)\b", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        result.InvoiceNo = m.Groups[1].Value;
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(result.InvoiceNo)) break;
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

        // Giới hạn phạm vi quét cho block dòng hàng hiện tại (quét tối đa 15 dòng cho đến khi gặp dòng hàng tiếp theo <02>, <03>,...)
        int maxR = totalRows - 1;
        for (int r = startRow + 1; r < totalRows; r++)
        {
            bool isNextItem = false;
            for (int c = 0; c < Math.Min(totalCols, 5); c++)
            {
                if (Regex.IsMatch(GetCell(r, c), @"^<0*(\d+)>$"))
                {
                    isNextItem = true;
                    break;
                }
            }
            if (isNextItem ||
                GetCell(r, 0).Contains("Tổng", StringComparison.OrdinalIgnoreCase) ||
                GetCell(r, 1).Contains("Tổng", StringComparison.OrdinalIgnoreCase) ||
                r >= startRow + 15)
            {
                maxR = r - 1;
                break;
            }
        }

        // Tập hợp các số ứng viên trong block để suy luận chính xác bộ ba (Số lượng, Đơn giá DAP, Trị giá DAP)
        var numericCandidates = new List<decimal>();

        for (int r = startRow; r <= maxR; r++)
        {
            int fromCol = (r == startRow) ? startCol : 0;
            for (int c = fromCol; c < totalCols; c++)
            {
                string val = GetCell(r, c);
                if (string.IsNullOrEmpty(val)) continue;

                // 1. Mã HS Code (8 số, thường bắt đầu bằng 64 cho giày dép)
                if (string.IsNullOrEmpty(item.HsCode))
                {
                    var hsMatch = Regex.Match(val, @"\b(640[0-9]{5})\b");
                    if (hsMatch.Success)
                    {
                        item.HsCode = hsMatch.Groups[1].Value;
                    }
                }

                // 2. Mô tả hàng hóa: kiểm tra nhãn "Mô tả hàng" hoặc ô chứa ký tự phân tách #& hoặc mã StyleCode
                if (string.IsNullOrEmpty(item.StyleCode))
                {
                    if (val.Contains("Mô tả hàng", StringComparison.OrdinalIgnoreCase))
                    {
                        // Quét các ô tiếp theo cùng hàng (Cột 5 trở đi)
                        for (int offset = 1; offset <= 10 && c + offset < totalCols; offset++)
                        {
                            string descCand = GetCell(r, c + offset);
                            if (!string.IsNullOrWhiteSpace(descCand) &&
                                (descCand.Contains("#&") || Regex.IsMatch(descCand, @"\b([A-Za-z0-9]{4,6}-[A-Za-z0-9]{2,4})")))
                            {
                                item.RawDescription = descCand;
                                ExtractStyleCodeAndCmt(descCand, item);
                                break;
                            }
                        }

                        // Nếu chưa thấy, thử ô ở hàng ngay phía dưới
                        if (string.IsNullOrEmpty(item.StyleCode) && r + 1 <= maxR)
                        {
                            for (int offset = 0; offset <= 8 && c + offset < totalCols; offset++)
                            {
                                string belowCand = GetCell(r + 1, c + offset);
                                if (!string.IsNullOrWhiteSpace(belowCand) &&
                                    (belowCand.Contains("#&") || Regex.IsMatch(belowCand, @"\b([A-Za-z0-9]{4,6}-[A-Za-z0-9]{2,4})")))
                                {
                                    item.RawDescription = belowCand;
                                    ExtractStyleCodeAndCmt(belowCand, item);
                                    break;
                                }
                            }
                        }
                    }
                    else if (val.Contains("#&", StringComparison.Ordinal) ||
                             val.Contains("Đơn giá gia công", StringComparison.OrdinalIgnoreCase) ||
                             val.Contains("KM3.PO", StringComparison.OrdinalIgnoreCase) ||
                             Regex.IsMatch(val, @"\b([A-Za-z0-9]{4,6}-[A-Za-z0-9]{2,4}(?:\.G)?)\b", RegexOptions.IgnoreCase))
                    {
                        if (!val.Contains("Số tờ khai", StringComparison.OrdinalIgnoreCase) &&
                            !val.Contains("Số hóa đơn", StringComparison.OrdinalIgnoreCase))
                        {
                            item.RawDescription = val;
                            ExtractStyleCodeAndCmt(val, item);
                        }
                    }
                }

                // 3. Đơn vị tính
                if (val.Equals("PRS", StringComparison.OrdinalIgnoreCase) ||
                    val.Equals("PCE", StringComparison.OrdinalIgnoreCase) ||
                    val.Equals("đôi", StringComparison.OrdinalIgnoreCase))
                {
                    item.Unit = val;
                }

                // 4. Bóc tách trực tiếp theo nhãn "Số lượng (1)" (Cột 16 trong bảng VNACCS)
                if (item.Quantity == 0 &&
                    (val.Contains("Số lượng (1)", StringComparison.OrdinalIgnoreCase) ||
                     val.Contains("Số lượng(1)", StringComparison.OrdinalIgnoreCase) ||
                     (val.Trim().Equals("Số lượng", StringComparison.OrdinalIgnoreCase) && c > 0 && !GetCell(r, c - 1).Contains("kiện", StringComparison.OrdinalIgnoreCase))))
                {
                    for (int offset = 1; offset <= 12 && c + offset < totalCols; offset++)
                    {
                        string qVal = GetCell(r, c + offset);
                        if (TryParseCustomsQuantity(qVal, out int q) && q > 0)
                        {
                            item.Quantity = q;
                            break;
                        }
                    }
                }

                // 5. Bóc tách trực tiếp theo nhãn "Đơn giá hóa đơn" (Cột 17 trong bảng VNACCS, loại trừ "Đơn giá gia công")
                if (item.UnitPriceDap == 0 &&
                    !val.Contains("gia công", StringComparison.OrdinalIgnoreCase) &&
                    !val.Contains("tính thuế", StringComparison.OrdinalIgnoreCase) &&
                    (val.Contains("Đơn giá hóa đơn", StringComparison.OrdinalIgnoreCase) ||
                     val.Contains("Đơn giá hoá đơn", StringComparison.OrdinalIgnoreCase) ||
                     val.Trim().Equals("Đơn giá", StringComparison.OrdinalIgnoreCase)))
                {
                    for (int offset = 1; offset <= 12 && c + offset < totalCols; offset++)
                    {
                        string pVal = GetCell(r, c + offset);
                        if (TryParseCustomsDecimal(pVal, out decimal p) && p >= 0.1m && p <= 500m)
                        {
                            item.UnitPriceDap = p;
                            break;
                        }
                    }
                }

                // 6. Bóc tách trực tiếp theo nhãn "Trị giá hóa đơn" (loại trừ "Trị giá tính thuế" và "Trị giá gia công")
                if (item.AmountDap == 0 &&
                    !val.Contains("gia công", StringComparison.OrdinalIgnoreCase) &&
                    !val.Contains("tính thuế", StringComparison.OrdinalIgnoreCase) &&
                    (val.Contains("Trị giá hóa đơn", StringComparison.OrdinalIgnoreCase) ||
                     val.Contains("Trị giá hoá đơn", StringComparison.OrdinalIgnoreCase) ||
                     val.Trim().Equals("Trị giá", StringComparison.OrdinalIgnoreCase)))
                {
                    for (int offset = 1; offset <= 12 && c + offset < totalCols; offset++)
                    {
                        string aVal = GetCell(r, c + offset);
                        if (TryParseCustomsDecimal(aVal, out decimal a) && a >= 1m)
                        {
                            item.AmountDap = a;
                            break;
                        }
                    }
                }

                // Thu thập các số ứng viên cho thuật toán suy luận / đối soát trio
                if (!Regex.IsMatch(val, @"^<0*\d+>$") &&
                    val != item.HsCode &&
                    TryParseCustomsDecimal(val, out decimal num) &&
                    num > 0 &&
                    num < 100_000_000m)
                {
                    numericCandidates.Add(num);
                }
            }
        }

        // Tự động đối soát và khớp bộ ba toán học: Quantity * UnitPriceDap ≈ AmountDap nếu chưa có đủ
        if (item.Quantity == 0 || item.UnitPriceDap == 0 || item.AmountDap == 0)
        {
            bool trioFound = false;
            for (int i = 0; i < numericCandidates.Count; i++)
            {
                decimal n1 = numericCandidates[i];
                for (int j = 0; j < numericCandidates.Count; j++)
                {
                    if (i == j) continue;
                    decimal n2 = numericCandidates[j];

                    // Giả định n1 là Quantity (nguyên dương), n2 là UnitPrice (0.1 đến 500)
                    if (n1 == Math.Floor(n1) && n1 >= 1 && n1 <= 1_000_000 && n2 >= 0.1m && n2 <= 500m)
                    {
                        for (int k = 0; k < numericCandidates.Count; k++)
                        {
                            if (k == i || k == j) continue;
                            decimal n3 = numericCandidates[k];

                            // Kiểm tra n1 * n2 ≈ n3
                            if (Math.Abs((n1 * n2) - n3) <= Math.Max(0.1m, n3 * 0.01m))
                            {
                                item.Quantity = (int)n1;
                                item.UnitPriceDap = n2;
                                item.AmountDap = n3;
                                trioFound = true;
                                break;
                            }
                        }
                    }
                    if (trioFound) break;
                }
                if (trioFound) break;
            }

            // Nếu không tìm được trọn bộ 3 số theo quan hệ nhân, suy luận từng số theo biên độ giá trị
            if (!trioFound)
            {
                // Số lượng: số nguyên và không nằm trong dải đơn giá thông thường
                if (item.Quantity == 0)
                {
                    var qCand = numericCandidates.FirstOrDefault(n => n == Math.Floor(n) && n >= 1 && (n > 500m || n != item.UnitPriceDap));
                    if (qCand > 0) item.Quantity = (int)qCand;
                }

                // Đơn giá DAP: số lẻ hoặc số trong khoảng 0.5 đến 500
                if (item.UnitPriceDap == 0)
                {
                    var pCand = numericCandidates.FirstOrDefault(n => n >= 0.5m && n <= 500m && n != item.Quantity);
                    if (pCand > 0) item.UnitPriceDap = pCand;
                }

                // Trị giá DAP: số lớn nhất hoặc tính từ Qty * Price
                if (item.AmountDap == 0)
                {
                    if (item.Quantity > 0 && item.UnitPriceDap > 0)
                    {
                        item.AmountDap = Math.Round(item.Quantity * item.UnitPriceDap, 2);
                    }
                    else
                    {
                        var aCand = numericCandidates.FirstOrDefault(n => n > 500m && n != item.Quantity);
                        if (aCand > 0) item.AmountDap = aCand;
                    }
                }
            }
        }

        // Tự tính giá trị còn thiếu nếu đã có 2 trong 3
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
        if (string.IsNullOrWhiteSpace(text)) return;
        string trimmed = text.Trim();

        // 1. Tách StyleCode:
        // Format: "40700-007 (KM3.PO5.26)#&Giày..." hoặc "45428-2LX.G #&Giày..." hoặc "40700-007(KM3.PO5.26)#&..."
        // Hoặc có tiền tố "Mô tả hàng hóa: 40700-007 (KM3.PO5.26)#&..."
        string codeCandidate = "";

        // Ưu tiên khớp regex mã StyleCode chuẩn (vd: 40700-007, 45187-1WQ, 45428-2LX.G)
        var matchPattern = Regex.Match(trimmed, @"\b([A-Za-z0-9]{4,6}-[A-Za-z0-9]{2,4}(?:\.G)?)\b", RegexOptions.IgnoreCase);
        if (matchPattern.Success)
        {
            codeCandidate = matchPattern.Groups[1].Value.Trim();
        }
        else
        {
            // Fallback: cắt bỏ tiền tố nhãn nếu có dấu hai chấm
            string cleaned = trimmed;
            int colonIdx = cleaned.IndexOf(':');
            if (colonIdx >= 0 && colonIdx < cleaned.Length - 1)
            {
                cleaned = cleaned[(colonIdx + 1)..].Trim();
            }
            var firstPart = cleaned.Split(new[] { "#&" }, StringSplitOptions.None)[0].Trim();
            var matchStyle = Regex.Match(firstPart, @"^([^\(\s]+)");
            if (matchStyle.Success)
            {
                codeCandidate = matchStyle.Groups[1].Value.Trim();
            }
        }

        if (!string.IsNullOrEmpty(codeCandidate))
        {
            string upper = codeCandidate.ToUpperInvariant();
            if (upper.EndsWith(".G"))
            {
                item.StyleCode = upper[..^2].Trim();
                item.ProcessType = ProcessType.GoKhongMay;
            }
            else
            {
                item.StyleCode = upper;
                item.ProcessType = ProcessType.Standard;
            }
        }

        // 2. Tách Đơn giá CMT:
        // Format: "(Đơn giá gia công: 3.00 USD/đôi)" hoặc "Đơn giá gia công: 3,00 USD/đôi"
        var matchCmt = Regex.Match(trimmed, @"(?:gia|giá)?\s*c[oô]ng\s*[:=]?\s*([0-9,.]+)", RegexOptions.IgnoreCase);
        if (matchCmt.Success && TryParseCustomsDecimal(matchCmt.Groups[1].Value, out decimal cmt))
        {
            item.UnitPriceCmt = cmt;
        }
    }

    /// <summary>
    /// Chuyển đổi chuỗi số từ tờ khai VNACCS (hỗ trợ cả định dạng phẩy Việt Nam/Châu Âu 8,1 và chấm 8.1, nghìn 13.585,2 hoặc 1,652.40)
    /// </summary>
    public static bool TryParseCustomsDecimal(string? input, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string s = input.Trim().Replace("$", "").Replace("USD", "", StringComparison.OrdinalIgnoreCase).Trim();

        int lastDot = s.LastIndexOf('.');
        int lastComma = s.LastIndexOf(',');

        if (lastDot >= 0 && lastComma >= 0)
        {
            if (lastComma > lastDot)
            {
                // Format: 1.652,40 hoặc 13.585,2 -> Dấu chấm là phân cách hàng nghìn, dấu phẩy là thập phân
                s = s.Replace(".", "").Replace(",", ".");
            }
            else
            {
                // Format: 1,652.40 hoặc 13,585.20 -> Dấu phẩy là phân cách hàng nghìn, dấu chấm là thập phân
                s = s.Replace(",", "");
            }
        }
        else if (lastComma >= 0)
        {
            int commaCount = s.Count(c => c == ',');
            if (commaCount == 1)
            {
                // Format: 8,1 hoặc 8,2 hoặc 1652,40 hoặc 204,00
                s = s.Replace(",", ".");
            }
            else
            {
                // Nhiều dấu phẩy -> phân cách hàng nghìn: 1,000,000
                s = s.Replace(",", "");
            }
        }
        else if (lastDot >= 0)
        {
            int dotCount = s.Count(c => c == '.');
            if (dotCount > 1)
            {
                // Format: 1.000.000 -> phân cách hàng nghìn
                s = s.Replace(".", "");
            }
        }

        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Chuyển đổi chuỗi số lượng từ tờ khai VNACCS (hỗ trợ 204, 204,00, 1.200, v.v.)
    /// </summary>
    public static bool TryParseCustomsQuantity(string? input, out int quantity)
    {
        quantity = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string s = Regex.Replace(input.Trim(), @"[^\d,.]", "");
        if (TryParseCustomsDecimal(s, out decimal d) && d >= 0 && d < 10_000_000m)
        {
            quantity = (int)Math.Round(d);
            return true;
        }
        return false;
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

        // KIỂM TRA LỆCH SỐ HÓA ĐƠN:
        // Nếu mở đối soát từ một đơn hàng cụ thể nhưng số hóa đơn trên tờ khai khác với đơn hàng này
        if (specificOrderId.HasValue && specificOrderId.Value > 0 && !string.IsNullOrWhiteSpace(declaration.InvoiceNo))
        {
            string decInv = declaration.InvoiceNo.Trim();
            string ordInv = order.InvoiceNo.Trim();
            if (!string.Equals(decInv, ordInv, StringComparison.OrdinalIgnoreCase))
            {
                result.IsInvoiceMismatch = true;
                result.InvoiceMismatchWarning = $"Số hóa đơn trên tờ khai ({declaration.InvoiceNo}) không trùng với hóa đơn hiện tại ({order.InvoiceNo}).";
                discrepancies.Add(result.InvoiceMismatchWarning);
            }
        }

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

        bool itemsMatched = comparisonRows.Count > 0 && comparisonRows.All(r => r.IsMatched) && result.TotalQuantityMatched;

        decimal invoiceTotalDap = order.Items.Sum(i => i.Quantity * i.UnitPriceDAP);
        decimal customsTotalDap = declaration.TotalDap > 0 ? declaration.TotalDap : declaration.Items.Sum(i => i.Quantity * i.UnitPriceDap);
        result.TotalDapMatched = itemsMatched || Math.Abs(invoiceTotalDap - customsTotalDap) <= Math.Max(1.0m, invoiceTotalDap * 0.002m);

        decimal invoiceTotalCmt = order.Items.Sum(i => i.Quantity * i.UnitPriceCMT);
        decimal customsTotalCmt = declaration.TotalCmt > 0 ? declaration.TotalCmt : declaration.Items.Sum(i => i.Quantity * i.UnitPriceCmt);
        result.TotalCmtMatched = customsTotalCmt == 0 || Math.Abs(invoiceTotalCmt - customsTotalCmt) <= 0.05m;

        // Đơn hàng được coi là khớp 100% khi:
        // Không bị lệch số hóa đơn, không có điểm sai lệch nào và toàn bộ các dòng hàng đối soát đều khớp
        result.IsFullyMatched = !result.IsInvoiceMismatch && itemsMatched && discrepancies.Count == 0;
        result.Discrepancies = discrepancies;

        if (result.IsInvoiceMismatch)
        {
            result.Message = $"⚠ Cảnh báo sai lệch hóa đơn: {result.InvoiceMismatchWarning}";
        }
        else if (result.IsFullyMatched)
        {
            result.Message = $"✔ Đối soát hoàn tất: Dữ liệu Tờ khai hải quan và Hóa đơn {order.InvoiceNo} khớp 100% ({invoiceTotalQty:N0} đôi, {comparisonRows.Count} dòng hàng). Đủ điều kiện chuyển trạng thái sang Đã thông quan.";
        }
        else
        {
            result.Message = $"⚠ Cảnh báo sai lệch: Phát hiện {discrepancies.Count} điểm không trùng khớp giữa Tờ khai và Hóa đơn {order.InvoiceNo}. Vui lòng kiểm tra chi tiết bảng đối soát.";
        }

        return result;
    }

    public static string NormalizeStyleCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var firstPart = code.Split(new[] { "#&" }, StringSplitOptions.None)[0].Trim();
        var match = Regex.Match(firstPart, @"^([^\(\s]+)");
        string extracted = match.Success ? match.Groups[1].Value.Trim() : firstPart.Trim();
        string clean = extracted.ToUpperInvariant();
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

        // Lưu file đính kèm vào thư mục uploads/customs theo yêu cầu lưu trữ điện tử
        string? savedFileName = null;
        string? relativeSavedPath = null;
        if (fileStream != null)
        {
            string storageDir = Path.Combine(_environment.ContentRootPath, "Uploads", "Customs");
            if (!Directory.Exists(storageDir))
            {
                Directory.CreateDirectory(storageDir);
            }

            string ext = !string.IsNullOrWhiteSpace(originalFileName) 
                ? Path.GetExtension(originalFileName) 
                : ".xls";
            if (string.IsNullOrEmpty(ext)) ext = ".xls";

            string declPart = string.IsNullOrWhiteSpace(request.DeclarationNo) ? "TK" : request.DeclarationNo.Trim();
            savedFileName = $"{declPart}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            string fullPath = Path.Combine(storageDir, savedFileName);
            relativeSavedPath = Path.Combine("Uploads", "Customs", savedFileName).Replace("\\", "/");

            using var targetStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(targetStream);
        }

        // Cập nhật thông tin pháp lý hải quan vào ShipmentOrder
        order.DeclarationNo = request.DeclarationNo;
        order.ClearanceDate = request.ClearanceDate ?? DateTime.UtcNow;
        order.CustomsDeclarationType = !string.IsNullOrWhiteSpace(request.CustomsDeclarationType)
            ? request.CustomsDeclarationType
            : "E52";
        order.CustomsChannel = request.CustomsChannel;
        order.CustomsOffice = request.CustomsOffice;
        order.CustomsPackageQty = request.CustomsPackageQty;
        order.CustomsGrossWeight = request.CustomsGrossWeight;
        order.CustomsTotalDap = request.CustomsTotalDap;
        order.CustomsTotalCmt = request.CustomsTotalCmt;

        if (!string.IsNullOrEmpty(savedFileName))
        {
            order.CustomsAttachmentFileName = savedFileName;
            order.CustomsAttachmentFilePath = relativeSavedPath;
        }

        // Cập nhật trạng thái và khóa hồ sơ điện tử
        if (request.IsFullyMatched)
        {
            order.Status = ShipmentStatus.Cleared;
            order.IsLocked = true; // Khóa chỉnh sửa hồ sơ sau khi thông quan
        }
        else
        {
            order.Status = ShipmentStatus.Discrepancy;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Đã xác nhận đồng bộ thông tin hải quan cho đơn hàng #{OrderId} (Status: {Status}, IsLocked: {IsLocked})", orderId, order.Status, order.IsLocked);

        return order;
    }

    /// <summary>
    /// Lấy file tờ khai đã đính kèm theo đơn hàng
    /// </summary>
    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetAttachmentAsync(int orderId)
    {
        var order = await _context.ShipmentOrders.FindAsync(orderId);
        if (order == null || (string.IsNullOrEmpty(order.CustomsAttachmentFileName) && string.IsNullOrEmpty(order.CustomsAttachmentFilePath)))
        {
            return null;
        }

        string? filePath = null;
        if (!string.IsNullOrEmpty(order.CustomsAttachmentFilePath))
        {
            string candidate = Path.IsPathRooted(order.CustomsAttachmentFilePath)
                ? order.CustomsAttachmentFilePath
                : Path.Combine(_environment.ContentRootPath, order.CustomsAttachmentFilePath);
            if (File.Exists(candidate)) filePath = candidate;
        }

        if (filePath == null && !string.IsNullOrEmpty(order.CustomsAttachmentFileName))
        {
            string candidate1 = Path.Combine(_environment.ContentRootPath, "Uploads", "Customs", order.CustomsAttachmentFileName);
            string candidate2 = Path.Combine(_environment.ContentRootPath, "data", "customs", order.CustomsAttachmentFileName);
            if (File.Exists(candidate1)) filePath = candidate1;
            else if (File.Exists(candidate2)) filePath = candidate2;
        }

        if (filePath == null || !File.Exists(filePath))
        {
            return null;
        }

        byte[] bytes = await File.ReadAllBytesAsync(filePath);
        string fileName = Path.GetFileName(filePath);
        string contentType = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "application/vnd.ms-excel";

        return (bytes, contentType, fileName);
    }
}
