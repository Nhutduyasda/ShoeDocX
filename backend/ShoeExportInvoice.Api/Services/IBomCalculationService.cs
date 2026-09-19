using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public interface IBomCalculationService
{
    Task<MaterialRequirementResultDto> CalculateAsync(
        int productionOrderId,
        CancellationToken cancellationToken = default);
}
