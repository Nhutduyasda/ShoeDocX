namespace ShoeExportInvoice.Api.Models.Dtos;

public class ShipmentExportModel
{
    public int? TemplateId { get; set; }
    public string InvoiceNo { get; set; } = "KMHD-NEW2026-0233";
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public string ContractNo { get; set; } = "KM-HANEW/01-2025";
    public string PoSuffix { get; set; } = "(KM3.PO5.26)";
    public string CustomerName { get; set; } = "CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR";
    public string Address { get; set; } = "SỐ 1, ĐƯỜNG 4A, KCN VIỆT NAM SINGAPORE, XÃ THỌ PHONG, TỈNH QUẢNG NGÃI, VIỆT NAM";
    public string DeliveryTerms { get; set; } = "DAP";
    public string PaymentTerms { get; set; } = "T/T";
    public List<ShipmentExportItemModel> Items { get; set; } = new();
}

public class ShipmentExportItemModel
{
    public string StyleCode { get; set; } = string.Empty;
    public string FullItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1200;
    public string Unit { get; set; } = "đôi";
    public decimal UnitPriceCMT { get; set; }
    public decimal UnitPriceDAP { get; set; }
    public int PairPerCarton { get; set; } = 12;
}
