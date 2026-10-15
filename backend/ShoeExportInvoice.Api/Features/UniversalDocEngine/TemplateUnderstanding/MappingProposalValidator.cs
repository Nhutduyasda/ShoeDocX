using System.Collections.Immutable;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;

public sealed class MappingProposalValidator
{
    public ProposalValidationResult Validate(Stream template, string filename, MappingProposal proposal,
        DocumentWorkflowProfile profile)
    {
        var input = TemplateWorkbookReader.Read(template, filename);
        if (input.Bytes is null) return new(input.Errors);
        var errors = new List<ExcelProjectionError>();
        void Error(string code, string at = "proposal") => errors.Add(UnderstandingIdentity.Error(code, at));
        try
        {
            if (proposal.Version != 1 || proposal.Revision < 1 || proposal.OmittedOptionalFields.IsDefault)
                Error("INVALID_PROPOSAL_VERSION");
            if (proposal.ProfileFingerprint != WorkflowProfileIdentity.Fingerprint(profile)) Error("PROFILE_IDENTITY_MISMATCH");
            var identity = ExcelTemplateLayout.TemplateIdentity(input.Bytes);
            if (identity != proposal.TemplateSha256 || identity != proposal.Layout.TemplateSha256) Error("TEMPLATE_IDENTITY_MISMATCH");
            if (new DocumentValidator().ValidateConfiguration(profile).Issues.Any(i => i.Severity == IssueSeverity.BlockingError))
                Error("INVALID_WORKFLOW_PROFILE");
            if (errors.Count != 0) return new(errors.ToImmutableArray());
            using var analysisStream = new MemoryStream(input.Bytes);
            var analysis = new TemplateStructureAnalyzer().Analyze(analysisStream, filename);
            if (!analysis.Success) return new(analysis.Errors);
            using var workbook = new XLWorkbook(new MemoryStream(input.Bytes));
            // A local validation-only shadow satisfies Gate 2's binding check. It is never
            // returned/persisted, and does not approve a layout, document or profile.
            var validationProfile = profile with { ExcelProjectionMap = profile.ExcelProjectionMap with {
                ApprovedLayoutFingerprint = proposal.Layout.Fingerprint() } };
            errors.AddRange(new ExcelLayoutValidator().Validate(workbook, proposal.Layout, validationProfile, 1));
            if (errors.Count != 0) return new(errors.ToImmutableArray());
            var mapped = new HashSet<FieldReference>();
            foreach (var sheet in proposal.Layout.Sheets)
            {
                var structure = analysis.Structure!.Sheets.Single(s => s.Name == sheet.Worksheet);
                var regions = structure.Tables.Where(t => t.PrototypeRows.Contains(sheet.Items.StartRow)).ToArray();
                if (regions.Length != 1 || regions[0].PrototypeRows.Length != 1)
                    Error("AMBIGUOUS_OR_INVALID_PROTOTYPE", sheet.Worksheet);
                foreach (var mapping in sheet.Headers) mapped.Add(mapping.Source);
                foreach (var column in sheet.Items.Columns) mapped.Add(column.Source);
            }
            var definitions = profile.ExtractionSchema.Fields;
            var omitted = proposal.OmittedOptionalFields;
            if (omitted.Distinct().Count() != omitted.Length) Error("DUPLICATE_OMISSION");
            foreach (var reference in omitted)
            {
                var definition = definitions.FirstOrDefault(f => f.Scope == reference.Scope && f.Key == reference.Key);
                if (definition is null || definition.Required || mapped.Contains(reference)) Error("INVALID_OMISSION", reference.ToString());
            }
            foreach (var definition in definitions)
            {
                var reference = new FieldReference(definition.Scope, definition.Key);
                if (!mapped.Contains(reference) && !omitted.Contains(reference)) Error("UNRESOLVED_FIELD", reference.ToString());
            }
            return new(errors.ToImmutableArray());
        }
        catch (Exception ex) when (TemplateWorkbookReader.KnownFailure(ex))
        { return new(ImmutableArray.Create(UnderstandingIdentity.Error("INVALID_PROPOSAL_OR_WORKBOOK"))); }
    }
}
