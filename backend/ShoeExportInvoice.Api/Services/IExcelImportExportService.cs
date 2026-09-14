using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public interface IExcelImportExportService
{
    Task<ImportPreviewResponseDto> PreviewProductMastersFromExcelAsync(Stream fileStream, int? folderId = null);
    Task<ImportResultDto> ImportProductMastersFromExcelAsync(Stream fileStream, bool updateExisting = true, int? folderId = null, ColumnMappingOverrideDto? mappingOverride = null);
    byte[] GenerateProductMasterTemplate();
    Task<byte[]> ExportProductMastersToExcelAsync();
    Task<byte[]> ExportShipmentToExcelAsync(ShipmentExportModel model);
    PklPreviewResponseDto CalculatePklBreakdown(CreateShipmentRequestDto request);

    /// <summary>
    /// Xuất file Excel đa sheet (INV, PKL, Sheet2) từ file mẫu.
    /// Sheet2 chứa ProductMaster thuộc đúng folder đối tác hiện tại và toàn bộ cây folder con.
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
