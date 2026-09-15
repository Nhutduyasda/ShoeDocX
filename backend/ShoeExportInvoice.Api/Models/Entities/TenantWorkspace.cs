namespace ShoeExportInvoice.Api.Models.Entities;

public class TenantWorkspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CompanyName { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public int AiCredits { get; set; } = 2; // Tặng sẵn 2 credit miễn phí
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
