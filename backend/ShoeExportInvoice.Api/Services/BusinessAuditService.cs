using System.Security.Claims;
using System.Text.Json;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public interface IBusinessAuditService
{
    void Add(HttpContext http, string action, string resourceType, object resourceId,
        object? previous = null, object? next = null, string? reason = null);
}

public sealed class BusinessAuditService : IBusinessAuditService
{
    private readonly AppDbContext _db;
    public BusinessAuditService(AppDbContext db) => _db = db;

    public void Add(HttpContext http, string action, string resourceType, object resourceId,
        object? previous = null, object? next = null, string? reason = null)
    {
        _db.BusinessAuditLogs.Add(new BusinessAuditLog
        {
            ActorUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorUserName = http.User.Identity?.Name ?? "Unknown",
            ActorRole = http.User.FindFirstValue(ClaimTypes.Role) ?? "Unknown",
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId.ToString() ?? string.Empty,
            PreviousStateJson = previous == null ? null : JsonSerializer.Serialize(previous),
            NewStateJson = next == null ? null : JsonSerializer.Serialize(next),
            Reason = reason,
            TraceId = http.TraceIdentifier,
            CreatedAt = DateTime.UtcNow
        });
    }
}
