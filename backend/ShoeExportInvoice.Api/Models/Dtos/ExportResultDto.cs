namespace ShoeExportInvoice.Api.Models.Dtos;

/// <summary>
/// DTO trả về kết quả sau khi xuất file Excel, gồm thông tin tổng kết để frontend hiển thị thông báo.
/// </summary>
public class ExportResultDto
{
    /// <summary>True nếu đơn hàng được tách thành 2 file (Gò + Thành hình)</summary>
    public bool HasTwoFiles { get; set; }

    // --- File 1: Gò không may ---
    /// <summary>Tên file Gò không may (ví dụ: KM3-26-DH233.xlsx)</summary>
    public string? GoFileName { get; set; }
    /// <summary>Invoice No file Gò không may (ví dụ: KMHD-NEW2026-0233)</summary>
    public string? GoInvoiceNo { get; set; }
    /// <summary>Tổng số đôi Gò không may</summary>
    public int GoTotalQuantity { get; set; }
    /// <summary>Số thứ tự trình tự (chỉ phần số cuối)</summary>
    public int GoSequenceNumber { get; set; }

    // --- File 2: Thành hình (Standard) ---
    /// <summary>Tên file Thành hình (ví dụ: KM3-26-DH234.xlsx)</summary>
    public string? StandardFileName { get; set; }
    /// <summary>Invoice No file Thành hình (ví dụ: KMHD-NEW2026-0234)</summary>
    public string? StandardInvoiceNo { get; set; }
    /// <summary>Tổng số đôi Thành hình</summary>
    public int StandardTotalQuantity { get; set; }
    /// <summary>Số thứ tự trình tự (chỉ phần số cuối)</summary>
    public int StandardSequenceNumber { get; set; }

    // --- File duy nhất (khi chỉ có 1 loại hàng) ---
    /// <summary>Tên file xuất (khi chỉ có 1 loại hàng)</summary>
    public string? SingleFileName { get; set; }
    /// <summary>Invoice No file duy nhất</summary>
    public string? SingleInvoiceNo { get; set; }
    /// <summary>Tổng số đôi file duy nhất</summary>
    public int SingleTotalQuantity { get; set; }
}
