using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public interface IExcelImportExportService
{
    Task<ImportResultDto> ImportProductMastersFromExcelAsync(Stream fileStream, bool updateExisting = true);
    byte[] GenerateProductMasterTemplate();
    Task<byte[]> ExportProductMastersToExcelAsync();
    Task<byte[]> ExportShipmentToExcelAsync(ShipmentExportModel model);
    PklPreviewResponseDto CalculatePklBreakdown(CreateShipmentRequestDto request);

    /// <summary>
    /// Xuất file Excel đa sheet (INV, PKL, Sheet2) từ file mẫu.
    /// Sheet2 luôn chứa toàn bộ ProductMaster (không lọc theo đơn hiện tại).
    /// </summary>
    Task<byte[]> ExportShipmentMultiSheetExcelAsync(CreateShipmentRequestDto request);

    /// <summary>
    /// Xuất 2 file Excel độc lập (1 cho Gò không may, 1 cho Thành hình) và đóng gói thành 1 file ZIP.
    /// </summary>
    Task<byte[]> ExportSplitToZipAsync(
        CreateShipmentRequestDto goRequest,
        string goFileName,
        CreateShipmentRequestDto standardRequest,
        string standardFileName);
}
