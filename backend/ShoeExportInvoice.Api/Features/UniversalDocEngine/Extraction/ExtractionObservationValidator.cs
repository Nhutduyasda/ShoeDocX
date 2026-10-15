using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;

public sealed class ExtractionObservationValidator
{
    internal static ExtractionIssue Issue(string code, string? observation = null, string? group = null,
        FieldReference? field = null, ImmutableArray<string> candidates = default) =>
        new(code, "Extraction requires correction: " + code + ".", observation, group, field,
            candidates.IsDefault ? ImmutableArray<string>.Empty : candidates);
    private static bool Id(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= ExtractionLimits.IdentifierLength;
    private static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    public ImmutableArray<ExtractionIssue> ValidateContext(ExtractionContext? context)
    {
        var issues = new List<ExtractionIssue>();
        void Error(string code) => issues.Add(Issue(code));
        if (context?.Profile is not { } p || context.Normalization?.Rules is not { IsDefault: false } rules ||
            context.Sources.IsDefaultOrEmpty) return ImmutableArray.Create(Issue("MALFORMED_CONTEXT"));
        if (context.Sources.Length > ExtractionLimits.Sources || context.Sources.Any(s => s is null))
            return ImmutableArray.Create(Issue("SOURCE_LIMIT_OR_SHAPE"));
        if (context.Sources.Any(s => !Id(s.SourceId) || !Hash(s.Sha256) || s.ByteLength <= 0 ||
            s.ByteLength > ExtractionLimits.SourceBytes || s.PageCount is <= 0 or > ExtractionLimits.PagesPerSource)) Error("INVALID_SOURCE_METADATA");
        if (context.Sources.Sum(s => s.ByteLength > 0 && s.ByteLength <= ExtractionLimits.SourceBytes ? s.ByteLength : 0) > ExtractionLimits.TotalSourceBytes ||
            context.Sources.Sum(s => s.PageCount is > 0 and <= ExtractionLimits.PagesPerSource ? s.PageCount.Value : 0) > ExtractionLimits.TotalPages) Error("SOURCE_LIMIT");
        if (context.Sources.Select(s => s.SourceId).Distinct(StringComparer.Ordinal).Count() != context.Sources.Length) Error("DUPLICATE_SOURCE");
        // Shape and bounds checked before invoking the existing Gate 1 configuration validator.
        if (!Id(p.ProfileId) || !Id(p.TenantId) || p.ExtractionSchema is null || p.ExtractionSchema.Fields.IsDefaultOrEmpty ||
            p.ExtractionSchema.Fields.Length > ExtractionLimits.Fields || p.ExtractionSchema.Fields.Any(f => f is null || !Id(f.Key)) ||
            p.EnrichmentRules.IsDefault || p.ComputationRules.IsDefault || p.ValidationPolicy is null || p.ExcelProjectionMap is null ||
            p.EnrichmentRules.Length > ExtractionLimits.Fields || p.ComputationRules.Length > ExtractionLimits.Fields ||
            p.ValidationPolicy.Arithmetic.IsDefault || p.ValidationPolicy.CrossFoot.IsDefault || p.ExcelProjectionMap.Mappings.IsDefault ||
            p.ValidationPolicy.Arithmetic.Length > ExtractionLimits.Fields || p.ValidationPolicy.CrossFoot.Length > ExtractionLimits.Fields ||
            p.ExcelProjectionMap.Mappings.Length > ExtractionLimits.Fields) return issues.Append(Issue("MALFORMED_PROFILE")).ToImmutableArray();
        try
        {
            if (!new DocumentValidator().ValidateConfiguration(p).Issues.IsEmpty) Error("INVALID_PROFILE");
        }
        catch (Exception ex) when (ex is NullReferenceException or ArgumentException or InvalidOperationException)
        { Error("MALFORMED_PROFILE"); }
        if (rules.Length > ExtractionLimits.Fields || rules.Any(r => r is null || r.Field is null ||
            r.DecimalConventions.IsDefault || r.DateFormats.IsDefault || r.TrueTokens.IsDefault || r.FalseTokens.IsDefault ||
            r.DecimalConventions.Length > 2 || r.DateFormats.Length > 5 || r.TrueTokens.Length > 20 || r.FalseTokens.Length > 20))
            return issues.Append(Issue("MALFORMED_NORMALIZATION_POLICY")).ToImmutableArray();
        if (rules.Select(r => r.Field).Distinct().Count() != rules.Length) Error("DUPLICATE_NORMALIZATION_RULE");
        foreach (var rule in rules)
        {
            var field = p.ExtractionSchema.Fields.FirstOrDefault(f => f.Key == rule.Field.Key && f.Scope == rule.Field.Scope);
            if (field is null || rule.DecimalConventions.Any(c => !Enum.IsDefined(c)) ||
                rule.DateFormats.Any(f => !TypedValueNormalizer.SupportedDateFormats.Contains(f)) ||
                rule.TrueTokens.Concat(rule.FalseTokens).Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 100 || t != t.Trim()) ||
                rule.TrueTokens.Intersect(rule.FalseTokens, StringComparer.OrdinalIgnoreCase).Any() ||
                (field.Type == FieldType.Decimal && rule.DecimalConventions.IsEmpty) ||
                (field.Type == FieldType.Date && rule.DateFormats.IsEmpty) ||
                (field.Type == FieldType.Boolean && (rule.TrueTokens.IsEmpty || rule.FalseTokens.IsEmpty))) Error("INVALID_NORMALIZATION_RULE");
        }
        return issues.ToImmutableArray();
    }

    public ImmutableArray<ExtractionIssue> Validate(ExtractionContext context, ExtractionBatch? batch)
    {
        var issues = ValidateContext(context).ToList();
        if (issues.Count != 0) return issues.ToImmutableArray();
        var p = context.Profile;
        if (batch is null || batch.Observations.IsDefault) return ImmutableArray.Create(Issue("MALFORMED_PROVIDER_DATA"));
        if (batch.TenantId != p.TenantId || batch.ProfileId != p.ProfileId || batch.ProfileVersion != p.Version ||
            batch.SchemaVersion != p.ExtractionSchema.Version || batch.ProfileFingerprint != WorkflowProfileIdentity.Fingerprint(p))
            issues.Add(Issue("WORKFLOW_IDENTITY_MISMATCH"));
        if (!Id(batch.ProviderId)) issues.Add(Issue("INVALID_PROVIDER"));
        if (batch.Observations.Length > ExtractionLimits.Observations) return issues.Append(Issue("OBSERVATION_LIMIT")).ToImmutableArray();
        if (batch.Observations.IsEmpty) issues.Add(Issue("EMPTY_EXTRACTION"));
        if (batch.Observations.Any(o => o is null)) return issues.Append(Issue("MALFORMED_OBSERVATION")).ToImmutableArray();
        if (batch.Observations.Sum(o => (long)(o.RawText?.Length ?? 0)) > ExtractionLimits.TotalTextLength) issues.Add(Issue("PAYLOAD_LIMIT"));
        var sources = context.Sources.ToDictionary(s => s.SourceId, StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var o in batch.Observations)
        {
            void Error(string code) => issues.Add(Issue(code, Id(o.ObservationId) ? o.ObservationId : null));
            if (!Id(o.ObservationId) || !ids.Add(o.ObservationId)) Error("INVALID_OBSERVATION_ID");
            if (!Id(o.SourceId) || !sources.TryGetValue(o.SourceId, out var source)) { Error("UNAUTHORIZED_SOURCE"); continue; }
            if (!Hash(o.SourceSha256) || !string.Equals(o.SourceSha256, source.Sha256, StringComparison.OrdinalIgnoreCase)) Error("SOURCE_FINGERPRINT_MISMATCH");
            if (o.Field is null || !Enum.IsDefined(o.Field.Scope) || !Id(o.Field.Key) ||
                !p.ExtractionSchema.Fields.Any(f => f.Key == o.Field.Key && f.Scope == o.Field.Scope)) Error("FIELD_NOT_EXTRACTABLE");
            if (!Id(o.DocumentGroupId)) Error("DOCUMENT_GROUP_REQUIRED");
            if (o.Field?.Scope == FieldScope.Header && (o.TableGroupId is not null || o.RowId is not null || o.RowPosition is not null)) Error("CONTRADICTORY_HEADER_GROUP");
            if (o.Field?.Scope == FieldScope.LineItem && (!Id(o.TableGroupId) || !Id(o.RowId) || o.RowPosition is null or < 0 or >= ExtractionLimits.RowsPerTable)) Error("ROW_GROUP_REQUIRED");
            if (o.RawText?.Length > ExtractionLimits.TextLength) Error("TEXT_LIMIT");
            if (o.PageNumber is <= 0 or > ExtractionLimits.PagesPerSource || (o.PageNumber.HasValue && source.PageCount.HasValue && o.PageNumber > source.PageCount)) Error("INVALID_PAGE");
            if (o.Region is { } r && (!o.PageNumber.HasValue || r.X < 0 || r.Y < 0 || r.X > 1 || r.Y > 1 ||
                r.Width <= 0 || r.Height <= 0 || r.Width > 1 || r.Height > 1 || r.X > 1 - r.Width || r.Y > 1 - r.Height)) Error("INVALID_REGION");
            if (o.ProviderConfidence is < 0 or > 1) Error("INVALID_CONFIDENCE");
            if (o.CandidateOrder is < 0 or > ExtractionLimits.Observations) Error("INVALID_CANDIDATE_ORDER");
        }
        if (issues.Count != 0) return issues.ToImmutableArray();
        var documents = batch.Observations.GroupBy(o => (o.SourceId, o.DocumentGroupId)).ToArray();
        var outputCount = 0;
        foreach (var document in documents)
        {
            var tables = document.Where(o => o.Field.Scope == FieldScope.LineItem).GroupBy(o => o.TableGroupId).ToArray();
            outputCount += Math.Max(1, tables.Length);
            foreach (var table in tables)
            {
                var rows = table.GroupBy(o => o.RowId).ToArray();
                if (rows.Length > ExtractionLimits.RowsPerTable || rows.Any(row => row.Select(o => o.RowPosition).Distinct().Count() != 1) ||
                    rows.Select(row => row.First().RowPosition).Distinct().Count() != rows.Length)
                    issues.Add(Issue("CONTRADICTORY_ROW_POSITIONS"));
                // This subset requires one page per explicit row; it does not infer multi-page row joins.
                foreach (var row in rows)
                    if (row.Where(o => o.PageNumber.HasValue).Select(o => o.PageNumber).Distinct().Count() > 1)
                        issues.Add(Issue("CONTRADICTORY_ROW_PAGE"));
            }
        }
        if (outputCount > ExtractionLimits.Documents) issues.Add(Issue("DOCUMENT_LIMIT"));
        return issues.ToImmutableArray();
    }
}
