namespace ShoeExportInvoice.Api.Models.Dtos;

public class ValidateItemsRequest
{
    public int CurrentPartnerFolderId { get; set; }
    public List<string> StyleCodes { get; set; } = new();
}

public class ItemValidationDetail
{
    public string RawCode { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public bool IsMatchedInCurrent { get; set; }
    public int? MatchedFolderId { get; set; }
    public string? MatchedFolderName { get; set; }
    public ProductMasterDto? MatchedProduct { get; set; }
}

public class ValidateItemsResult
{
    public bool HasMismatch { get; set; }
    public int? SuggestedPartnerFolderId { get; set; }
    public string? SuggestedPartnerName { get; set; }
    public int MatchedCountInSuggested { get; set; }
    public int TotalCodes { get; set; }
    public List<ItemValidationDetail> Details { get; set; } = new();
}
