using System.ComponentModel.DataAnnotations;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class CreateShipmentRequestDto
{
    [Required(ErrorMessage = "Số hóa đơn (Invoice No) không được để trống")]
    [MaxLength(100)]
    public string InvoiceNo { get; set; } = "KMHD-NEW2026-0233";

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string PoSuffix { get; set; } = "(KM3.PO5.26)";

    [MaxLength(100)]
    public string ContractNo { get; set; } = "KM-HANEW/01-2025";

    [MaxLength(255)]
    public string CustomerName { get; set; } = "CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR";

    [MaxLength(500)]
    public string Address { get; set; } = "SỐ 1, ĐƯỜNG 4A, KCN VIỆT NAM SINGAPORE, XÃ THỌ PHONG, TỈNH QUẢNG NGÃI, VIỆT NAM";

    [MaxLength(100)]
    public string DeliveryTerms { get; set; } = "DAP";

    [MaxLength(100)]
    public string PaymentTerms { get; set; } = "T/T";

    [Required]
    [MinLength(1, ErrorMessage = "Đơn hàng phải có ít nhất 1 mặt hàng")]
    public List<CreateShipmentItemDto> Items { get; set; } = new();
}

public class CreateShipmentItemDto
{
    [Required(ErrorMessage = "Mã hình thể gốc không được để trống")]
    public string StyleCode { get; set; } = string.Empty;

    public string? FullItemCode { get; set; }

    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
    public int Quantity { get; set; }

    public ProcessType ProcessType { get; set; } = ProcessType.Standard;

    public decimal? UnitPriceCMT { get; set; }

    public decimal? UnitPriceDAP { get; set; }

    public string Unit { get; set; } = "đôi";

    public int? PairPerCarton { get; set; }
}
