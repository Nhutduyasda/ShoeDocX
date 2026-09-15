using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Models.Templates;

namespace ShoeExportInvoice.Api.Services;

public class TemplateAiParserService : ITemplateAiParserService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IExcelImportExportService _excelService;
    private readonly ILogger<TemplateAiParserService> _logger;

    public TemplateAiParserService(
        HttpClient httpClient,
        IConfiguration configuration,
        IExcelImportExportService excelService,
        ILogger<TemplateAiParserService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _excelService = excelService;
        _logger = logger;
    }

    public string ExtractTextGrid(Stream excelStream)
    {
        using var ms = new MemoryStream();
        excelStream.CopyTo(ms);
        ms.Position = 0;
        excelStream.Position = 0;

        using var workbook = new XLWorkbook(ms);

        var invSheet = workbook.Worksheet("INV")
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.ToUpper().Contains("INV"))
            ?? workbook.Worksheets.FirstOrDefault();

        var pklSheet = workbook.Worksheet("PKL")
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.ToUpper().Contains("PKL"))
            ?? workbook.Worksheets.Skip(1).FirstOrDefault();

        var sb = new StringBuilder();

        if (invSheet != null)
        {
            sb.AppendLine($"=== SHEET: {invSheet.Name} ===");
            ExtractSheetGrid(invSheet, sb, maxRow: 35, maxCol: 13);
            sb.AppendLine();
        }

        if (pklSheet != null && pklSheet != invSheet)
        {
            sb.AppendLine($"=== SHEET: {pklSheet.Name} ===");
            ExtractSheetGrid(pklSheet, sb, maxRow: 30, maxCol: 12);
        }

        return sb.ToString();
    }

    private static void ExtractSheetGrid(IXLWorksheet ws, StringBuilder sb, int maxRow, int maxCol)
    {
        for (int r = 1; r <= maxRow; r++)
        {
            var cellEntries = new List<string>();
            for (int c = 1; c <= maxCol; c++)
            {
                var cell = ws.Cell(r, c);
                if (cell.IsEmpty()) continue;

                var text = cell.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Chuẩn hóa và cắt gọn chuỗi để tiết kiệm token
                text = text.Replace("\r", " ").Replace("\n", " ").Trim();
                if (text.Length > 60)
                {
                    text = text[..57] + "...";
                }

                cellEntries.Add($"{cell.Address}: \"{text}\"");
            }

            if (cellEntries.Count > 0)
            {
                sb.AppendLine($"[Row {r}] {string.Join(" | ", cellEntries)}");
            }
        }
    }

    public async Task<AiTemplateAnalysisResult> AnalyzeTemplateAsync(
        Stream excelStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var textGrid = ExtractTextGrid(excelStream);
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
            ?? _configuration["OpenAI:ApiKey"];

        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            try
            {
                var aiResult = await CallOpenAiForTemplateConfigAsync(textGrid, fileName, openAiKey.Trim(), cancellationToken);
                if (aiResult != null && aiResult.Config != null)
                {
                    _logger.LogInformation("Phân tích cấu trúc template thành công bằng AI cho file {FileName}", fileName);
                    return aiResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gọi AI phân tích template thất bại, tự động chuyển sang bộ bóc tách quy tắc thông minh (Heuristic Parser).");
            }
        }
        else
        {
            _logger.LogInformation("Không tìm thấy OpenAI API Key, sử dụng bộ bóc tách quy tắc thông minh (Heuristic Parser) cho {FileName}.", fileName);
        }

        // Heuristic fallback
        var heuristicResult = HeuristicAnalyze(textGrid, fileName);
        return heuristicResult;
    }

    private async Task<AiTemplateAnalysisResult?> CallOpenAiForTemplateConfigAsync(
        string textGrid,
        string fileName,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        var endpoint = _configuration["OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";

        var systemPrompt = @"Bạn là chuyên gia bóc tách biểu mẫu xuất nhập khẩu giày da (Commercial Invoice & Packing List).
Nhiệm vụ của bạn là phân tích cấu trúc các ô trong sheet INV và sheet PKL từ lưới biểu diễn các ô Excel (Text Grid) để xác định tọa độ ô tiêu đề và các cột trong bảng hàng hóa.
Hãy trả về DUY NHẤT một chuỗi JSON hợp lệ theo đúng cấu trúc sau (không kèm markdown format ngoài json):
{
  ""detectedName"": ""Tên biểu mẫu nhận diện được"",
  ""config"": {
    ""templateName"": ""Tên biểu mẫu"",
    ""invSheet"": {
      ""sheetName"": ""INV"",
      ""header"": {
        ""invoiceNoCell"": ""J4"",
        ""dateCell"": ""J5"",
        ""contractNoCell"": ""J6"",
        ""buyerNameCell"": ""D4"",
        ""buyerAddressCell"": ""D5"",
        ""deliveryTermsCell"": ""J7"",
        ""paymentTermsCell"": ""J8"",
        ""destinationCell"": ""D6""
      },
      ""table"": {
        ""startRow"": 13,
        ""itemCodeCol"": ""C"",
        ""descriptionCol"": ""D"",
        ""quantityCol"": ""E"",
        ""unitCol"": ""F"",
        ""cmtUnitPriceCol"": ""G"",
        ""dapUnitPriceCol"": ""H"",
        ""cmtAmountCol"": ""I"",
        ""dapAmountCol"": ""J""
      },
      ""totalAmountCell"": """",
      ""wordsAmountCell"": """"
    },
    ""pklSheet"": {
      ""sheetName"": ""PKL"",
      ""startRow"": 12,
      ""cartonRangeCol"": ""A"",
      ""itemCodeCol"": ""B"",
      ""descriptionCol"": ""C"",
      ""quantityCol"": ""D"",
      ""unitCol"": ""E"",
      ""cartonsCol"": ""F"",
      ""netWeightCol"": ""G"",
      ""grossWeightCol"": ""H""
    }
  }
}";

        var requestBody = new
        {
            model = model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Tên file: {fileName}\n\nLưới ô Excel:\n{textGrid}" }
            },
            response_format = new { type = "json_object" },
            temperature = 0.1,
            max_tokens = 2048
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("OpenAI trả về mã lỗi {StatusCode}: {Error}", response.StatusCode, err);
            return null;
        }

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseString);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content)) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<AiParsedResponse>(content, options);
        if (parsed?.Config == null) return null;

        var detectedName = !string.IsNullOrWhiteSpace(parsed.DetectedName) 
            ? parsed.DetectedName 
            : $"Mẫu {Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Trim()}";

        if (string.IsNullOrWhiteSpace(parsed.Config.TemplateName))
        {
            parsed.Config.TemplateName = detectedName;
        }

        return new AiTemplateAnalysisResult(detectedName, parsed.Config, textGrid, IsAiAnalyzed: true);
    }

    private static string OffsetColumn(string cellAddress, int colOffset = 1)
    {
        if (string.IsNullOrWhiteSpace(cellAddress)) return cellAddress;
        var colMatch = Regex.Match(cellAddress, @"^([A-Z]+)(\d+)$", RegexOptions.IgnoreCase);
        if (!colMatch.Success) return cellAddress;

        var colStr = colMatch.Groups[1].Value.ToUpper();
        var rowStr = colMatch.Groups[2].Value;

        int colNum = 0;
        foreach (char c in colStr)
        {
            colNum = colNum * 26 + (c - 'A' + 1);
        }

        colNum += colOffset;

        string newColStr = string.Empty;
        while (colNum > 0)
        {
            int rem = (colNum - 1) % 26;
            newColStr = (char)('A' + rem) + newColStr;
            colNum = (colNum - 1) / 26;
        }

        return $"{newColStr}{rowStr}";
    }

    public AiTemplateAnalysisResult HeuristicAnalyze(string textGrid, string fileName)
    {
        var config = new DocumentTemplateConfig();
        var lines = textGrid.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string detectedName = $"Mẫu {Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Trim()}";
        bool inInv = true;
        int invStartRow = 13;
        int pklStartRow = 12;

        foreach (var line in lines)
        {
            if (line.Contains("=== SHEET: PKL", StringComparison.OrdinalIgnoreCase))
            {
                inInv = false;
                continue;
            }

            if (inInv)
            {
                // Nhận diện Số hóa đơn
                if (Regex.IsMatch(line, @"(INVOICE\s*NO|INV\s*NO)", RegexOptions.IgnoreCase))
                {
                    var m = Regex.Match(line, @"([A-Z]\d+):\s*""[^""]*(INVOICE\s*NO|INV\s*NO)[^""]*""(?:\s*\|\s*([A-Z]\d+):)?", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var labelCell = m.Groups[1].Value.ToUpper();
                        var nextCell = m.Groups[3].Success ? m.Groups[3].Value.ToUpper() : OffsetColumn(labelCell, 1);
                        config.InvSheet.Header.InvoiceNoCell = nextCell;
                    }
                }

                // Nhận diện Ngày
                if (Regex.IsMatch(line, @"\bDATE\b", RegexOptions.IgnoreCase))
                {
                    var m = Regex.Match(line, @"([A-Z]\d+):\s*""[^""]*DATE[^""]*""(?:\s*\|\s*([A-Z]\d+):)?", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var labelCell = m.Groups[1].Value.ToUpper();
                        var nextCell = m.Groups[2].Success ? m.Groups[2].Value.ToUpper() : OffsetColumn(labelCell, 1);
                        config.InvSheet.Header.DateCell = nextCell;
                    }
                }

                // Nhận diện Hợp đồng
                if (Regex.IsMatch(line, @"(CONTRACT\s*NO|HỢP\s*ĐỒNG)", RegexOptions.IgnoreCase))
                {
                    var m = Regex.Match(line, @"([A-Z]\d+):\s*""[^""]*(CONTRACT|HỢP\s*ĐỒNG)[^""]*""(?:\s*\|\s*([A-Z]\d+):)?", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var labelCell = m.Groups[1].Value.ToUpper();
                        var nextCell = m.Groups[3].Success ? m.Groups[3].Value.ToUpper() : OffsetColumn(labelCell, 1);
                        config.InvSheet.Header.ContractNoCell = nextCell;
                    }
                }

                // Nhận diện Tên khách hàng (MESSRS, TO:, BUYER)
                if (Regex.IsMatch(line, @"(MESSRS|BUYER|TO:)", RegexOptions.IgnoreCase))
                {
                    var m = Regex.Match(line, @"([A-Z]\d+):\s*""[^""]*(MESSRS|BUYER|TO:)[^""]*""(?:\s*\|\s*([A-Z]\d+):)?", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var labelCell = m.Groups[1].Value.ToUpper();
                        var nextCell = m.Groups[3].Success ? m.Groups[3].Value.ToUpper() : OffsetColumn(labelCell, 1);
                        config.InvSheet.Header.BuyerNameCell = nextCell;
                    }
                }

                // Nhận diện Dòng tiêu đề bảng INV -> dòng dữ liệu là header + 1
                if (Regex.IsMatch(line, @"(STYLE\s*CODE|MÃ\s*HÀNG|ART\s*NO)", RegexOptions.IgnoreCase))
                {
                    var rowMatch = Regex.Match(line, @"\[Row\s*(\d+)\]");
                    if (rowMatch.Success && int.TryParse(rowMatch.Groups[1].Value, out int headerRow))
                    {
                        invStartRow = headerRow + 1;
                        config.InvSheet.Table.StartRow = invStartRow;
                    }

                    var colMatch = Regex.Match(line, @"([A-Z])\d+:\s*""[^""]*(STYLE|MÃ\s*HÀNG|ART)", RegexOptions.IgnoreCase);
                    if (colMatch.Success)
                    {
                        config.InvSheet.Table.ItemCodeCol = colMatch.Groups[1].Value.ToUpper();
                    }
                }

                if (Regex.IsMatch(line, @"(QUANTITY|SỐ\s*LƯỢNG|QTY)", RegexOptions.IgnoreCase))
                {
                    var colMatch = Regex.Match(line, @"([A-Z])\d+:\s*""[^""]*(QUANTITY|SỐ\s*LƯỢNG|QTY)", RegexOptions.IgnoreCase);
                    if (colMatch.Success)
                    {
                        config.InvSheet.Table.QuantityCol = colMatch.Groups[1].Value.ToUpper();
                    }
                }

                // Phát hiện dòng sub-header (ví dụ dòng 12 ghi CMT (USD) / DAP (USD) ngay dưới dòng 11)
                if (Regex.IsMatch(line, @"(CMT|DAP|USD)", RegexOptions.IgnoreCase))
                {
                    var rowMatch = Regex.Match(line, @"\[Row\s*(\d+)\]");
                    if (rowMatch.Success && int.TryParse(rowMatch.Groups[1].Value, out int subHeaderRow))
                    {
                        if (subHeaderRow == config.InvSheet.Table.StartRow)
                        {
                            config.InvSheet.Table.StartRow = subHeaderRow + 1;
                        }
                    }
                }
            }
            else
            {
                // Sheet PKL
                if (Regex.IsMatch(line, @"(CARTON|SỐ\s*KIỆN|C/NO)", RegexOptions.IgnoreCase))
                {
                    var rowMatch = Regex.Match(line, @"\[Row\s*(\d+)\]");
                    if (rowMatch.Success && int.TryParse(rowMatch.Groups[1].Value, out int headerRow))
                    {
                        pklStartRow = headerRow + 1;
                        config.PklSheet.StartRow = pklStartRow;
                    }
                }
            }
        }

        config.TemplateName = detectedName;
        return new AiTemplateAnalysisResult(detectedName, config, textGrid, IsAiAnalyzed: false);
    }

    public Task<DocumentPreviewResponseDto> GenerateDummyPreviewAsync(
        Stream excelStream,
        DocumentTemplateConfig config,
        CancellationToken cancellationToken = default)
    {
        var dummyRequest = new CreateShipmentRequestDto
        {
            InvoiceNo = "INV-SAMPLE-001",
            InvoiceDate = DateTime.Today,
            ContractNo = "KM-SAMPLE/01-2026",
            CustomerName = "CÔNG TY TNHH SAMPLE BUYER VIETNAM",
            Address = "LÔ A1-2, KCN VSIP, XÃ TỊNH PHONG, SƠN TỊNH, QUẢNG NGÃI",
            DeliveryTerms = "DAP",
            PaymentTerms = "T/T",
            PoSuffix = "(SAMPLE.PO1)",
            UseSavedSnapshot = true,
            Items = new List<CreateShipmentItemDto>
            {
                new()
                {
                    StyleCode = "42072-030",
                    Description = "Giày Thể Thao Mẫu Nam Standard",
                    Quantity = 1200,
                    UnitPriceCMT = 4.50m,
                    UnitPriceDAP = 14.80m,
                    PairPerCarton = 12,
                    ProcessType = ProcessType.Standard,
                    Unit = "đôi"
                },
                new()
                {
                    StyleCode = "45428-2LX",
                    Description = "Giày Thể Thao Mẫu Nữ (Thùng chẵn + lẻ)",
                    Quantity = 38,
                    UnitPriceCMT = 5.00m,
                    UnitPriceDAP = 16.20m,
                    PairPerCarton = 12,
                    ProcessType = ProcessType.Standard,
                    Unit = "đôi"
                },
                new()
                {
                    StyleCode = "51200-1BK",
                    Description = "Giày Thời Trang Gò Không May",
                    Quantity = 780,
                    UnitPriceCMT = 4.20m,
                    UnitPriceDAP = 13.50m,
                    PairPerCarton = 12,
                    ProcessType = ProcessType.GoKhongMay,
                    Unit = "đôi"
                }
            }
        };

        var preview = _excelService.CalculateDocumentPreview(dummyRequest);
        return Task.FromResult(preview);
    }

    private class AiParsedResponse
    {
        public string? DetectedName { get; set; }
        public DocumentTemplateConfig? Config { get; set; }
    }
}
