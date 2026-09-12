using Microsoft.AspNetCore.Identity;

namespace ShoeExportInvoice.Api.Models.Entities;

public enum Department
{
    Admin = 0,
    Xnk = 1,
    Kho = 2,
    KeToan = 3
}

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public Department Department { get; set; } = Department.Xnk;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
