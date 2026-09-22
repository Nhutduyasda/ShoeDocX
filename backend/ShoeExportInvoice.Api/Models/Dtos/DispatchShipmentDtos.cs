using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public sealed class MergeShipmentRequestDto
{
    public List<int> SourceBatchIds { get; set; } = [];
    public int? ContractFolderId { get; set; }
    public string? PoSuffix { get; set; }
    public string? InvoiceNo { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public int? TemplateId { get; set; }
}

public sealed class MergeShipmentPreviewResponseDto
{
    public int TotalQuantity { get; set; }
    public int TotalCartons { get; set; }
    public List<CreateShipmentItemDto> MergedItems { get; set; } = [];
    public List<PklBreakdownItemDto> PklBreakdown { get; set; } = [];
    public List<DispatchValidationMessageDto> Warnings { get; set; } = [];
}

public sealed class SplitShipmentRequestDto
{
    public int SourceBatchId { get; set; }
    public int? ContractFolderId { get; set; }
    public int? TemplateId { get; set; }
    public string? PoSuffix { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public List<SubInvoiceAllocationDto> SubInvoices { get; set; } = [];
}

public sealed class ValidateSplitRequestDto
{
    public int SourceBatchId { get; set; }
    public List<SubInvoiceAllocationDto> SubInvoices { get; set; } = [];
}

public sealed class SubInvoiceAllocationDto
{
    public string InvoiceSuffixTitle { get; set; } = string.Empty;
    public List<CreateShipmentItemDto> Items { get; set; } = [];
}

public sealed class DispatchValidationMessageDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StyleCode { get; set; }
}

public sealed class ItemAllocationCheckDto
{
    public string StyleCode { get; set; } = string.Empty;
    public ProcessType ProcessType { get; set; }
    public int OriginalQty { get; set; }
    public int AllocatedQty { get; set; }
    public int Discrepancy => AllocatedQty - OriginalQty;
    public bool IsMatched => OriginalQty == AllocatedQty;
}

public sealed class ValidateSplitResultDto
{
    public bool IsValid { get; set; }
    public int OriginalTotal { get; set; }
    public int AllocatedTotal { get; set; }
    public int Discrepancy => AllocatedTotal - OriginalTotal;
    public List<DispatchValidationMessageDto> Errors { get; set; } = [];
    public List<DispatchValidationMessageDto> Warnings { get; set; } = [];
    public List<ItemAllocationCheckDto> ItemChecks { get; set; } = [];
}

public sealed record ExportFileResult(byte[] Content, string ContentType, string FileName);
