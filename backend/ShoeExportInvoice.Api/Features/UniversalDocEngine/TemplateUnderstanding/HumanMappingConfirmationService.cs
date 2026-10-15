using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;

public sealed class HumanMappingConfirmationService
{
    // Authentication, tenant access and reviewer authorization MUST be enforced by the
    // trusted integrating caller. This isolated domain service cannot authenticate names.
    public MappingConfirmationResult Confirm(Stream template, string filename, MappingProposal proposal,
        DocumentWorkflowProfile profile, string reviewerId)
    {
        if (string.IsNullOrWhiteSpace(reviewerId) || reviewerId.Length > 200)
            return new(null, ImmutableArray.Create(UnderstandingIdentity.Error("NAMED_MAPPING_REVIEWER_REQUIRED")));
        var validation = new MappingProposalValidator().Validate(template, filename, proposal, profile);
        return validation.IsValid
            ? new(new HumanMappingConfirmation(reviewerId, proposal), validation.Errors)
            : new(null, validation.Errors);
    }

    public ConfirmedLayoutResult Convert(Stream template, string filename, MappingProposal proposal,
        DocumentWorkflowProfile profile, HumanMappingConfirmation? confirmation)
    {
        if (confirmation is null)
            return new(null, ImmutableArray.Create(UnderstandingIdentity.Error("MAPPING_CONFIRMATION_REQUIRED")));
        var validation = new MappingProposalValidator().Validate(template, filename, proposal, profile);
        if (!validation.IsValid) return new(null, validation.Errors);
        if (confirmation.TemplateSha256 != proposal.TemplateSha256 ||
            confirmation.ProfileFingerprint != WorkflowProfileIdentity.Fingerprint(profile) ||
            confirmation.ProposalRevision != proposal.Revision || confirmation.ProposalFingerprint != proposal.Fingerprint() ||
            confirmation.MappingFingerprint != proposal.Layout.Fingerprint())
            return new(null, ImmutableArray.Create(UnderstandingIdentity.Error("MAPPING_CONFIRMATION_STALE")));
        // Returns only an immutable Gate 2 layout. Binding it and reviewing a business
        // document are explicit, independent operations, never performed here.
        return new(proposal.Layout, ImmutableArray<ExcelProjectionError>.Empty);
    }
}
