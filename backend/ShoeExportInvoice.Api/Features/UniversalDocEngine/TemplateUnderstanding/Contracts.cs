using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;

public sealed record TemplateCell(string Address, int Row, int Column, string? Label,
    string? Formula, bool Merged, bool Bold, string NumberFormat);
public sealed record HeaderSlot(string LabelAddress, string Label, string ValueAddress);
public sealed record TableRegion(int HeaderRow, ImmutableArray<TemplateCell> Columns,
    ImmutableArray<int> PrototypeRows);
public sealed record WorksheetStructure(string Name, string UsedRange,
    ImmutableArray<TemplateCell> Cells, ImmutableArray<HeaderSlot> HeaderSlots,
    ImmutableArray<TableRegion> Tables, ImmutableArray<int> FooterRows,
    ImmutableArray<string> MergedRanges);
public sealed record TemplateStructure(string TemplateSha256, ImmutableArray<WorksheetStructure> Sheets);
public sealed record TemplateAnalysisResult(TemplateStructure? Structure, ImmutableArray<ExcelProjectionError> Errors)
{
    public bool Success => Structure is not null && Errors.IsEmpty;
}
public sealed record FieldAliases(FieldReference Source, ImmutableArray<string> Labels);
// The trusted integrating caller supplies an approved vocabulary. This is not an authorization token.
public sealed record ApprovedLabelSynonyms(string ApprovalReference, ImmutableArray<FieldAliases> Fields);
public enum SuggestionStatus { UniqueCandidate, Ambiguous, Unresolved }
public sealed record MappingCandidate(string Worksheet, string LabelCell, string MatchedLabel,
    string? Cell, int? Column, int? PrototypeRow, string Evidence, string Explanation);
public sealed record MappingSuggestion(FieldReference Source, SuggestionStatus Status,
    ImmutableArray<MappingCandidate> Candidates)
{
    public bool RequiresHumanReview => true;
}
public sealed record MappingSuggestionResult(string TemplateSha256, string ProfileFingerprint,
    ImmutableArray<MappingSuggestion> Suggestions, ImmutableArray<ExcelProjectionError> Errors);

public sealed record MappingProposal(int Version, long Revision, string TemplateSha256,
    string ProfileFingerprint, ExcelTemplateLayout Layout, ImmutableArray<FieldReference> OmittedOptionalFields)
{
    public string Fingerprint() => UnderstandingIdentity.Hash(this);
    public MappingProposal Revise(ExcelTemplateLayout layout, ImmutableArray<FieldReference> omissions) =>
        this with { Revision = checked(Revision + 1), Layout = layout, OmittedOptionalFields = omissions };
    public static MappingProposal Draft(TemplateStructure structure, DocumentWorkflowProfile profile,
        ExcelTemplateLayout layout) => new(1, 1, structure.TemplateSha256,
            WorkflowProfileIdentity.Fingerprint(profile), layout, ImmutableArray<FieldReference>.Empty);
}
public sealed record ProposalValidationResult(ImmutableArray<ExcelProjectionError> Errors)
{
    public bool IsValid => Errors.IsEmpty; // Structural validity never implies confirmation or approval.
}
public sealed class HumanMappingConfirmation
{
    public string ReviewerId { get; }
    public string TemplateSha256 { get; }
    public string ProfileFingerprint { get; }
    public long ProposalRevision { get; }
    public string ProposalFingerprint { get; }
    public string MappingFingerprint { get; }
    internal HumanMappingConfirmation(string reviewer, MappingProposal proposal)
    {
        ReviewerId = reviewer; TemplateSha256 = proposal.TemplateSha256;
        ProfileFingerprint = proposal.ProfileFingerprint; ProposalRevision = proposal.Revision;
        ProposalFingerprint = proposal.Fingerprint(); MappingFingerprint = proposal.Layout.Fingerprint();
    }
}
public sealed record MappingConfirmationResult(HumanMappingConfirmation? Confirmation,
    ImmutableArray<ExcelProjectionError> Errors);
public sealed record ConfirmedLayoutResult(ExcelTemplateLayout? Layout, ImmutableArray<ExcelProjectionError> Errors);

internal static class UnderstandingIdentity
{
    internal static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    internal static string Label(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD).ToLowerInvariant().Replace('đ', 'd');
        return string.Concat(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)));
    }
    internal static ExcelProjectionError Error(string code, string at = "template") => new(code, at, code + ": " + at);
}
