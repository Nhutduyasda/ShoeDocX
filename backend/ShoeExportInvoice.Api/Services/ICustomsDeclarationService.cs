using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public interface ICustomsDeclarationService
{
    /// <summary>
    /// Bóc tách thông tin tờ khai VNACCS từ file .xls / .xlsx
    /// </summary>
    CustomsDeclarationParsedDto ParseDeclarationFile(Stream fileStream, string fileName);

    /// <summary>
    /// Đối soát chéo 2 chiều giữa dữ liệu tờ khai hải quan và đơn hàng trong CSDL
    /// </summary>
    Task<CustomsReconciliationResultDto> ReconcileAsync(CustomsDeclarationParsedDto declaration, int? specificOrderId = null);

    /// <summary>
    /// Xác nhận đồng bộ thông tin hải quan vào đơn hàng và lưu trữ file tờ khai
    /// </summary>
    Task<ShipmentOrder> ConfirmSyncAsync(int orderId, ConfirmCustomsSyncRequestDto request, Stream? fileStream = null, string? originalFileName = null);

    /// <summary>
    /// Lấy file tờ khai đã đính kèm theo đơn hàng
    /// </summary>
    Task<(byte[] Bytes, string ContentType, string FileName)?> GetAttachmentAsync(int orderId);
}
