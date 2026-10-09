using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public sealed class CustomsParseException(string message, CustomsParseIssueDto issue)
    : InvalidOperationException(message)
{
    public IReadOnlyList<CustomsParseIssueDto> Details { get; } = [issue];
}
