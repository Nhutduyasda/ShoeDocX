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
    private readonly IConfiguration _configuration;
    private readonly ILogger<OcrExtractionService> _logger;

    public OcrExtractionService(
        HttpClient httpClient,
        AppDbContext context,
        IConfiguration configuration,
        ILogger<OcrExtractionService> logger)
    {
        _httpClient = httpClient;
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OcrExtractionResponseDto> ExtractFromImageAsync(
        Stream imageStream,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        var openAiKey = _configuration["OpenAI:ApiKey"]
            ?? _configuration["OcrSettings:OpenAIApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

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
        var model = _configuration["OpenAI:Model"]
            ?? _configuration["OcrSettings:OpenAIModel"]
            ?? "gpt-4o-mini";

        var url = "https://api.openai.com/v1/chat/completions";

        var prompt = @"Bạn là trợ lý bóc tách bảng số liệu kiểm kho từ hình ảnh. Hãy đọc thật kỹ từng dòng trong bảng:
1. Tiêu đề: Đọc chính xác dòng text màu đỏ/đen ở trên cùng của bảng (Ví dụ: ""LẦN 20 08/9 5BUY HD THÀNH HÌNH"").
2. Bảng dữ liệu:
   - Chỉ đọc các dòng CÓ DỮ LIỆU. Bỏ qua hoàn toàn các dòng kẻ trống.
   - Cột 1 (Hình thể/Mã giày): Giữ nguyên format mã (vd: ""42072-410"", ""42073-030"").
   - Cột 2 (Số lượng đi hàng): Đọc đúng số nguyên tương ứng trên cùng dòng đó.
   - Cột 3 (Ghi chú/Màu sắc nếu có): Nếu có ghi chú bên cạnh (vd: ""GÒ KHÔNG MAY"") thì trích xuất, nếu không thì để chuỗi rỗng """".
3. Tổng cộng: Đọc chính xác con số nằm trong ô màu vàng ở dòng ""TỔNG CỘNG"" cuối bảng.

Trả về DUY NHẤT một chuỗi JSON hợp lệ theo cấu trúc:
{
  ""title"": string,
  ""reportedTotal"": number,
  ""items"": [
    { ""styleCode"": string, ""quantity"": number, ""note"": string }
  ]
}";

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
            _logger.LogError("OpenAI API trả về lỗi HTTP {StatusCode}: {Response}", response.StatusCode, responseString);

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
                    throw new HttpRequestException($"Lỗi từ OpenAI API: {msg}");
                }
            }
            catch (JsonException)
            {
                // Bỏ qua nếu không parse được JSON lỗi
            }

            throw new HttpRequestException($"OpenAI API gặp sự cố (HTTP {(int)response.StatusCode}): {responseString}");
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

        var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
        var reportedTotal = root.TryGetProperty("reportedTotal", out var repProp) && repProp.TryGetInt32(out var repVal) ? repVal : 0;

        var extractedItems = new List<ExtractedRawItem>();
        if (root.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemElem in itemsProp.EnumerateArray())
            {
                var code = itemElem.TryGetProperty("styleCode", out var sProp) ? sProp.GetString() ?? "" : "";
                var qty = itemElem.TryGetProperty("quantity", out var qProp) && qProp.TryGetInt32(out var qVal) ? qVal : 0;
                var note = itemElem.TryGetProperty("note", out var nProp) ? nProp.GetString() ?? "" : "";

                if (!string.IsNullOrWhiteSpace(code) && qty > 0)
                {
                    extractedItems.Add(new ExtractedRawItem(code.Trim(), qty, note.Trim()));
                }
            }
        }

        // Tra cứu Master Data trong SQLite để làm giàu thông tin (Enrichment)
        var lookupCodes = extractedItems.Select(x =>
        {
            var c = x.StyleCode.ToUpperInvariant();
            if (c.EndsWith(".G")) c = c.Substring(0, c.Length - 2).Trim();
            return c;
        }).Distinct().ToList();

        var dbProducts = await _context.ProductMasters
            .AsNoTracking()
            .Where(p => lookupCodes.Contains(p.StyleCode.ToUpper()))
            .ToDictionaryAsync(p => p.StyleCode.ToUpper(), p => p, cancellationToken);

        var finalItems = new List<OcrItemDto>();
        foreach (var raw in extractedItems)
        {
            var upperCode = raw.StyleCode.ToUpperInvariant();
            var lookupCode = upperCode.EndsWith(".G") ? upperCode.Substring(0, upperCode.Length - 2).Trim() : upperCode;

            dbProducts.TryGetValue(lookupCode, out var matchedProduct);

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

        var calculatedTotal = finalItems.Sum(x => x.Quantity);
        if (reportedTotal == 0)
        {
            reportedTotal = calculatedTotal;
        }

        return new OcrExtractionResponseDto
        {
            Title = title,
            Items = finalItems,
            ReportedTotal = reportedTotal,
            CalculatedTotal = calculatedTotal,
            IsTotalMatched = (calculatedTotal == reportedTotal)
        };
    }

    private record ExtractedRawItem(string StyleCode, int Quantity, string Note);
}
