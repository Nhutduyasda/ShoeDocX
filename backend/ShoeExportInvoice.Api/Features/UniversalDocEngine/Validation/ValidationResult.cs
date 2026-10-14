using System.Collections.Immutable;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
public enum CheckOutcome { Passed, Failed, NotEvaluated }
public enum IssueSeverity { Info, Warning, BlockingError }
public sealed record ValidationIssue(string Code, string CheckId, IssueSeverity Severity, string Message);
public sealed record ValidationCheck(string Id, CheckOutcome Outcome, bool Required);
// Informational evidence only: passing rules does not establish factual correctness.
public sealed record ValidationResult(ImmutableArray<ValidationCheck> Checks,
    ImmutableArray<ValidationIssue> Issues);
