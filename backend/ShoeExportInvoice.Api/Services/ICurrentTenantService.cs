using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public interface ICurrentTenantService
{
    Guid TenantId { get; }
    Task<TenantWorkspace> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default);
}
