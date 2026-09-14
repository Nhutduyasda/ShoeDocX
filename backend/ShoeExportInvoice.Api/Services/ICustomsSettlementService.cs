using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public interface ICustomsSettlementService
{
    Task<SettlementReportDto> CalculateSettlementAsync(CalculateSettlementRequestDto request);
    Task<CustomsSettlementPeriod> SaveSettlementPeriodAsync(SaveSettlementPeriodRequestDto request);
    Task<CustomsSettlementPeriod> FinalizeSettlementPeriodAsync(int id);
    Task<List<SettlementPeriodSummaryDto>> GetSettlementPeriodsAsync();
    Task<PagedResultDto<SettlementPeriodSummaryDto>> GetSettlementPeriodsPagedAsync(int page, int pageSize);
    Task<SettlementReportDto?> GetSettlementPeriodByIdAsync(int id);
    Task<byte[]> ExportSettlementExcelAsync(SettlementReportDto report);
    Task<byte[]> ExportSettlementExcelByIdAsync(int id);
    Task<List<SettlementDrillDownItemDto>> GetDrillDownAsync(string productCode, DateTime fromDate, DateTime toDate, string? contractNo = null);
    Task<AnalyticsExportStatsDto> GetExportAnalyticsAsync(int year);
    Task<WarehouseImportResultDto> ImportWarehouseExcelAsync(Stream stream, List<SettlementItemDto> currentItems);
    Task<WarehouseImportResultDto> MatchWarehouseRowsAsync(List<WarehouseDataRowDto> rows, List<SettlementItemDto> currentItems);
}
