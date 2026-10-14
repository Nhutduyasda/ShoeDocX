using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

public sealed class DocumentValidator : IDocumentValidator
{
    private static IEnumerable<FieldDefinition> Fields(DocumentWorkflowProfile p) =>
        p.ExtractionSchema.Fields.Concat(p.EnrichmentRules.Select(r => r.Target))
            .Concat(p.ComputationRules.Select(r => r.Target));
    public ValidationResult ValidateConfiguration(DocumentWorkflowProfile p)
    {
        var issues = new List<ValidationIssue>();
        void Error(string code, string id) => issues.Add(new(code, id, IssueSeverity.BlockingError,
            "Invalid workflow configuration: " + code));
        if (string.IsNullOrWhiteSpace(p.ProfileId) || string.IsNullOrWhiteSpace(p.TenantId) ||
            p.Version <= 0 || p.ExtractionSchema.Version <= 0) Error("INVALID_SCHEMA_VERSION", "profile");
        if (p.ExtractionSchema.Fields.IsDefault || p.EnrichmentRules.IsDefault ||
            p.ComputationRules.IsDefault || p.ValidationPolicy.Arithmetic.IsDefault ||
            p.ValidationPolicy.CrossFoot.IsDefault || p.ExcelProjectionMap.Mappings.IsDefault)
        {
            Error("INVALID_COLLECTION", "profile");
            return Result(issues);
        }
        var fields = Fields(p).ToArray();
        foreach (var group in fields.GroupBy(f => new FieldReference(f.Scope, f.Key)))
            if (group.Count() > 1) Error("DUPLICATE_FIELD", group.Key.ToString());
        foreach (var f in fields)
        {
            if (string.IsNullOrWhiteSpace(f.Key) || !Enum.IsDefined(f.Type) || !Enum.IsDefined(f.Scope))
                Error("INVALID_FIELD_DEFINITION", f.Key);
            if ((f.Minimum.HasValue || f.Maximum.HasValue || f.ExclusiveMinimum) &&
                (f.Type != FieldType.Decimal || f.Minimum > f.Maximum ||
                 (f.ExclusiveMinimum && (!f.Minimum.HasValue || f.Minimum == f.Maximum))))
                Error("INVALID_RANGE", f.Key);
        }
        FieldDefinition? Find(FieldReference r) => fields.FirstOrDefault(f => f.Scope == r.Scope && f.Key == r.Key);
        void Ref(FieldReference r, string id, FieldType? type = null)
        {
            var f = Find(r);
            if (f is null) Error("UNKNOWN_FIELD_REFERENCE", id);
            else if (type.HasValue && f.Type != type) Error("INCOMPATIBLE_RULE_TYPE", id);
        }
        void Numeric(NumericPolicy n, string id)
        {
            if (n.Tolerance < 0 || n.DecimalPlaces is < 0 or > 28 || !Enum.IsDefined(n.Rounding))
                Error("INVALID_NUMERIC_POLICY", id);
        }
        void Op(ArithmeticOperator op, string id)
        { if (!Enum.IsDefined(op)) Error("UNSUPPORTED_OPERATOR", id); }
        var ids = p.EnrichmentRules.Select(r => r.Id).Concat(p.ComputationRules.Select(r => r.Id))
            .Concat(p.ValidationPolicy.Arithmetic.Select(r => r.Id)).Concat(p.ValidationPolicy.CrossFoot.Select(r => r.Id));
        foreach (var g in ids.GroupBy(x => x))
            if (string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1) Error("INVALID_RULE_ID", g.Key);
        foreach (var r in p.EnrichmentRules)
        {
            Ref(r.LookupField, r.Id);
            if (string.IsNullOrWhiteSpace(r.ApprovedSourceId)) Error("MISSING_APPROVED_SOURCE", r.Id);
            if (r.Target.Scope == FieldScope.Header && r.LookupField.Scope == FieldScope.LineItem)
                Error("INVALID_RULE_SCOPE", r.Id);
        }
        foreach (var r in p.ComputationRules)
        {
            Op(r.Operator, r.Id); Ref(r.Left, r.Id, FieldType.Decimal); Ref(r.Right, r.Id, FieldType.Decimal);
            if (r.Target.Type != (r.Operator == ArithmeticOperator.Compare ? FieldType.Boolean : FieldType.Decimal))
                Error("INCOMPATIBLE_RULE_TYPE", r.Id);
            if (r.Target.Scope == FieldScope.Header &&
                (r.Left.Scope == FieldScope.LineItem || r.Right.Scope == FieldScope.LineItem)) Error("INVALID_RULE_SCOPE", r.Id);
        }
        // Detect computed dependency cycles; execution is deliberately outside Gate 1.
        var computed = p.ComputationRules.GroupBy(r => new FieldReference(r.Target.Scope, r.Target.Key))
            .ToDictionary(g => g.Key, g => g.First());
        var visiting = new HashSet<FieldReference>(); var done = new HashSet<FieldReference>();
        bool Cycle(FieldReference key)
        {
            if (done.Contains(key) || !computed.TryGetValue(key, out var rule)) return false;
            if (!visiting.Add(key)) return true;
            var cycle = Cycle(rule.Left) || Cycle(rule.Right);
            visiting.Remove(key); done.Add(key); return cycle;
        }
        foreach (var key in computed.Keys) if (Cycle(key)) Error("COMPUTATION_CYCLE", key.ToString());
        foreach (var r in p.ValidationPolicy.Arithmetic)
        {
            Op(r.Operator, r.Id); Numeric(r.Numeric, r.Id);
            Ref(r.Left, r.Id, FieldType.Decimal); Ref(r.Right, r.Id, FieldType.Decimal);
            Ref(r.PrintedValue, r.Id, r.Operator == ArithmeticOperator.Compare ? FieldType.Boolean : FieldType.Decimal);
            if (!Enum.IsDefined(r.Comparison) || !Enum.IsDefined(r.Severity)) Error("INVALID_RULE_POLICY", r.Id);
            if (r.PrintedValue.Scope == FieldScope.Header &&
                (r.Left.Scope == FieldScope.LineItem || r.Right.Scope == FieldScope.LineItem)) Error("INVALID_RULE_SCOPE", r.Id);
            if (!p.ExtractionSchema.Fields.Any(f => f.Scope == r.PrintedValue.Scope && f.Key == r.PrintedValue.Key))
                Error("PRINTED_VALUE_NOT_EXTRACTED", r.Id);
            if (r.PrintedValue == r.Left || r.PrintedValue == r.Right) Error("SELF_VERIFICATION", r.Id);
        }
        foreach (var r in p.ValidationPolicy.CrossFoot)
        {
            Ref(r.LineValue, r.Id, FieldType.Decimal); Ref(r.PrintedTotal, r.Id, FieldType.Decimal);
            Numeric(r.Numeric, r.Id);
            if (r.LineValue.Scope != FieldScope.LineItem || r.PrintedTotal.Scope != FieldScope.Header)
                Error("INVALID_RULE_SCOPE", r.Id);
            if (!Enum.IsDefined(r.Severity)) Error("INVALID_RULE_POLICY", r.Id);
            if (!p.ExtractionSchema.Fields.Any(f => f.Scope == r.PrintedTotal.Scope && f.Key == r.PrintedTotal.Key))
                Error("PRINTED_VALUE_NOT_EXTRACTED", r.Id);
        }
        foreach (var r in p.ExcelProjectionMap.Mappings)
        {
            Ref(r.Source, "excel:" + r.Destination);
            if (string.IsNullOrWhiteSpace(r.Destination)) Error("INVALID_EXCEL_DESTINATION", "excel");
        }
        foreach (var g in p.ExcelProjectionMap.Mappings.GroupBy(r => r.Destination))
            if (g.Count() > 1) Error("DUPLICATE_EXCEL_DESTINATION", "excel:" + g.Key);
        return Result(issues);
    }
    private static ValidationResult Result(List<ValidationIssue> issues) => new(
        ImmutableArray.Create(new ValidationCheck("schema", issues.Count == 0 ? CheckOutcome.Passed : CheckOutcome.Failed, true)),
        issues.ToImmutableArray());
    public ValidationResult Validate(CanonicalDocument d, DocumentWorkflowProfile p)
    {
        var schema = ValidateConfiguration(p);
        if (schema.Issues.Length != 0) return schema;
        var checks = schema.Checks.ToList(); var issues = new List<ValidationIssue>();
        void Check(string id, CheckOutcome outcome, bool required, string? code = null,
            IssueSeverity severity = IssueSeverity.BlockingError)
        {
            checks.Add(new(id, outcome, required));
            if (code is not null) issues.Add(new(code, id, severity,
                "Check " + id + ": " + code + ". Internal consistency does not establish source accuracy."));
        }
        if (d.TenantId != p.TenantId || d.ProfileId != p.ProfileId || d.ProfileVersion != p.Version ||
            string.IsNullOrWhiteSpace(d.DocumentId) || d.LineItems.IsDefault)
        {
            Check("identity", CheckOutcome.Failed, true, "DOCUMENT_PROFILE_MISMATCH");
            return new(checks.ToImmutableArray(), issues.ToImmutableArray());
        }
        Check("identity", CheckOutcome.Passed, true);
        var definitions = Fields(p).ToArray();
        void UnknownFields(ImmutableDictionary<string, TypedFieldValue> row, FieldScope scope, int index)
        {
            foreach (var key in row.Keys.OrderBy(k => k, StringComparer.Ordinal))
                if (!definitions.Any(f => f.Scope == scope && f.Key == key))
                    Check($"field:{scope}:{key}:{index}", CheckOutcome.Failed, true, "UNKNOWN_DOCUMENT_FIELD");
        }
        UnknownFields(d.Header, FieldScope.Header, 0);
        for (int line = 0; line < d.LineItems.Length; line++) UnknownFields(d.LineItems[line], FieldScope.LineItem, line);
        foreach (var f in definitions)
        {
            var rows = f.Scope == FieldScope.Header ? new[] { d.Header } : d.LineItems.ToArray();
            if (rows.Length == 0 && f.Required)
                Check("field:LineItem:" + f.Key, CheckOutcome.NotEvaluated, true, "REQUIRED_FIELD_MISSING");
            for (int i = 0; i < rows.Length; i++)
            {
                var id = $"field:{f.Scope}:{f.Key}:{i}";
                rows[i].TryGetValue(f.Key, out var v);
                if (v is not null && v.Type != f.Type) Check(id, CheckOutcome.Failed, f.Required, "INVALID_FIELD_TYPE");
                else if (v is null || v.IsMissing)
                    Check(id, CheckOutcome.NotEvaluated, f.Required, f.Required ? "REQUIRED_FIELD_MISSING" : null);
                else if (!Enum.IsDefined(v.Source) || !Enum.IsDefined(v.Review))
                    Check(id, CheckOutcome.Failed, true, "INVALID_FIELD_METADATA");
                else if (v.Source != FieldSource.Manual && v.Source !=
                    (p.ExtractionSchema.Fields.Contains(f) ? FieldSource.Extracted :
                     p.EnrichmentRules.Any(r => r.Target == f) ? FieldSource.Enriched : FieldSource.Computed))
                    Check(id, CheckOutcome.Failed, true, "INVALID_FIELD_PROVENANCE");
                else if (v.Number is decimal n && ((f.Minimum is decimal min && (f.ExclusiveMinimum ? n <= min : n < min)) ||
                    (f.Maximum is decimal max && n > max))) Check(id, CheckOutcome.Failed, f.Required, "VALUE_OUT_OF_RANGE");
                else Check(id, CheckOutcome.Passed, f.Required);
            }
        }
        TypedFieldValue? Value(FieldReference r, int line)
        {
            var row = r.Scope == FieldScope.Header ? d.Header : d.LineItems[line];
            return row.TryGetValue(r.Key, out var value) ? value : null;
        }
        decimal? Number(FieldReference r, int line)
        {
            var v = Value(r, line);
            return v?.Type == FieldType.Decimal ? v.Number : null;
        }
        foreach (var r in p.ValidationPolicy.Arithmetic)
        {
            var count = r.PrintedValue.Scope == FieldScope.Header ? 1 : d.LineItems.Length;
            if (count == 0) Check(r.Id, CheckOutcome.NotEvaluated, r.Required, "MISSING_RULE_INPUT", r.Severity);
            for (var i = 0; i < count; i++)
            {
                var id = $"{r.Id}:{i}"; var left = Number(r.Left, i); var right = Number(r.Right, i);
                var printed = Value(r.PrintedValue, i);
                if (left is null || right is null || printed is null || printed.IsMissing ||
                    printed.Type != (r.Operator == ArithmeticOperator.Compare ? FieldType.Boolean : FieldType.Decimal))
                { Check(id, CheckOutcome.NotEvaluated, r.Required, "MISSING_RULE_INPUT", r.Severity); continue; }
                try
                {
                    if (r.Operator == ArithmeticOperator.Divide && right == 0)
                    { Check(id, CheckOutcome.Failed, r.Required, "DIVISION_BY_ZERO", r.Severity); continue; }
                    bool match;
                    if (r.Operator == ArithmeticOperator.Compare)
                    {
                        var comparison = r.Comparison switch {
                            ComparisonOperator.Equal => Close(left.Value, right.Value, r.Numeric),
                            ComparisonOperator.LessThan => left < right,
                            ComparisonOperator.LessThanOrEqual => left <= right,
                            ComparisonOperator.GreaterThan => left > right,
                            ComparisonOperator.GreaterThanOrEqual => left >= right,
                            _ => false };
                        match = printed.Boolean == comparison;
                    }
                    else
                    {
                        var expected = r.Operator switch {
                            ArithmeticOperator.Add => checked(left.Value + right.Value),
                            ArithmeticOperator.Subtract => checked(left.Value - right.Value),
                            ArithmeticOperator.Multiply => checked(left.Value * right.Value),
                            ArithmeticOperator.Divide => left.Value / right.Value,
                            _ => throw new InvalidOperationException() };
                        match = Close(expected, printed.Number!.Value, r.Numeric);
                    }
                    Check(id, match ? CheckOutcome.Passed : CheckOutcome.Failed, r.Required,
                        match ? null : "ARITHMETIC_MISMATCH", r.Severity);
                }
                catch (OverflowException) { Check(id, CheckOutcome.Failed, r.Required, "NUMERIC_OVERFLOW", r.Severity); }
            }
        }
        foreach (var r in p.ValidationPolicy.CrossFoot)
        {
            var total = Number(r.PrintedTotal, 0);
            var values = Enumerable.Range(0, d.LineItems.Length).Select(i => Number(r.LineValue, i)).ToArray();
            if (total is null || values.Length == 0 || values.Any(v => v is null))
            { Check(r.Id, CheckOutcome.NotEvaluated, r.Required, "MISSING_RULE_INPUT", r.Severity); continue; }
            try
            {
                var sum = values.Aggregate(0m, (acc, v) => checked(acc + v!.Value));
                var match = Close(sum, total.Value, r.Numeric);
                Check(r.Id, match ? CheckOutcome.Passed : CheckOutcome.Failed, r.Required,
                    match ? null : "SOURCE_TOTAL_MISMATCH", r.Severity);
            }
            catch (OverflowException) { Check(r.Id, CheckOutcome.Failed, r.Required, "NUMERIC_OVERFLOW", r.Severity); }
        }
        return new(checks.ToImmutableArray(), issues.ToImmutableArray());
    }
    private static bool Close(decimal expected, decimal actual, NumericPolicy p)
    {
        if (p.DecimalPlaces is int places) expected = decimal.Round(expected, places, p.Rounding);
        return Math.Abs(checked(expected - actual)) <= p.Tolerance;
    }
}
