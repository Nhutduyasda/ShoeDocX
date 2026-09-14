using System.Text.Json;
using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public static class ShipmentSizeBreakdownValidator
{
    public static void Validate(IEnumerable<CreateShipmentItemDto> items)
    {
        var errors = new List<string>();
        var index = 0;
        foreach (var item in items)
        {
            index++;
            if (string.IsNullOrWhiteSpace(item.SizeBreakdownJson)) continue;
            try
            {
                using var document = JsonDocument.Parse(item.SizeBreakdownJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
                var total = document.RootElement.EnumerateObject().Sum(p => ReadQuantity(p.Value));
                if (total != item.Quantity)
                    errors.Add($"Dòng {index} - mã {item.StyleCode}: tổng Size Breakdown là {total} đôi, nhưng số lượng là {item.Quantity} đôi.");
            }
            catch (JsonException)
            {
                errors.Add($"Dòng {index} - mã {item.StyleCode}: SizeBreakdownJson không đúng định dạng JSON số lượng theo size.");
            }
        }
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
    }

    private static int ReadQuantity(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var quantity) && quantity >= 0
            ? quantity
            : throw new JsonException();
}
