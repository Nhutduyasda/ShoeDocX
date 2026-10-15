using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;

public sealed class DocumentExtractionAssembler
{
    // Always validates admission, including direct callers; no public unchecked assembly path.
    public ExtractionResult Assemble(ExtractionContext context, ExtractionBatch batch)
    {
        var errors = new ExtractionObservationValidator().Validate(context, batch);
        if (!errors.IsEmpty) return new(ImmutableArray<ExtractedDocument>.Empty, errors);
        var output = ImmutableArray.CreateBuilder<ExtractedDocument>();
        var p = context.Profile;
        foreach (var group in batch.Observations.GroupBy(o => (o.SourceId, o.DocumentGroupId))
            .OrderBy(g => g.Key.SourceId, StringComparer.Ordinal).ThenBy(g => g.Key.DocumentGroupId, StringComparer.Ordinal))
        {
            var tables = group.Where(o => o.Field.Scope == FieldScope.LineItem).Select(o => o.TableGroupId!)
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).Cast<string?>().ToArray();
            if (tables.Length == 0) tables = new string?[] { null };
            foreach (var table in tables)
            {
                var observations = group.Where(o => o.Field.Scope == FieldScope.Header || o.TableGroupId == table)
                    .OrderBy(o => o.ObservationId, StringComparer.Ordinal).ToImmutableArray();
                var groupId = System.Text.Json.JsonSerializer.Serialize(new[] { group.Key.SourceId, group.Key.DocumentGroupId, table });
                var issues = new List<ExtractionIssue>();
                var header = Values(observations.Where(o => o.Field.Scope == FieldScope.Header), FieldScope.Header);
                var lines = observations.Where(o => o.Field.Scope == FieldScope.LineItem).GroupBy(o => o.RowId)
                    .OrderBy(row => row.First().RowPosition).Select(row => Values(row, FieldScope.LineItem, row.Key, row.First().RowPosition)).ToImmutableArray();
                if (lines.IsEmpty && p.ExtractionSchema.Fields.Any(f => f.Scope == FieldScope.LineItem && f.Required))
                    issues.Add(ExtractionObservationValidator.Issue("LINE_ITEMS_NOT_OBSERVED", group: groupId));
                var document = new CanonicalDocument(groupId, p.TenantId, p.ProfileId, p.Version, header, lines);
                output.Add(new(group.Key.SourceId, group.Key.DocumentGroupId, table, document,
                    new DocumentValidator().Validate(document, p), issues.ToImmutableArray(), observations));

                ImmutableDictionary<string, TypedFieldValue> Values(IEnumerable<ExtractionObservation> row, FieldScope scope,
                    string? rowId = null, int? rowPosition = null)
                {
                    var values = ImmutableDictionary.CreateBuilder<string, TypedFieldValue>(StringComparer.Ordinal);
                    foreach (var field in p.ExtractionSchema.Fields.Where(f => f.Scope == scope))
                    {
                        var reference = new FieldReference(field.Scope, field.Key);
                        var candidates = row.Where(o => o.Field == reference).ToArray();
                        void Issue(string code, string? id = null) => issues.Add(ExtractionObservationValidator.Issue(code, id, groupId,
                            reference, candidates.Select(o => o.ObservationId).ToImmutableArray(), rowId, rowPosition));
                        if (candidates.Length == 0) { Issue("NOT_OBSERVED"); continue; }
                        // Neither candidate ordering nor provider confidence establishes a fact.
                        if (candidates.Length != 1) { Issue("CONFLICTING_OBSERVATIONS"); continue; }
                        var o = candidates[0];
                        var rule = context.Normalization.Rules.FirstOrDefault(r => r.Field == reference);
                        var normalized = new TypedValueNormalizer().Normalize(field.Type, o.RawText, rule);
                        if (normalized.IssueCode is { } code) Issue(code, o.ObservationId);
                        if (o.PageNumber is null) Issue("PAGE_EVIDENCE_ABSENT", o.ObservationId);
                        if (o.Region is null) Issue("REGION_EVIDENCE_ABSENT", o.ObservationId);
                        if (context.Sources.Single(s => s.SourceId == o.SourceId).PageCount is null) Issue("PAGE_BOUNDS_UNKNOWN", o.ObservationId);
                        if (normalized.Value is { } value)
                            values.Add(field.Key, value with { Source = FieldSource.Extracted, Review = FieldReviewStatus.Unreviewed,
                                Evidence = new(o.SourceId, o.PageNumber, o.Region, batch.ProviderId, o.ProviderConfidence) });
                    }
                    return values.ToImmutable();
                }
            }
        }
        return new(output.ToImmutable(), ImmutableArray<ExtractionIssue>.Empty);
    }
}
