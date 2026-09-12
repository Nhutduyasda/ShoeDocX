using System.ComponentModel.DataAnnotations;

namespace ShoeExportInvoice.Api.Services;

public sealed class XnkOptions
{
    [Required] public string CustomsStoragePath { get; set; } = "Uploads/Customs";
    [Required] public string ShipmentTemplatePath { get; set; } = "Templates/Shipment_Template.xlsx";
    [Range(1, 104857600)] public long MaxUploadBytes { get; set; } = 20971520;
    [Required] public string InvoicePrefix { get; set; } = "KMHD-NEW2026-0";
    [Required] public string FilePrefix { get; set; } = "KM3-26-DH";
    [Range(1, int.MaxValue)] public int FirstInvoiceNumber { get; set; } = 233;
    public string[] AllowedOrigins { get; set; } = ["http://localhost:5173", "http://127.0.0.1:5173"];

    // Cấu hình thông tin pháp lý & đối tác mặc định (Zero-hardcode)
    public string DefaultCompanyName { get; set; } = "CÔNG TY TNHH HẢI AN NEW MATERIAL HẬU GIANG";
    public string DefaultTaxCode { get; set; } = "4300326888";
    public string DefaultAddress { get; set; } = "KCN VSIP Quảng Ngãi, Xã Tịnh Phong, Huyện Sơn Tịnh, Tỉnh Quảng Ngãi";
    public string DefaultCustomsOffice { get; set; } = "Chi cục Hải quan Quản lý Hàng gia công";
    public string DefaultCustomerName { get; set; } = "CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR";
    public string DefaultContractNo { get; set; } = "KM-HANEW/01-2025";
    public string DefaultPoSuffix { get; set; } = "(KM3.PO5.26)";
}

public sealed class OcrOptions
{
    public string? ApiKey { get; set; }
    [Required] public string Model { get; set; } = "gpt-4o-mini";
    [Required, Url] public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
}

public sealed class DatabaseOptions
{
    [Required] public string DefaultConnection { get; set; } = "";
}
