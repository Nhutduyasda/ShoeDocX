namespace ShoeExportInvoice.Api.Models.Entities;

public enum ShipmentStatus
{
    Draft = 0,        // Bản nháp
    Exported = 1,     // Chờ thông quan (đã xuất file Excel)
    Cleared = 2,      // Đã thông quan (khớp tờ khai hải quan)
    Discrepancy = 3   // Sai lệch hồ sơ hải quan
}
