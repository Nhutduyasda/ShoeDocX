using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class DocumentPreviewResponseDto
{
    public InvoicePreviewDto Invoice { get; set; } = new();
    public PklPreviewResponseDto PackingList { get; set; } = new();
}

public class ShipmentFolderCountDto
{
    public int FolderId { get; set; }
    public int Count { get; set; }
}

public class BatchPrintRequestDto
{
    public List<int> ShipmentIds { get; set; } = new();
    public string DocumentType { get; set; } = "ALL";
}

public class BatchPrintResponseDto
{
    public string DocumentType { get; set; } = "ALL";
    public List<DocumentPreviewResponseDto> Documents { get; set; } = new();
}

public class InvoicePreviewDto
{
    public string SellerName { get; set; } = string.Empty;
    public string SellerAddress { get; set; } = string.Empty;
    public string SellerAddressLine1 { get; set; } = string.Empty;
    public string SellerAddressLine2 { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerAddress { get; set; } = string.Empty;
    public string BuyerAddressLine1 { get; set; } = string.Empty;
    public string BuyerAddressLine2 { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string InvoiceDate { get; set; } = string.Empty;
    public string ContractNo { get; set; } = string.Empty;
    public string DeliveryTerms { get; set; } = "DAP";
    public string PaymentTerms { get; set; } = "T/T";
    public string DestinationCountry { get; set; } = "VIETNAM";
    public string PoSuffix { get; set; } = string.Empty;

    public List<InvoicePreviewItemDto> Items { get; set; } = new();

    public int TotalQuantity { get; set; }
    public decimal TotalAmountCMT { get; set; }
    public decimal TotalAmountDAP { get; set; }
    public string TotalAmountDAPInWords { get; set; } = string.Empty;
}

public class InvoicePreviewItemDto
{
    public int LineNo { get; set; }
    public string StyleCode { get; set; } = string.Empty;
    public string FullItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Unit { get; set; } = "đôi";
    public decimal UnitPriceCMT { get; set; }
    public decimal UnitPriceDAP { get; set; }
    public decimal AmountCMT { get; set; }
    public decimal AmountDAP { get; set; }
    public int CartonCount { get; set; }
    public int PairsPerCarton { get; set; } = 12;
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;
}
