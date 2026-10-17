using System.Collections.Immutable;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record SchemaFieldCorrection(string Key, string Label, string Scope, string Type, bool Required, bool Omit, int? Candidate, string? Worksheet = null, string? Cell = null, int? Column = null, int? PrototypeRow = null);
public sealed record ManualSample(ImmutableDictionary<string, string?> Header, ImmutableArray<ImmutableDictionary<string, string?>> Rows);

public sealed partial class UniversalDocWorkflowOrchestrator
{
    private static TemplateStructure AnalyzeLearning(byte[] bytes, string filename)
    {
        if (bytes.Length is <= 0 or > InMemoryUniversalDocSessionStore.MaximumTemplateBytes ||
            !filename.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new StudioFault(400, "SUPPORTED_XLSX_REQUIRED");
        using var stream = new MemoryStream(bytes);
        var result = new TemplateStructureAnalyzer().Analyze(stream, filename);
        if (!result.Success) throw new StudioFault(400, "UNSUPPORTED_OR_INVALID_TEMPLATE");
        var structure = result.Structure!;
        if (structure.Sheets.Length > 4 || structure.Sheets.Sum(s => s.Cells.Length) > 2000 ||
            structure.Sheets.Any(s => s.Cells.Any(c => c.Row > 1000 || c.Column > 32 || c.Label?.Length > 2000)))
            throw new StudioFault(400, "STUDIO_TEMPLATE_LIMIT");
        return structure;
    }
    public StudioSessionView UploadLearning(string id, long version, byte[] bytes, string filename, bool isExample)
    {
        var actor = Actor();
        return View(store.Update(actor, id, version, s =>
        {
            if (s.Workflow != "example-template") throw new StudioFault(400, "EXAMPLE_MODE_REQUIRED");
            var structure = AnalyzeLearning(bytes, filename); var learner = new ExampleSchemaLearner();
            if (isExample)
            {
                RequireTemplate(s);
                if (!learner.Corresponds(s.Structure!, structure)) throw new StudioFault(400, "EXAMPLE_STRUCTURE_MISMATCH");
            }
            var template = isExample ? s.Template.ToArray() : bytes;
            var actual = isExample ? s.Structure! : structure;
            ImmutableArray<LearnedField> fields;
            try { fields = learner.Suggest(actual, template, isExample ? bytes : null); }
            catch (ArgumentException) { throw new StudioFault(400, "SCHEMA_CANDIDATE_LIMIT"); }
            var draft = new ExampleSchemaDraft((s.SchemaDraft?.Revision ?? 0) + 1,
                fields.IsEmpty || fields.Any(f => f.Status == "Ambiguous") ? "NeedsCorrection" : "Suggested",
                actual.TemplateSha256, isExample ? structure.TemplateSha256 : null, fields);
            var profile = StudioDemoCatalog.Profile("example-template", actor.TenantId) with { Version = s.Profile.Version + 1,
                ExtractionSchema = new(s.Profile.ExtractionSchema.Version + 1, ImmutableArray<FieldDefinition>.Empty) };
            return s with { Template = template.ToImmutableArray(), TemplateSha256 = actual.TemplateSha256, Structure = actual,
                Example = isExample ? bytes.ToImmutableArray() : ImmutableArray<byte>.Empty, SchemaDraft = draft with { ProfileFingerprint = WorkflowProfileIdentity.Fingerprint(profile), ProfileVersion = profile.Version },
                MappingProfile = profile, Profile = profile, Proposal = null, Confirmation = null,
                Suggestions = null, MappingState = WorkflowMappingState.DraftSuggested, Extraction = null,
                Document = null, Validation = null };
        }), actor);
    }
    public StudioSessionView CorrectSchema(string id, long version, ImmutableArray<SchemaFieldCorrection> corrections)
    {
        var actor = Actor();
        if (corrections.IsDefaultOrEmpty || corrections.Length > 32 || corrections.Any(f => f is null ||
            f.Key is null || !Regex.IsMatch(f.Key, "^[a-z][a-zA-Z0-9_]{0,63}$") ||
            string.IsNullOrWhiteSpace(f.Label) || f.Label.Length > 256 ||
            f.Worksheet?.Length > 31 || f.Cell?.Length > 12 || f.Column is < 1 or > 32 || f.PrototypeRow is < 1 or > 1000 ||
            f.Scope is not ("Header" or "LineItem") || f.Type is not ("Text" or "Decimal" or "Date" or "Boolean") || (f.Omit && f.Required)))
            throw new StudioFault(400, "INVALID_SCHEMA_FIELDS");
        return View(store.Update(actor, id, version, s =>
        {
            RequireTemplate(s); var draft = s.SchemaDraft ?? throw new StudioFault(409, "SCHEMA_REQUIRED");
            if (corrections.Length != draft.Fields.Length || corrections.Select(f => (f.Scope, f.Key)).Distinct().Count() != corrections.Length)
                throw new StudioFault(400, "INVALID_SCHEMA_FIELDS");
            using var evidenceWorkbook = new XLWorkbook(new MemoryStream((s.Example.IsEmpty ? s.Template : s.Example).ToArray()));
            var fields = corrections.Select((f, i) =>
            {
                var evidence = draft.Fields[i];
                if (!f.Omit && f.Worksheet is null && (f.Candidate is null || f.Candidate < 0 || f.Candidate >= evidence.Candidates.Length))
                    throw new StudioFault(400, "UNRESOLVED_SCHEMA_FIELD");
                var choices = evidence.Candidates;
                MappingCandidate? selected = f.Omit || f.Worksheet is not null ? null : choices[f.Candidate!.Value];
                if (!f.Omit && f.Worksheet is not null)
                {
                    var sheet = s.Structure!.Sheets.SingleOrDefault(x => x.Name == f.Worksheet) ?? throw new StudioFault(400, "INVALID_SCHEMA_DESTINATION");
                    var cell = f.Scope == "Header" ? sheet.Cells.SingleOrDefault(c => c.Address == f.Cell) :
                        sheet.Cells.SingleOrDefault(c => c.Column == f.Column && c.Row == f.PrototypeRow);
                    if (cell is null || cell.Formula is not null || cell.Merged ||
                        (f.Scope == "LineItem" && !sheet.Tables.Any(t => t.PrototypeRows.Length == 1 && t.PrototypeRows[0] == f.PrototypeRow)))
                        throw new StudioFault(400, "INVALID_SCHEMA_DESTINATION");
                    selected = new(sheet.Name, cell.Address, f.Label, f.Scope == "Header" ? cell.Address : null,
                        f.Scope == "LineItem" ? cell.Column : null, f.Scope == "LineItem" ? cell.Row : null,
                        cell.Address, "Explicit user destination on a materialized template cell; not inferred source evidence.");
                    if (!choices.Contains(selected))
                    {
                        if (choices.Length >= 32) throw new StudioFault(400, "SCHEMA_CANDIDATE_LIMIT");
                        choices = choices.Add(selected);
                    }
                }
                if (selected is not null && ((f.Scope == "Header") != (selected.Cell is not null)))
                    throw new StudioFault(400, "UNSUPPORTED_SCOPE_DESTINATION");
                if (selected is not null && f.Type != "Text")
                {
                    var cell = evidenceWorkbook.Worksheet(selected.Worksheet).Cell(selected.Cell ?? XLHelper.GetColumnLetterFromNumber(selected.Column!.Value) + selected.PrototypeRow);
                    var raw = cell.DataType == XLDataType.Text ? cell.GetString() : "";
                    if (Regex.IsMatch(raw, "^0[0-9]+$") || (cell.DataType == XLDataType.Number && Regex.IsMatch(cell.Style.NumberFormat.Format, "^0{2,}$")))
                        throw new StudioFault(400, "IDENTIFIER_REQUIRES_TEXT");
                }
                return evidence with { Key = f.Key, Label = f.Label, Scope = f.Scope, Type = f.Type, Required = f.Required,
                    Omit = f.Omit, Candidates = choices, Candidate = selected is null ? null : choices.IndexOf(selected), Status = f.Omit ? "Omitted" : "Resolved" };
            }).ToImmutableArray();
            var active = fields.Where(f => !f.Omit).ToArray();
            if (!active.Any(f => f.Scope == "LineItem")) throw new StudioFault(400, "LINE_REGION_REQUIRED");
            var chosen = active.Select(f => f.Candidates[f.Candidate!.Value]).ToArray();
            if (chosen.Select(c => c.Worksheet).Distinct().Count() != 1) throw new StudioFault(400, "SINGLE_SHEET_EDITOR_REQUIRED");
            var row = chosen.Where(c => c.PrototypeRow.HasValue).Select(c => c.PrototypeRow!.Value).Distinct().ToArray();
            if (row.Length != 1) throw new StudioFault(400, "SINGLE_PROTOTYPE_REQUIRED");
            var definitions = fields.Select(f => new FieldDefinition(f.Key, Enum.Parse<FieldScope>(f.Scope), Enum.Parse<FieldType>(f.Type), f.Required)).ToImmutableArray();
            var profile = s.MappingProfile with { Version = checked(s.Profile.Version + 1), ExtractionSchema = new(s.Profile.ExtractionSchema.Version + 1, definitions),
                ExcelProjectionMap = new(ImmutableArray<ExcelMapping>.Empty) };
            var layout = new ExcelTemplateLayout(1, s.TemplateSha256!, ImmutableArray.Create(new ExcelSheetLayout(chosen[0].Worksheet,
                active.Where(f => f.Scope == "Header").Select(f => new ExcelCellMapping(new(FieldScope.Header, f.Key), f.Candidates[f.Candidate!.Value].Cell!)).ToImmutableArray(),
                new(row[0], row[0], 20, active.Where(f => f.Scope == "LineItem").Select(f => new ExcelColumnMapping(new(FieldScope.LineItem, f.Key), f.Candidates[f.Candidate!.Value].Column!.Value)).ToImmutableArray()))));
            var proposal = new MappingProposal(1, (s.Proposal?.Revision ?? 0) + 1, s.TemplateSha256!, WorkflowProfileIdentity.Fingerprint(profile), layout,
                fields.Where(f => f.Omit).Select(f => new FieldReference(Enum.Parse<FieldScope>(f.Scope), f.Key)).ToImmutableArray());
            using var stream = Template(s);
            var validation = new MappingProposalValidator().Validate(stream, "template.xlsx", proposal, profile);
            if (!validation.IsValid) throw new StudioFault(400, "INVALID_SCHEMA_MAPPING");
            var document = s.Document?.WithProfileVersion(profile.Version);
            return s with { SchemaDraft = draft with { Revision = draft.Revision + 1, State = "ReadyForConfirmation", Fields = fields,
                    ProfileFingerprint = WorkflowProfileIdentity.Fingerprint(profile), ProfileVersion = profile.Version },
                MappingProfile = profile, Profile = profile, Proposal = proposal, Confirmation = null, MappingState = WorkflowMappingState.DraftSuggested,
                Extraction = null, Document = document, Validation = document is null ? null : new DocumentValidator().Validate(document, profile) };
        }), actor);
    }
    public StudioSessionView SetManualSample(string id, long version, ManualSample sample)
    {
        var actor = Actor();
        if (sample.Header is null || sample.Rows.IsDefaultOrEmpty || sample.Rows.Length > 20 || sample.Header.Count > 32 ||
            sample.Rows.Any(r => r is null || r.Count > 32) || sample.Header.Concat(sample.Rows.SelectMany(r => r)).Any(x => x.Key.Length > 64 || x.Value?.Length > 2000))
            throw new StudioFault(400, "SAMPLE_LIMIT");
        return View(store.Update(actor, id, version, s =>
        {
            ConfirmedLayout(s);
            if (s.SchemaDraft is null) throw new StudioFault(400, "EXAMPLE_MODE_REQUIRED");
            ImmutableDictionary<string, TypedFieldValue> Normalize(ImmutableDictionary<string, string?> values, FieldScope scope)
            {
                var result = ImmutableDictionary.CreateBuilder<string, TypedFieldValue>();
                foreach (var (key, raw) in values)
                {
                    var field = s.Profile.ExtractionSchema.Fields.SingleOrDefault(f => f.Scope == scope && f.Key == key) ?? throw new StudioFault(400, "UNKNOWN_FIELD");
                    if (s.Proposal!.OmittedOptionalFields.Contains(new(scope, key))) throw new StudioFault(400, "OMITTED_FIELD");
                    var normalized = new TypedValueNormalizer().Normalize(field.Type, raw, StudioDemoCatalog.Rule(new(scope, key)));
                    if (normalized.Value is null) throw new StudioFault(400, "INVALID_EDITED_VALUE");
                    result[key] = normalized.Value with { Source = FieldSource.Manual, Evidence = null, Review = FieldReviewStatus.Unreviewed };
                }
                return result.ToImmutable();
            }
            var header = Normalize(sample.Header, FieldScope.Header); var rows = sample.Rows.Select(r => Normalize(r, FieldScope.LineItem)).ToImmutableArray();
            var document = s.Document is null ? new CanonicalDocument("manual-" + s.Id, actor.TenantId, s.Profile.ProfileId, s.Profile.Version, header, rows)
                : s.Document.Edit(header, rows);
            return s with { Document = document, Extraction = null, Validation = new DocumentValidator().Validate(document, s.Profile) };
        }), actor);
    }
}
