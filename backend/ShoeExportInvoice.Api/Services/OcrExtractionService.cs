using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public class OcrExtractionService : IOcrExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;
    private readonly OcrOptions _options;
    private readonly ILogger<OcrExtractionService> _logger;

    public OcrExtractionService(
        HttpClient httpClient,
        AppDbContext context,
        IConfiguration configuration,
        ILogger<OcrExtractionService> logger, Microsoft.Extensions.Options.IOptions<OcrOptions>? options = null)
    {
        _httpClient = httpClient;
        _context = context;
        _options = options?.Value ?? configuration.GetSection("OpenAI").Get<OcrOptions>() ?? new OcrOptions();
        _logger = logger;
    }

    public async Task<OcrExtractionResponseDto> ExtractFromImageAsync(
        Stream imageStream,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? _options.ApiKey;

        if (string.IsNullOrWhiteSpace(openAiKey))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình OpenAI API Key. Vui lòng mở file backend/ShoeExportInvoice.Api/appsettings.json " +
                "và điền API Key vào mục 'OpenAI:ApiKey' (hoặc biến môi trường OPENAI_API_KEY).");
        }

        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();

        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Dữ liệu hình ảnh trống. Vui lòng tải lên ảnh hợp lệ.");
        }

        var base64Image = Convert.ToBase64String(imageBytes);

        // Gọi trực tiếp OpenAI Vision API (gpt-4o-mini) - KHÔNG dùng Fallback/Mock data
        var rawJson = await CallOpenAiVisionApiAsync(base64Image, mimeType, openAiKey.Trim(), cancellationToken);

        // Parse kết quả, đối soát số liệu và map với danh mục Master Data
        var result = await ParseAndEnrichOcrResultAsync(rawJson, cancellationToken);
        result.IsSimulation = false;
        result.RawJsonResponse = rawJson;

        return result;
    }

    private async Task<string> CallOpenAiVisionApiAsync(
        string base64Image,
        string mimeType,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var model = _options.Model;

        var url = _options.Endpoint;

        var prompt = @"Bạn là trợ lý AI cao cấp chuyên bóc tách số liệu bảng kiểm kho / phiếu giao hàng từ hình ảnh hoá đơn xuất khẩu giày.

NHIỆM VỤ QUAN TRỌNG:
Ảnh có thể chứa MỘT hoặc NHIỀU bảng giao hàng độc lập (thường được đánh số đợt như 'LẦN 19', 'LẦN 20', 'ĐỢT 1', 'ĐỢT 2'...).

1. ĐẶC BIỆT CHÚ Ý CÁC BẢNG NẰM SONG SONG / CẠNH NHAU (SIDE-BY-SIDE / HAI CỘT TRÁI - PHẢI):
   - Rất phổ biến trường hợp ảnh chụp bảng tính Excel hoặc tài liệu có 2 (hoặc nhiều) bảng đặt song song cạnh nhau trên cùng một trang (ví dụ: Cột bảng bên trái là 'LẦN 19', cột bảng bên phải là 'LẦN 20').
   - Hãy chia tài liệu theo chiều dọc thành các khối (bounding box / cột dữ liệu) độc lập.
   - Với mỗi bảng: Quét TOÀN BỘ từ tiêu đề trên cùng ('LẦN ...') xuống hết các dòng hàng và dòng TỔNG CỘNG của bảng đó.
   - TUYỆT ĐỐI KHÔNG quét ngang hàng từ trái sang phải qua cả trang giấy vì sẽ làm trộn lẫn dòng của bảng trái với bảng phải (interleaving rows), ghép sai mã giày hoặc nhầm số lượng giữa các đợt!
   - Bảng bên trái tạo thành một document riêng, bảng bên phải tạo thành một document riêng rẽ.

2. VỚI TỪNG BẢNG ĐỘC LẬP:
   - title: Trích xuất tiêu đề chính xác của bảng đó (ví dụ: ""LẦN 19"", ""LẦN 20"", hoặc ""BẢNG XUẤT HÀNG ĐỢT 1"").
   - items (danh sách dòng hàng):
     + Chỉ đọc các dòng CÓ DỮ LIỆU THỰC SỰ. Bỏ qua hoàn toàn các dòng kẻ trống hoặc dòng tiêu đề lặp lại.
     + styleCode (Hình thể / Mã giày): Giữ nguyên format mã (vd: ""42072-410"", ""BM5879-464""). Chỉ trích xuất mã hình thể gốc, loại bỏ các ký hiệu ghi chú phân xưởng / đối tác nằm trong ngoặc ở đuôi như '(KM3)', '(X3)'.
     + quantity (Số lượng đi hàng): Chỉ lấy số nguyên khi số nằm cùng hàng với styleCode, thuộc đúng cột số lượng của bảng đó. TUYỆT ĐỐI KHÔNG lấy các số từ tiêu đề, số lần, ngày tháng, số thứ tự (STT), ghi chú phụ, số trang hoặc annotation ngoài lề.
     + note (Ghi chú): Trích xuất ghi chú quy trình / màu sắc nếu có (ví dụ: ""GÒ KHÔNG MAY"", ""GO KHONG MAY"", màu sắc...), nếu không có thì để chuỗi rỗng """".
   - reportedTotal (Tổng cộng được in trên bảng):
     + Chỉ đọc con số nếu nó nằm cạnh nhãn 'TỔNG', 'TỔNG CỘNG', hoặc 'TOTAL' của chính bảng đó.
     + Nếu không có hoặc không chắc chắn, trả null. TUYỆT ĐỐI không đoán và không tự tính thay.
   - sourceRegion (Vùng tọa độ của bảng):
     + Tọa độ chuẩn hóa từ 0.0 đến 1.0 (x, y, width, height) bao quanh toàn bộ bảng đó.
     + Ví dụ: Bảng bên trái thường có x ~ 0.0, width ~ 0.5. Bảng bên phải thường có x ~ 0.5, width ~ 0.5. Trả null nếu không xác định được.

ĐỊNH DẠNG ĐẦU RA (JSON FORMAT):
Trả về DUY NHẤT một chuỗi JSON hợp lệ tuân thủ cấu trúc sau:
{
  ""documents"": [
    {
      ""title"": ""LẦN 19"",
      ""reportedTotal"": 1500,
      ""sourceRegion"": { ""x"": 0.02, ""y"": 0.05, ""width"": 0.46, ""height"": 0.88 },
      ""items"": [
        { ""styleCode"": ""42072-410"", ""quantity"": 500, ""note"": """" },
        { ""styleCode"": ""42073-030"", ""quantity"": 1000, ""note"": ""GÒ KHÔNG MAY"" }
      ]
    },
    {
      ""title"": ""LẦN 20"",
      ""reportedTotal"": 2000,
      ""sourceRegion"": { ""x"": 0.52, ""y"": 0.05, ""width"": 0.46, ""height"": 0.88 },
      ""items"": [
        { ""styleCode"": ""42072-410"", ""quantity"": 2000, ""note"": """" }
      ]
    }
  ]
}
TUYỆT ĐỐI không trả mảng items toàn cục ngoài documents. Mỗi bảng song song hoặc độc lập bắt buộc phải là một document riêng trong mảng documents.";

        var requestBody = new
        {
            model = model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:{mimeType};base64,{base64Image}",
                                detail = "high"
                            }
                        }
                    }
                }
            },
            response_format = new { type = "json_object" },
            max_tokens = 4096,
            temperature = 0.1
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể kết nối đến OpenAI API.");
            throw new HttpRequestException($"Lỗi kết nối đến máy chủ OpenAI: {ex.Message}", ex);
        }

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI API trả về lỗi HTTP {StatusCode}", response.StatusCode);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HttpRequestException("OpenAI API Key không hợp lệ hoặc đã hết hạn (HTTP 401). Vui lòng kiểm tra lại 'OpenAI:ApiKey' trong appsettings.json.");
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new HttpRequestException("Tài khoản OpenAI đã hết hạn mức sử dụng (Quota / Rate Limit - HTTP 429). Vui lòng nạp thêm credit hoặc thử lại sau.");
            }

            try
            {
                using var errDoc = JsonDocument.Parse(responseString);
                if (errDoc.RootElement.TryGetProperty("error", out var errorObj) &&
                    errorObj.TryGetProperty("message", out var msgProp))
                {
                    var msg = msgProp.GetString();
                    _logger.LogWarning("OpenAI API error message: {ProviderMessage}", msg);
                }
            }
            catch (JsonException)
            {
                // Bỏ qua nếu không parse được JSON lỗi
            }

            throw new HttpRequestException($"OpenAI API gặp sự cố (HTTP {(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(responseString);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("OpenAI không trả về nội dung nhận diện nào.");
        }

        var content = choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content?.Trim() ?? string.Empty;
    }

    public async Task<OcrExtractionResponseDto> ParseAndEnrichOcrResultAsync(
        string rawJson,
        CancellationToken cancellationToken)
    {
        // Làm sạch Markdown code blocks nếu LLM vô tình bọc ```json ... ```
        var cleanJson = rawJson.Trim();
        if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleanJson = cleanJson.Substring(7);
        }
        else if (cleanJson.StartsWith("```"))
        {
            cleanJson = cleanJson.Substring(3);
        }

        if (cleanJson.EndsWith("```"))
        {
            cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
        }
        cleanJson = cleanJson.Trim();

        using var doc = JsonDocument.Parse(cleanJson);
        var root = doc.RootElement;

        // Accept documents array, or batches/tables, or top-level array, or fallback to single object root
        var documentElements = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().ToList()
            : (root.TryGetProperty("documents", out var docsProp) && docsProp.ValueKind == JsonValueKind.Array
                ? docsProp.EnumerateArray().ToList()
                : (root.TryGetProperty("batches", out var batchesProp) && batchesProp.ValueKind == JsonValueKind.Array
                    ? batchesProp.EnumerateArray().ToList()
                    : (root.TryGetProperty("tables", out var tablesProp) && tablesProp.ValueKind == JsonValueKind.Array
                        ? tablesProp.EnumerateArray().ToList()
                        : new List<JsonElement> { root })));

        var rawDocuments = new List<(string Title, int? ReportedTotal, OcrSourceRegionDto? Region, List<ExtractedRawItem> Items)>();
        foreach (var documentElement in documentElements)
        {
            var title = documentElement.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
            int? reportedTotal = documentElement.TryGetProperty("reportedTotal", out var repProp) &&
                repProp.ValueKind == JsonValueKind.Number && repProp.TryGetInt32(out var repVal) ? repVal : null;
            OcrSourceRegionDto? region = null;
            if (documentElement.TryGetProperty("sourceRegion", out var regionProp) && regionProp.ValueKind == JsonValueKind.Object)
            {
                region = new OcrSourceRegionDto
                {
                    X = ReadNullableDouble(regionProp, "x"), Y = ReadNullableDouble(regionProp, "y"),
                    Width = ReadNullableDouble(regionProp, "width"), Height = ReadNullableDouble(regionProp, "height")
                };
            }
            var items = new List<ExtractedRawItem>();
            if (documentElement.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                foreach (var itemElem in itemsProp.EnumerateArray())
                {
                    var code = NormalizeStyleCode(itemElem.TryGetProperty("styleCode", out var s) ? s.GetString() ?? "" : "");
                    var qty = itemElem.TryGetProperty("quantity", out var q) && q.TryGetInt32(out var qv) ? qv : 0;
                    var note = itemElem.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(code) && qty > 0) items.Add(new(code.Trim(), qty, note.Trim()));
                }
            rawDocuments.Add((title, reportedTotal, region, items));
        }

        // Tra cứu Master Data trong SQLite để làm giàu thông tin (Enrichment)
        var lookupCodes = rawDocuments.SelectMany(d => d.Items).Select(x =>
        {
            var c = x.StyleCode.ToUpperInvariant();
            if (c.EndsWith(".G")) c = c.Substring(0, c.Length - 2).Trim();
            return c;
        }).Distinct().ToList();

        var dbProducts = await _context.ProductMasters
            .AsNoTracking()
            .Where(p => lookupCodes.Contains(p.StyleCode.ToUpper()))
            .ToListAsync(cancellationToken);
        var productMap = dbProducts.GroupBy(p => p.StyleCode.ToUpperInvariant()).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());

        var result = new OcrExtractionResponseDto();
        foreach (var rawDocument in rawDocuments)
        {
            var finalItems = new List<OcrItemDto>();
            foreach (var raw in rawDocument.Items)
            {
                var upperCode = raw.StyleCode.ToUpperInvariant();
                var lookupCode = upperCode.EndsWith(".G") ? upperCode.Substring(0, upperCode.Length - 2).Trim() : upperCode;

                productMap.TryGetValue(lookupCode, out var matchedProduct);

            // Kiểm tra quy trình Gò không may
                var isGoProcess = raw.Note.Contains("GÒ", StringComparison.OrdinalIgnoreCase)
                    || raw.Note.Contains("GO", StringComparison.OrdinalIgnoreCase)
                    || upperCode.EndsWith(".G");

            var processType = isGoProcess ? ProcessType.GoKhongMay : ProcessType.Standard;

                finalItems.Add(new OcrItemDto
                {
                StyleCode = raw.StyleCode,
                Quantity = raw.Quantity,
                Note = raw.Note,
                ProcessType = processType,
                UnitPriceCMT = matchedProduct?.UnitPriceCMT ?? 0m,
                UnitPriceDAP = matchedProduct?.UnitPriceDAP ?? 0m,
                PairPerCarton = (matchedProduct?.PairPerCarton > 0) ? matchedProduct.PairPerCarton : 12,
                Description = matchedProduct?.Description ?? string.Empty,
                Unit = matchedProduct?.Unit ?? "đôi",
                IsMatched = matchedProduct != null
                });
            }
            result.Documents.Add(new OcrDetectedDocumentDto
            {
                Title = rawDocument.Title,
                ReportedTotal = rawDocument.ReportedTotal,
                CalculatedTotal = finalItems.Sum(x => x.Quantity),
                Items = finalItems,
                SourceRegion = rawDocument.Region
            });
        }
        return result;
    }

    public async Task<List<OcrDetectedDocumentDto>> ReEnrichOcrDocumentsForPartnerAsync(
        List<OcrDetectedDocumentDto> documents,
        int? partnerFolderId,
        CancellationToken cancellationToken = default)
    {
        if (documents == null || documents.Count == 0) return new List<OcrDetectedDocumentDto>();

        var lookupCodes = documents.SelectMany(d => d.Items).Select(x =>
        {
            var c = x.StyleCode.ToUpperInvariant();
            if (c.EndsWith(".G")) c = c.Substring(0, c.Length - 2).Trim();
            return c;
        }).Distinct().ToList();

        var query = _context.ProductMasters.AsNoTracking().Where(p => lookupCodes.Contains(p.StyleCode.ToUpper()));
        if (partnerFolderId.HasValue)
        {
            query = query.Where(p => p.FolderId == partnerFolderId.Value);
        }

        var dbProducts = await query.ToListAsync(cancellationToken);
        var productMap = dbProducts.GroupBy(p => p.StyleCode.ToUpperInvariant()).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());

        var enrichedDocs = new List<OcrDetectedDocumentDto>();
        foreach (var doc in documents)
        {
            var enrichedItems = new List<OcrItemDto>();
            foreach (var item in doc.Items)
            {
                var upperCode = item.StyleCode.ToUpperInvariant();
                var lookupCode = upperCode.EndsWith(".G") ? upperCode.Substring(0, upperCode.Length - 2).Trim() : upperCode;

                productMap.TryGetValue(lookupCode, out var matchedProduct);

                var isGo = item.ProcessType == ProcessType.GoKhongMay
                    || item.Note.Contains("GÒ", StringComparison.OrdinalIgnoreCase)
                    || item.Note.Contains("GO", StringComparison.OrdinalIgnoreCase)
                    || upperCode.EndsWith(".G");

                decimal cmt = isGo ? (matchedProduct?.UnitPriceCMT_Go ?? matchedProduct?.UnitPriceCMT ?? 0m) : (matchedProduct?.UnitPriceCMT ?? 0m);
                decimal dap = isGo ? (matchedProduct?.UnitPriceDAP_Go ?? matchedProduct?.UnitPriceDAP ?? 0m) : (matchedProduct?.UnitPriceDAP ?? 0m);

                enrichedItems.Add(new OcrItemDto
                {
                    StyleCode = item.StyleCode,
                    Quantity = item.Quantity,
                    Note = item.Note,
                    ProcessType = isGo ? ProcessType.GoKhongMay : ProcessType.Standard,
                    UnitPriceCMT = cmt,
                    UnitPriceDAP = dap,
                    PairPerCarton = (matchedProduct?.PairPerCarton > 0) ? matchedProduct.PairPerCarton : 12,
                    Description = matchedProduct?.Description ?? string.Empty,
                    Unit = matchedProduct?.Unit ?? "đôi",
                    IsMatched = matchedProduct != null
                });
            }

            var calculatedTotal = enrichedItems.Sum(x => x.Quantity);
            enrichedDocs.Add(new OcrDetectedDocumentDto
            {
                DocumentId = doc.DocumentId,
                Title = doc.Title,
                ReportedTotal = doc.ReportedTotal,
                CalculatedTotal = calculatedTotal,
                Items = enrichedItems,
                SourceRegion = doc.SourceRegion,
                IsManuallyConfirmed = doc.IsManuallyConfirmed && doc.CalculatedTotal == calculatedTotal,
                ConfirmationReason = doc.ConfirmationReason
            });
        }

        return enrichedDocs;
    }

    private static double? ReadNullableDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetDouble(out var number) ? number : null;

    public static string NormalizeStyleCode(string rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode)) return string.Empty;
        var trimmed = rawCode.Trim();
        bool hasGo = false;
        if (trimmed.EndsWith(".G", StringComparison.OrdinalIgnoreCase))
        {
            hasGo = true;
            trimmed = trimmed[..^2].Trim();
        }
        // Xóa bỏ phần trong ngoặc đơn ở cuối mã: vd "BM5879-464(KM3)" -> "BM5879-464"
        var cleaned = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s*\([^\)]*\)$", "").Trim();
        if (hasGo && !cleaned.EndsWith(".G", StringComparison.OrdinalIgnoreCase))
        {
            cleaned += ".G";
        }
        return cleaned;
    }

    private record ExtractedRawItem(string StyleCode, int Quantity, string Note);
}
