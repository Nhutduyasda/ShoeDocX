using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public class CurrentTenantService : ICurrentTenantService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
    }

    public Guid TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return DefaultTenantId;
            }

            // 1. Kiểm tra header X-Tenant-Id
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerVal) &&
                Guid.TryParse(headerVal.FirstOrDefault(), out var headerGuid) &&
                headerGuid != Guid.Empty)
            {
                return headerGuid;
            }

            // 2. Kiểm tra JWT Claim tenant_id
            var claimVal = httpContext.User.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrWhiteSpace(claimVal) &&
                Guid.TryParse(claimVal, out var claimGuid) &&
                claimGuid != Guid.Empty)
            {
                return claimGuid;
            }

            return DefaultTenantId;
        }
    }

    public async Task<TenantWorkspace> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var workspace = await db.TenantWorkspaces
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == tenantId, cancellationToken);

        if (workspace == null)
        {
            workspace = new TenantWorkspace
            {
                Id = tenantId,
                CompanyName = tenantId == DefaultTenantId ? "Công ty Mặc Định (Hải An / Kingmaker)" : $"Doanh Nghiệp {tenantId.ToString()[..8]}",
                TaxCode = "1800123456",
                AiCredits = 2,
                CreatedAt = DateTime.UtcNow
            };

            db.TenantWorkspaces.Add(workspace);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // Xử lý race condition nếu có 2 luồng cùng tạo workspace cùng lúc
                var existing = await db.TenantWorkspaces
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(w => w.Id == tenantId, cancellationToken);
                if (existing != null)
                {
                    return existing;
                }
            }
        }

        return workspace;
    }
}
