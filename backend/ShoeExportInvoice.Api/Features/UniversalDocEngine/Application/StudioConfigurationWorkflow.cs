using System.Collections.Immutable;
using ClosedXML.Excel;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioValidationRule(string Id, string Kind, string FieldA, string? Operator,
    string? FieldB, string Target, decimal Tolerance, bool Required, string Severity);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioSheetMapping(string Sheet, int PrototypeRow,
    ImmutableDictionary<string, string> Headers, ImmutableDictionary<string, int> Columns);

public sealed partial class UniversalDocWorkflowOrchestrator
{
    private static void VerifyExampleMapping(UniversalDocSession s)
    {
        if (s.Example.IsEmpty) return;
        var analysis = AnalyzeLearning(s.Example.ToArray(), "example.xlsx");
        var sheets = s.Proposal!.Layout.Sheets;
        var correspondence = new ExampleRowCorrespondence().Compare(s.Template.ToArray(), s.Example.ToArray(), s.Structure!, analysis,
            sheets.Select(x => x.Worksheet).ToHashSet(StringComparer.Ordinal));
        if (!correspondence.Supported || correspondence.Sheets.Length != sheets.Length)
            throw new StudioFault(400, "EXAMPLE_SELECTED_SHEETS_MISMATCH");
        if (sheets.Length < 2) return;
        // Require explicit common Text row identifiers, unique and identically ordered on all selected sheets.
        // Equal row counts alone never establish that rows describe the same collection.
        var shared = s.Profile.ExtractionSchema.Fields.Where(f => f.Scope == FieldScope.LineItem && f.Type == FieldType.Text &&
            sheets.All(sheet => sheet.Items.Columns.Count(c => c.Source.Key == f.Key) == 1)).ToArray();
        using var workbook = new XLWorkbook(new MemoryStream(s.Example.ToArray()));
        var count = correspondence.Sheets[0].ExampleRowCount;
        var verified = shared.Any(field =>
        {
            var sequences = sheets.Select(sheet => Enumerable.Range(sheet.Items.StartRow, count)
                .Select(row => workbook.Worksheet(sheet.Worksheet).Cell(row, sheet.Items.Columns.Single(c => c.Source.Key == field.Key).Column))
                .Select(cell => cell.DataType == XLDataType.Text ? cell.GetString() : "").ToArray()).ToArray();
            return sequences[0].All(value => !string.IsNullOrWhiteSpace(value)) && sequences[0].Distinct(StringComparer.Ordinal).Count() == count &&
                sequences.Skip(1).All(values => values.SequenceEqual(sequences[0], StringComparer.Ordinal));
        });
        if (!verified) throw new StudioFault(400, "EXAMPLE_SHARED_ORDERED_IDENTIFIER_REQUIRED");
    }
    private static ValidationPolicy ValidateStudioRules(DocumentWorkflowProfile profile,
        ImmutableArray<StudioValidationRule> rules, ImmutableArray<FieldReference> omissions)
    {
        if (rules.IsDefault || rules.Length > 8 || rules.Any(r => r is null || r.Id is null ||
            !Regex.IsMatch(r.Id, "^[a-zA-Z][a-zA-Z0-9_]{0,63}$") || r.Id is "schema" or "identity" or "field" || r.Kind is not ("Arithmetic" or "CrossFoot") ||
            r.FieldA is null || r.FieldA.Length > 64 || r.Target is null || r.Target.Length > 64 || r.FieldB?.Length > 64 ||
            r.Tolerance is < 0 or > 1 || r.Severity is not ("BlockingError" or "Warning") ||
            (r.Kind == "Arithmetic" && (r.Operator is not ("Add" or "Subtract" or "Multiply" or "Divide") || r.FieldB is null)) ||
            (r.Kind == "CrossFoot" && (r.Operator is not null || r.FieldB is not null))))
            throw new StudioFault(400, "INVALID_VALIDATION_RULES");
        if (rules.Select(r => r.Id).Distinct().Count() != rules.Length ||
            rules.Select(r => (r.Kind, r.FieldA, r.Operator, r.FieldB, r.Target)).Distinct().Count() != rules.Length ||
            rules.GroupBy(r => (r.Kind, r.Target)).Any(g => g.Count() > 1))
            throw new StudioFault(400, "DUPLICATE_OR_CONFLICTING_RULE");
        FieldReference Ref(FieldScope scope, string key)
        {
            var reference = new FieldReference(scope, key);
            if (omissions.Contains(reference) || !profile.ExtractionSchema.Fields.Any(f => f.Scope == scope && f.Key == key && f.Type == FieldType.Decimal))
                throw new StudioFault(400, "INVALID_RULE_FIELD");
            return reference;
        }
        var arithmetic = rules.Where(r => r.Kind == "Arithmetic").Select(r => new ArithmeticCheck(r.Id,
            Ref(FieldScope.LineItem, r.FieldA), Ref(FieldScope.LineItem, r.FieldB!), Enum.Parse<ArithmeticOperator>(r.Operator!),
            Ref(FieldScope.LineItem, r.Target), new(r.Tolerance), r.Required, Enum.Parse<IssueSeverity>(r.Severity))).ToImmutableArray();
        var totals = rules.Where(r => r.Kind == "CrossFoot").Select(r => new CrossFootCheck(r.Id,
            Ref(FieldScope.LineItem, r.FieldA), Ref(FieldScope.Header, r.Target), new(r.Tolerance), r.Required, Enum.Parse<IssueSeverity>(r.Severity))).ToImmutableArray();
        var policy = profile.ValidationPolicy with { Arithmetic = arithmetic, CrossFoot = totals };
        if (new DocumentValidator().ValidateConfiguration(profile with { ValidationPolicy = policy }).Issues.Length != 0)
            throw new StudioFault(400, "INVALID_VALIDATION_POLICY");
        return policy;
    }
    public StudioSessionView ChangeValidationRules(string id, long version, ImmutableArray<StudioValidationRule> rules)
    {
        var actor = Actor();
        return View(store.Update(actor, id, version, s =>
        {
            RequireTemplate(s);
            if (s.Workflow != "example-template" || s.Proposal is null || s.SchemaDraft is null) throw new StudioFault(409, "SCHEMA_CORRECTION_REQUIRED");
            var policy = ValidateStudioRules(s.MappingProfile, rules, s.Proposal.OmittedOptionalFields);
            if (s.ValidationRules.SequenceEqual(rules)) return s;
            var profile = s.MappingProfile with { Version = checked(s.Profile.Version + 1), ValidationPolicy = policy,
                ExcelProjectionMap = s.MappingProfile.ExcelProjectionMap with { ApprovedLayoutFingerprint = null } };
            return ReviseConfiguration(s, profile, s.Proposal.Layout) with { ValidationRules = rules };
        }), actor);
    }
    public StudioSessionView ChangeMultisheetMapping(string id, long version, ImmutableArray<StudioSheetMapping> mappings)
    {
        var actor = Actor();
        if (mappings.IsDefaultOrEmpty || mappings.Length > 3 || mappings.Any(m => m is null ||
            string.IsNullOrWhiteSpace(m.Sheet) || m.Sheet.Length > 31 || m.PrototypeRow is < 1 or > 1000 ||
            m.Headers is null || m.Columns is null || m.Headers.Count > 32 || m.Columns.Count is < 1 or > 32 ||
            m.Headers.Any(h => h.Key.Length > 64 || h.Value is null || h.Value.Length > 12) ||
            m.Columns.Any(c => c.Key.Length > 64 || c.Value is < 1 or > 32)))
            throw new StudioFault(400, "INVALID_MULTISHEET_MAPPING");
        return View(store.Update(actor, id, version, s =>
        {
            RequireTemplate(s);
            if (s.Workflow != "example-template" || s.Proposal is null || s.SchemaDraft is null) throw new StudioFault(409, "SCHEMA_CORRECTION_REQUIRED");
            var layout = new ExcelTemplateLayout(1, s.TemplateSha256!, mappings.Select(m => new ExcelSheetLayout(m.Sheet,
                m.Headers.OrderBy(x => x.Key, StringComparer.Ordinal).Select(h => new ExcelCellMapping(new(FieldScope.Header, h.Key), h.Value)).ToImmutableArray(),
                new(m.PrototypeRow, m.PrototypeRow, 20, m.Columns.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(c => new ExcelColumnMapping(new(FieldScope.LineItem, c.Key), c.Value)).ToImmutableArray()))).ToImmutableArray());
            var profile = s.MappingProfile with { Version = checked(s.Profile.Version + 1),
                ExcelProjectionMap = s.MappingProfile.ExcelProjectionMap with { ApprovedLayoutFingerprint = null } };
            return ReviseConfiguration(s, profile, layout);
        }), actor);
    }
    private static UniversalDocSession ReviseConfiguration(UniversalDocSession s, DocumentWorkflowProfile profile, ExcelTemplateLayout layout)
    {
        var proposal = s.Proposal! with { Revision = checked(s.Proposal!.Revision + 1), ProfileFingerprint = WorkflowProfileIdentity.Fingerprint(profile), Layout = layout };
        using var stream = Template(s);
        if (!new MappingProposalValidator().Validate(stream, "template.xlsx", proposal, profile).IsValid) throw new StudioFault(400, "INVALID_WORKFLOW_MAPPING");
        var document = s.Document?.WithProfileVersion(profile.Version);
        return s with { MappingProfile = profile, Profile = profile, Proposal = proposal, Confirmation = null,
            MappingState = WorkflowMappingState.DraftSuggested, Extraction = null, Document = document,
            Validation = document is null ? null : new DocumentValidator().Validate(document, profile),
            SchemaDraft = s.SchemaDraft! with { Revision = checked(s.SchemaDraft!.Revision + 1), State = "ReadyForConfirmation",
                ProfileVersion = profile.Version, ProfileFingerprint = WorkflowProfileIdentity.Fingerprint(profile) } };
    }
}
