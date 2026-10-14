using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
public sealed record ExtractionSchema(int Version, ImmutableArray<FieldDefinition> Fields);
public sealed record EnrichmentRule(string Id, FieldDefinition Target, string ApprovedSourceId,
    FieldReference LookupField);
public enum ArithmeticOperator { Add, Subtract, Multiply, Divide, Compare }
public enum ComparisonOperator { Equal, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual }
public sealed record ComputationRule(string Id, FieldDefinition Target, FieldReference Left,
    FieldReference Right, ArithmeticOperator Operator);
public sealed record NumericPolicy(decimal Tolerance = 0, int? DecimalPlaces = null,
    MidpointRounding Rounding = MidpointRounding.ToEven);
public sealed record ArithmeticCheck(string Id, FieldReference Left, FieldReference Right,
    ArithmeticOperator Operator, FieldReference PrintedValue, NumericPolicy Numeric,
    bool Required = true, IssueSeverity Severity = IssueSeverity.BlockingError,
    ComparisonOperator Comparison = ComparisonOperator.Equal);
public sealed record CrossFootCheck(string Id, FieldReference LineValue, FieldReference PrintedTotal,
    NumericPolicy Numeric, bool Required = true, IssueSeverity Severity = IssueSeverity.BlockingError);
public sealed record ValidationPolicy(ImmutableArray<ArithmeticCheck> Arithmetic,
    ImmutableArray<CrossFootCheck> CrossFoot, bool RequireEvaluatedChecks = true,
    bool RequireHumanApproval = true, bool RequireWarningAcknowledgement = true);
public sealed record ExcelMapping(FieldReference Source, string Destination);
public sealed record ExcelProjectionMap(ImmutableArray<ExcelMapping> Mappings);
