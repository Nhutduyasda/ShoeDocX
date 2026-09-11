using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("CustomsSettlementPeriods")]
public class CustomsSettlementPeriod
{
    [Key]
    public int Id { get; set; }

    public int Year { get; set; }                           // Năm quyết toán (vd: 2026)

    public DateTime FromDate { get; set; }                 // Từ ngày

    public DateTime ToDate { get; set; }                   // Đến ngày

    [MaxLength(100)]
    public string? ContractNo { get; set; }                 // Hợp đồng gia công (nếu có)

    [MaxLength(200)]
    public string CustomsOffice { get; set; } = "Chi cục Hải quan Quản lý Hàng gia công"; // Chi cục Hải quan quản lý

    [MaxLength(50)]
    public string Status { get; set; } = "Draft";          // Draft (Nháp), Finalized (Đã chốt)

    [MaxLength(255)]
    public string? CompanyName { get; set; }                // Tên tổ chức/doanh nghiệp

    [MaxLength(50)]
    public string? TaxCode { get; set; }                    // Mã số thuế

    [MaxLength(500)]
    public string? Address { get; set; }                    // Địa chỉ

    [MaxLength(1000)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public List<CustomsSettlementItem> Items { get; set; } = new();
}
