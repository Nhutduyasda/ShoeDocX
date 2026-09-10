using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

/// <summary>
/// Dòng hàng bóc tách từ tờ khai VNACCS
/// </summary>
public class CustomsDeclarationItemDto
{
    public int LineNumber { get; set; }
    public string HsCode { get; set; } = string.Empty;
    public string StyleCode { get; set; } = string.Empty;
    public string RawDescription { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Unit { get; set; } = "đôi";
    public decimal UnitPriceDap { get; set; }
    public decimal AmountDap { get; set; }
    public decimal UnitPriceCmt { get; set; }
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;
}

/// <summary>
/// Kết quả bóc tách thông tin tờ khai từ file .xls VNACCS
/// </summary>
public class CustomsDeclarationParsedDto
{
    public string DeclarationNo { get; set; } = string.Empty;
    public DateTime? ClearanceDate { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string CustomsDeclarationType { get; set; } = string.Empty;
    public int? CustomsChannel { get; set; }
    public string CustomsChannelName => CustomsChannel switch
    {
        1 => "Luồng 1 - Xanh (Thông quan ngay)",
        2 => "Luồng 2 - Vàng (Kiểm tra chứng từ)",
        3 => "Luồng 3 - Đỏ (Kiểm tra thực tế hàng hóa)",
        _ => "Chưa phân luồng"
    };
    public string CustomsOffice { get; set; } = string.Empty;
    public int PackageQty { get; set; }
    public decimal GrossWeight { get; set; }
    public decimal TotalDap { get; set; }
    public decimal TotalCmt { get; set; }
    public string FileName { get; set; } = string.Empty;
    public List<CustomsDeclarationItemDto> Items { get; set; } = new();
    public int TotalItemQuantity => Items.Sum(i => i.Quantity);
}

/// <summary>
/// Dòng so sánh đối soát chéo giữa Invoice nội bộ và Tờ khai hải quan
/// </summary>
public class CustomsComparisonRowDto
{
    public int Index { get; set; }
    public string StyleCode { get; set; } = string.Empty;
    public ProcessType ProcessType { get; set; }
    public int InvoiceQuantity { get; set; }
    public int CustomsQuantity { get; set; }
    public int DifferenceQuantity => CustomsQuantity - InvoiceQuantity;
    public decimal InvoicePriceDap { get; set; }
    public decimal CustomsPriceDap { get; set; }
    public decimal InvoicePriceCmt { get; set; }
    public decimal CustomsPriceCmt { get; set; }
    public bool IsQuantityMatched => DifferenceQuantity == 0;
    public bool IsPriceMatched => Math.Abs(InvoicePriceDap - CustomsPriceDap) < 0.005m;
    public bool IsMatched => IsQuantityMatched && IsPriceMatched;
    public string StatusText { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả đối soát 2 chiều toàn diện giữa Invoice và Tờ khai
/// </summary>
public class CustomsReconciliationResultDto
{
    public bool IsOrderFound { get; set; }
    public bool IsFullyMatched { get; set; }
    public string Message { get; set; } = string.Empty;

    public CustomsDeclarationParsedDto Declaration { get; set; } = new();

    public MatchedOrderSummaryDto? MatchedOrder { get; set; }

    public bool TotalQuantityMatched { get; set; }
    public bool TotalDapMatched { get; set; }
    public bool TotalCmtMatched { get; set; }

    public List<CustomsComparisonRowDto> ComparisonRows { get; set; } = new();
    public List<string> Discrepancies { get; set; } = new();
}

public class MatchedOrderSummaryDto
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string? PoSuffix { get; set; }
    public string? ContractNo { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal TotalAmountDap { get; set; }
    public decimal TotalAmountCmt { get; set; }
    public ShipmentStatus CurrentStatus { get; set; }
}

public class ConfirmCustomsSyncRequestDto
{
    public string DeclarationNo { get; set; } = string.Empty;
    public DateTime? ClearanceDate { get; set; }
    public string? CustomsDeclarationType { get; set; }
    public int? CustomsChannel { get; set; }
    public string? CustomsOffice { get; set; }
    public int? CustomsPackageQty { get; set; }
    public decimal? CustomsGrossWeight { get; set; }
    public decimal? CustomsTotalDap { get; set; }
    public decimal? CustomsTotalCmt { get; set; }
    public string? TempAttachmentFileName { get; set; }
    public bool IsFullyMatched { get; set; }
}
