using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

public sealed class StudioFault(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed record UniversalDocActor(string UserId, string TenantId, string Role, DateTimeOffset IdentityExpiresAt)
{
    public bool CanReview => Role is "Admin" or "KeToan";
    public bool CanApprove => Role == "Admin";
}
public enum WorkflowMappingState { NoTemplate, DraftSuggested, UserConfirmed }
// Application-only snapshot. Never bind this type from a request or return it from a controller.
public sealed record UniversalDocSession(string Id, string OwnerId, string TenantId, string Workflow,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, long Version,
    DocumentWorkflowProfile MappingProfile, DocumentWorkflowProfile Profile,
    ImmutableArray<byte> Template, string? TemplateSha256, TemplateStructure? Structure,
    MappingSuggestionResult? Suggestions, MappingProposal? Proposal, HumanMappingConfirmation? Confirmation,
    WorkflowMappingState MappingState, ExtractionResult? Extraction, CanonicalDocument? Document,
    ValidationResult? Validation)
{
    public ExampleSchemaDraft? SchemaDraft { get; init; }
    public ImmutableArray<byte> Example { get; init; } = ImmutableArray<byte>.Empty;
    public ImmutableArray<StudioSource> Sources { get; init; } = ImmutableArray<StudioSource>.Empty;
}
public sealed record StudioFieldView(string Scope, string Key, int? Row, string Type, string? Value,
    string Review, string Source, string? SourceId, int? Page, NormalizedRegion? Region,
    ImmutableArray<string> RawObservations);
public sealed record StudioCandidate(string Sheet, string LabelCell, string? Cell, int? Column, int? PrototypeRow);
public sealed record StudioSuggestion(string Scope, string Key, string Status, ImmutableArray<StudioCandidate> Candidates);
public sealed record StudioMapping(string Sheet, int StartRow, ImmutableDictionary<string, string> Headers,
    ImmutableDictionary<string, int> Columns);
public sealed record StudioIssueView(string Code, string? Field, string? RowId, int? RowPosition);
public sealed record StudioSessionView(string Id, long Version, DateTimeOffset ExpiresAt, string Workflow,
    string MappingState, long? MappingRevision, int ProfileVersion, long? DataRevision, string ReviewState,
    ImmutableArray<string> Sheets, ImmutableArray<StudioSuggestion> Suggestions, StudioMapping? Mapping,
    ImmutableArray<StudioFieldView> Fields, ImmutableArray<StudioIssueView> Issues, bool CanReview, bool CanApprove)
{
    public ExampleSchemaDraft? SchemaDraft { get; init; }
    public ImmutableArray<StudioSourceView> Sources { get; init; } = ImmutableArray<StudioSourceView>.Empty;
}
public sealed record StudioExport(byte[] Workbook, long SessionVersion)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string Filename = "synthetic-document.xlsx";
}
