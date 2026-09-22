using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public sealed class MergeShipmentRequestDto
{
    public List<int> SourceBatchIds { get; set; } = [];
    public List<OcrDispatchSourceDocumentDto> SourceDocuments { get; set; } = [];
    public int? ContractFolderId { get; set; }
    public string? PoSuffix { get; set; }
    public string? InvoiceNo { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public int? TemplateId { get; set; }
}

public sealed class MergeShipmentPreviewResponseDto
{
    public bool IsExportable { get; set; }
    public int GeneratedDocumentCount { get; set; }
    public int SourceItemCount { get; set; }
    public int MergedItemCount => MergedItems.Count;
    public int ConsolidatedItemCount => Math.Max(0, SourceItemCount - MergedItems.Count);
    public int TotalQuantity { get; set; }
    public int TotalCartons { get; set; }
    public List<CreateShipmentItemDto> MergedItems { get; set; } = [];
    public List<PklBreakdownItemDto> PklBreakdown { get; set; } = [];
    public List<DispatchValidationMessageDto> Warnings { get; set; } = [];
    public List<DispatchValidationMessageDto> BlockingErrors { get; set; } = [];
    public List<ProcessGroupPreviewDto> ProcessGroups { get; set; } = [];
}

public sealed class ProcessGroupPreviewDto
{
    public ProcessType ProcessType { get; set; }
    public int ItemCount { get; set; }
    public int TotalQuantity { get; set; }
}

public sealed class SplitShipmentRequestDto
{
    public int? SourceBatchId { get; set; }
    public OcrDispatchSourceDocumentDto? SourceDocument { get; set; }
    public List<OcrDispatchSourceDocumentDto> SourceDocuments { get; set; } = [];
    public int? ContractFolderId { get; set; }
    public int? TemplateId { get; set; }
    public string? PoSuffix { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public List<SubInvoiceAllocationDto> SubInvoices { get; set; } = [];
}

public sealed class ValidateSplitRequestDto
{
    public int? SourceBatchId { get; set; }
    public OcrDispatchSourceDocumentDto? SourceDocument { get; set; }
    public List<OcrDispatchSourceDocumentDto> SourceDocuments { get; set; } = [];
    public List<SubInvoiceAllocationDto> SubInvoices { get; set; } = [];
}

public sealed class OcrDispatchSourceDocumentDto
{
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ClientFileId { get; set; }
    public string? SourceFileName { get; set; }
    public int? ReportedTotal { get; set; }
    public int CalculatedTotal { get; set; }
    public List<CreateShipmentItemDto> Items { get; set; } = [];
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

public sealed class DispatchBusinessException(string code, string message, IReadOnlyList<DispatchValidationMessageDto>? details = null)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public IReadOnlyList<DispatchValidationMessageDto> Details { get; } = details ?? [];
}
