using System.ComponentModel.DataAnnotations;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class UpdateProductMasterDto
{
    [Required(ErrorMessage = "Mã hình thể gốc (Style Code) không được để trống")]
    [MaxLength(50, ErrorMessage = "Mã hình thể tối đa 50 ký tự")]
    public string StyleCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mô tả hàng hóa hải quan không được để trống")]
    [MaxLength(255, ErrorMessage = "Mô tả tối đa 255 ký tự")]
    public string Description { get; set; } = string.Empty;

    [Range(0, 999999999.9999, ErrorMessage = "Đơn giá CMT phải lớn hơn hoặc bằng 0")]
    public decimal UnitPriceCMT { get; set; }

    [Range(0, 999999999.9999, ErrorMessage = "Đơn giá DAP phải lớn hơn hoặc bằng 0")]
    public decimal UnitPriceDAP { get; set; }

    /// <summary>Đơn giá CMT riêng cho hàng Gò không may. Để trống (0) nếu dùng chung với UnitPriceCMT.</summary>
    [Range(0, 999999999.9999, ErrorMessage = "Đơn giá CMT Gò phải lớn hơn hoặc bằng 0")]
    public decimal? UnitPriceCMT_Go { get; set; }

    /// <summary>Đơn giá DAP riêng cho hàng Gò không may. Để trống (0) nếu dùng chung với UnitPriceDAP.</summary>
    [Range(0, 999999999.9999, ErrorMessage = "Đơn giá DAP Gò phải lớn hơn hoặc bằng 0")]
    public decimal? UnitPriceDAP_Go { get; set; }

    [Required(ErrorMessage = "Mã HS Code không được để trống")]
    [MaxLength(30, ErrorMessage = "Mã HS Code tối đa 30 ký tự")]
    public string HsCode { get; set; } = "64041990";

    [Required(ErrorMessage = "Đơn vị tính không được để trống")]
    [MaxLength(30, ErrorMessage = "Đơn vị tính tối đa 30 ký tự")]
    public string Unit { get; set; } = "đôi";

    [Range(1, 1000, ErrorMessage = "Số đôi / thùng (Pair/CTN) phải từ 1 đến 1000")]
    public int PairPerCarton { get; set; } = 12;

    public int? FolderId { get; set; }
}
