using System.ComponentModel.DataAnnotations;

namespace ShoeExportInvoice.Api.Models.Entities;

public class BusinessAuditLog
{
    public long Id { get; set; }
    [MaxLength(450)] public string? ActorUserId { get; set; }
    [MaxLength(256)] public string ActorUserName { get; set; } = "Unknown";
    [MaxLength(50)] public string ActorRole { get; set; } = "Unknown";
    [MaxLength(100)] public string Action { get; set; } = string.Empty;
    [MaxLength(100)] public string ResourceType { get; set; } = string.Empty;
    [MaxLength(100)] public string ResourceId { get; set; } = string.Empty;
    public string? PreviousStateJson { get; set; }
    public string? NewStateJson { get; set; }
    [MaxLength(1000)] public string? Reason { get; set; }
    [MaxLength(100)] public string? TraceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RevokedJwt
{
    public long Id { get; set; }
    [MaxLength(100)] public string Jti { get; set; } = string.Empty;
    [MaxLength(450)] public string? UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime RevokedAt { get; set; } = DateTime.UtcNow;
}
