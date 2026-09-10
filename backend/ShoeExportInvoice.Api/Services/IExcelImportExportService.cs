using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public interface IExcelImportExportService
{
    Task<ImportResultDto> ImportProductMastersFromExcelAsync(Stream fileStream, bool updateExisting = true);
    byte[] GenerateProductMasterTemplate();
    Task<byte[]> ExportProductMastersToExcelAsync();
    Task<byte[]> ExportShipmentToExcelAsync(ShipmentExportModel model);
    PklPreviewResponseDto CalculatePklBreakdown(CreateShipmentRequestDto request);
    Task<byte[]> ExportShipmentMultiSheetExcelAsync(CreateShipmentRequestDto request);
}
