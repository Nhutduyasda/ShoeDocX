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
    public bool HasCmt { get; set; }
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
    public bool CustomsHasCmt { get; set; }
    public decimal CustomsPriceCmt { get; set; }
    public bool IsQuantityMatched => DifferenceQuantity == 0;
    public bool IsPriceMatched => Math.Round(InvoicePriceDap, 4, MidpointRounding.AwayFromZero) == Math.Round(CustomsPriceDap, 4, MidpointRounding.AwayFromZero) && ((!CustomsHasCmt && CustomsPriceCmt == 0) || Math.Round(InvoicePriceCmt, 4, MidpointRounding.AwayFromZero) == Math.Round(CustomsPriceCmt, 4, MidpointRounding.AwayFromZero));
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
    public bool IsInvoiceMismatch { get; set; }
    public string? InvoiceMismatchWarning { get; set; }
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
    public int OrderId { get; set; }
    public string DeclarationNo { get; set; } = string.Empty;
    public DateTime? ClearanceDate { get; set; }
    public string? CustomsDeclarationType { get; set; }
    public int? CustomsChannel { get; set; }
    public string? CustomsOffice { get; set; }
    public int? CustomsPackageQty { get; set; }
    public int? PackageQty { get => CustomsPackageQty; set => CustomsPackageQty = value; }
    public decimal? CustomsGrossWeight { get; set; }
    public decimal? GrossWeight { get => CustomsGrossWeight; set => CustomsGrossWeight = value; }
    public decimal? CustomsTotalDap { get; set; }
    public decimal? TotalDap { get => CustomsTotalDap; set => CustomsTotalDap = value; }
    public decimal? CustomsTotalCmt { get; set; }
    public decimal? TotalCmt { get => CustomsTotalCmt; set => CustomsTotalCmt = value; }
    public string? TempAttachmentFileName { get; set; }
    public bool IsFullyMatched { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? CustomsFile { get; set; }
}

/// <summary>
/// Node trong Cây Thư Mục Lưu Trữ Hồ Sơ Hải Quan
/// Cấu trúc phân cấp: Root -> Đối tác -> Năm -> Hợp đồng -> Luồng xử lý / Chờ đối soát
/// </summary>
public class CustomsArchiveTreeNodeDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Count { get; set; }
    public int? PartnerFolderId { get; set; }
    public string? PartnerName { get; set; }
    public int? Year { get; set; }
    public string? ContractNo { get; set; }
    public string? FilterType { get; set; } // "Green", "Yellow", "Red", "Pending", "Contract", "Year", "Partner", "All"
    public int? Channel { get; set; }
    public string? CustomsStatus { get; set; }
    public List<CustomsArchiveTreeNodeDto> Children { get; set; } = new();
}

/// <summary>
/// Bộ lọc danh sách hồ sơ tờ khai hải quan (GET /api/customs/declarations)
/// </summary>
public class CustomsDeclarationFilterDto
{
    public int? PartnerFolderId { get; set; }
    public int? Year { get; set; }
    public string? ContractNo { get; set; }
    public string? CustomsStatus { get; set; } // "Pending", "Cleared"
    public int? Channel { get; set; } // 1, 2, 3
    public string? Keyword { get; set; }
}

/// <summary>
/// DTO tóm tắt thông tin hồ sơ tờ khai cho danh sách và bảng hiển thị
/// </summary>
public class CustomsDeclarationSummaryDto
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string? PoSuffix { get; set; }
    public int? ContractFolderId { get; set; }
    public string? ContractNo { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? DeliveryTerms { get; set; }
    public string? PaymentTerms { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? DeclarationNo { get; set; }
    public DateTime? ClearanceDate { get; set; }
    public string? CustomsDeclarationType { get; set; }
    public int? CustomsChannel { get; set; }
    public string? CustomsOffice { get; set; }
    public int? CustomsPackageQty { get; set; }
    public decimal? CustomsGrossWeight { get; set; }
    public decimal? CustomsTotalDap { get; set; }
    public decimal? CustomsTotalCmt { get; set; }
    public string? CustomsAttachmentFileName { get; set; }
    public string? CustomsAttachmentFilePath { get; set; }
    public bool HasCustomsAttachment { get; set; }
    public bool IsLocked { get; set; }
    public int ItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalAmountCMT { get; set; }
    public decimal TotalAmountDAP { get; set; }
    public int TotalCartons { get; set; }
}


