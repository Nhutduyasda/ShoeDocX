# Universal Document Engine — Gate 1 implementation report

## A. Repository baseline

- Starting branch: `main`; clean fresh clone, no pre-existing local changes.
- Verified remote main and baseline commit: `ab4160bf1ba39cb4f0178b421ad4642e9af12293`.
- Feature branch: `codex/universal-doc-engine-gate-1`; branch did not already exist remotely.
- No AGENTS.md instructions were present in the checkout.
- Existing backend and xUnit test projects target .NET 8. Existing domain models use EF/footwear entities; the new module imports none of them.
- The initial baseline command could not start because dotnet was absent. SDK 8.0.408 was downloaded into `/tmp/shoedocx-dotnet` without changing repository configuration. A detached baseline worktree was then tested separately at the original SHA.
- Standard VSTest baseline built both projects, then crashed before running tests. Direct xUnit baseline executed 197 tests: 187 passed, 10 failed, 0 skipped. The same runner and dependencies were used for regression comparison.

## B. Files created

- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Configuration/Contracts.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Domain/Contracts.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Validation/DocumentValidator.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Validation/ExportEligibilityPolicy.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Validation/IDocumentValidator.cs`
- `backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Validation/ValidationResult.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/ArithmeticValidationTests.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/CrossFootValidationTests.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/ExportEligibilityTests.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/Fixture.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/RequiredFieldTests.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/ReviewStateTests.cs`
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/SchemaValidationTests.cs`
- `docs/UNIVERSAL_DOC_ENGINE_GATE1.md`

All changes are additions inside the three approved paths. No project/package/configuration edits.
Structural deviation: the small related domain types are grouped in `Domain/Contracts.cs`; configuration-only records are grouped in `Configuration/Contracts.cs`; check/issue/result records share `ValidationResult.cs`. This avoids empty wrappers and keeps the contracts together. The validator, interface, and export policy remain separate components. Six named test suites plus a shared synthetic fixture follow the requested test layout.

## C. Architecture delivered

### Contracts and isolation

`DocumentWorkflowProfile` separates versioned extraction fields, enrichment rules with approved reference-source IDs, computation definitions, validation policies, and Excel mappings. Schema validation checks declarations across all three data sources; an Excel destination never creates an extraction field. Projection may refer to enriched/computed fields while extraction-only fields can stay out of Excel.

`TypedFieldValue` has private construction and explicit Text/Decimal/Date/Boolean factories. Null and absent values stay missing; numeric zero and boolean false are real values. Decimal arithmetic never uses double. Each value carries source provenance, field review, and optional provider-neutral evidence. Provider confidence is raw metadata and grants no approval or certainty. No extraction, enrichment execution, computation pipeline execution, or Excel writing is implemented.

`CanonicalDocument` owns immutable dictionaries and arrays. Identity/content/revision are read-only. `Edit` increments revision, clears approval and warning acknowledgements, and requests review. Profile changes also clear approval. Human approval requires a named reviewer and ReadyForApproval, records revision/version, and additionally binds to a SHA-256 fingerprint of the entire profile configuration. Reusing a profile version with changed configuration cannot silently preserve approval. The fingerprint uses built-in deterministic JSON serialization of immutable profile arrays; configuration order is intentionally part of identity. Profile authors should increment version for every configuration change.

The module depends only on .NET libraries and itself: no EF, HTTP, footwear models/services, Excel packages, AI, storage, DI, or production configuration.

### Deterministic validation

`IDocumentValidator` exposes configuration validation and document validation. Results contain structured stable issue codes, severity, required-check flags, and Passed/Failed/NotEvaluated outcomes. Validation never changes its inputs.

Configuration errors include duplicate fields, invalid versions/types/ranges/operators/tolerances/rounding, unknown or incompatible references, invalid scopes, duplicate rule IDs and destinations, missing approved sources, self-verification and computation cycles. Printed arithmetic targets must be extraction-schema fields.

Document validation checks tenant/profile/version identity, required fields, declared types, numeric limits, provenance, and unknown fields. Arithmetic uses allowlisted addition/subtraction/multiplication/division/comparison; no expression interpreter or dynamic code. Comparison supports equal, less/greater and inclusive variants. Arithmetic output is rounded when configured; its distance from the unrounded printed value is compared to absolute tolerance. Equality comparison uses the same numeric policy; ordered comparisons use exact decimal operands. Division by zero and decimal overflow produce issues.

Cross-foot compares the decimal sum of every line value with the header printed total. Missing operands, missing totals, and empty line lists produce NotEvaluated, never a verified zero. Neither discrepancies nor passing rules diagnose OCR correctness or establish factual source accuracy.

### Review and export

Review states: Draft, NeedsReview, ReadyForApproval, Approved, Rejected. Approval and validation are independent: recording human approval does not remove validation errors.

`ExportEligibilityPolicy` recomputes validation on the exact snapshot rather than accepting a caller-supplied stale/forged result. It denies blocking errors, required unevaluated checks when configured, pending review, rejected documents/fields, unacknowledged warnings when configured, missing approval when configured, and stale approval revision/version/fingerprint. Acknowledging warnings cannot clear BlockingError. Default policy requires review, evaluated required checks, approval, and warning acknowledgement. Only explicitly reviewed data can export even if human approval is configured optional.

## D. Test evidence

Commands (dotnet is `/tmp/shoedocx-dotnet/dotnet` in this environment):

```bash
# First attempt before provisioning SDK: command not found.
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj

# Detached baseline checkout, original commit.
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --logger 'trx;LogFileName=baseline.trx'

# Feature build and full tests.
dotnet build backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --logger 'trx;LogFileName=gate1-full.trx'
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --no-build --filter FullyQualifiedName~UniversalDocEngine --logger 'trx;LogFileName=gate1-targeted.trx'

# After final approval-fingerprint change.
dotnet build backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj --no-restore
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --no-restore --logger 'trx;LogFileName=gate1-full-final.trx'
```

Backend build succeeded, 0 warnings and 0 errors. Test project also compiled successfully. Standard VSTest full/baseline requests crashed in `TestEngine.ShouldRunInProcess` before executing tests; one targeted attempt failed reading process metadata in the CLI. These commands did NOT pass. The temporary SDK extraction reported ownership-setting warnings, but the extracted SDK compiled both projects successfully. No repository SDK/package settings were changed.

As a fallback, a temporary console harness used the existing `Xunit.Runners.AssemblyRunner.WithoutAppDomain` from xUnit runner utility 2.5.2 (bundled with the existing VS adapter). It ran actual compiled xUnit Fact/Theory cases with their real assertions, serially, without modifying tests. Its dependency manifest copied the built test manifest and added only the runner utility runtime assembly so native SQLite dependencies resolved normally. A preliminary run without that manifest had extra SQLite loader failures; those are excluded from the final comparison.

| Final execution | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Direct xUnit original baseline | 197 | 187 | 10 | 0 |
| Direct xUnit Gate 1 only | 66 | 66 | 0 | 0 |
| Direct xUnit full feature suite | 263 | 253 | 10 | 0 |

The final full suite's failing identities/messages match baseline exactly, with zero newly failing legacy cases. No test was weakened, deleted, or marked skipped. New tests cover schema, field types/nulls/ranges, arithmetic/operators/rounding/overflow, cross-foot including empty/missing data, review/approval invalidation, warnings versus blocking errors, unchecked states, tenant isolation and contract dependencies. Synthetic values only.

Fallback execution commands:

```bash
# Gate1Runner.dll is a temporary harness, not a repository artifact.
dotnet tests/ShoeExportInvoice.Tests/bin/Debug/net8.0/Gate1Runner.dll \
  "$PWD/tests/ShoeExportInvoice.Tests/bin/Debug/net8.0/ShoeExportInvoice.Tests.dll" UniversalDocEngine
dotnet tests/ShoeExportInvoice.Tests/bin/Debug/net8.0/Gate1Runner.dll \
  "$PWD/tests/ShoeExportInvoice.Tests/bin/Debug/net8.0/ShoeExportInvoice.Tests.dll"
```

For reproducible standard verification on a normal .NET 8 host/CI, run the original build/test commands above; no harness is needed. The temporary harness source is recorded below for audit, not added to production or the test project.

## E. Regression assessment and remaining risks

- No existing tracked file was modified, including legacy services/models/controllers/frontend/tests.
- No migrations, endpoints, external API calls, authentication, tenant resolution, DI registrations, secrets, database connections, cloud changes, or deployment were added.
- Independent module compilation and source import scan succeeded; no forbidden dependencies found. `git diff --check` passed.
- Baseline failures: eight legacy cases report “Memory stream is not expandable”; two existing actual-workbook tests depend on developer-specific Windows fixture paths. The failure identities are recorded below. These existing tests were left intact.
- The standard VSTest path remains unverified because of an environment crash; direct xUnit results provide regression evidence but do not prove the host's standard test infrastructure is healthy. This is why the gate decision is conservative.
- Gate 1 is an in-memory foundation. API-bound malformed/null object deserialization, persistence, authorization of reviewer IDs, audit storage, tenant enforcement at infrastructure boundaries and Excel output remain future integration responsibilities. Reviewer identity here is supplied by a trusted caller, not authenticated by this module. Immutable profile versions should be persisted by a later gate.
- Evidence coordinates/confidence are metadata contracts only; provider normalization is not performed.

## F. Git delivery

Feature branch: `codex/universal-doc-engine-gate-1`, targeting `main` as a Draft PR. The implementation commit and PR URL are supplied in the delivery response because a commit cannot embed its own SHA. Main is unchanged; no merge, force push or deployment. Only the feature branch is published.

## G. Gate decision

**PARTIAL** — implementation and all 66 new xUnit cases are validated; full direct-xUnit regression has no new failures, but standard VSTest regression execution remains blocked by the host crash and the existing suite still has 10 baseline failures. Do not treat this as a clean standard-suite PASS. Obtain normal-host/CI test evidence before changing the decision.

## H. Gate 2 readiness

The schema, immutable typed canonical fields, extraction/enrichment/computation separation, Excel source mapping contracts, and export eligibility policy are ready to guide a future ExcelTemplateProjector. Gate 2 must explicitly specify destination grammar, row expansion, formatting, template/version identity and file handling. It should call eligibility before output and retain provenance and approvals. No projector or Gate 2 execution has begun.

## Appendix: existing failed test identities

- `ShoeExportInvoice.Tests.VietnameseNumberToWordsTests.ExportShipmentMultiSheetExcel_ShouldUpdateBangChuCellInInvSheet`
- `ShoeExportInvoice.Tests.BatchOcrTests.BatchExportZip_ShouldAllocateConsecutiveSequences_AndSaveToDb`
- `ShoeExportInvoice.Tests.ProductMasterExcelImportTests.Import_ActualWorkbook1File_AdaptiveParser_ShouldDetectColumnsAndSucceed`
- `ShoeExportInvoice.Tests.ProductMasterExcelImportTests.Import_ActualBook1File_AdaptiveParser_ShouldDetectColumnsAndSucceed`
- `ShoeExportInvoice.Tests.UnitTest1.CalculatePklBreakdown_WithDynamicPairPerCarton_CalculatesAccurately`
- `ShoeExportInvoice.Tests.UnitTest1.ExportShipmentMultiSheetExcel_ShouldPreserveThreeSheetsAndFormulas`
- `ShoeExportInvoice.Tests.MasterDataFolderTests.ExportShipment_Sheet2_ContainsOnlyTargetPartnerAndAllDescendantProducts`
- `ShoeExportInvoice.Tests.TemplateSchemaTests.ExportShipmentMultiSheetExcel_WithCustomTemplateConfig_MapsCoordinatesDynamically`
- `ShoeExportInvoice.Tests.TemplateSchemaTests.ExportShipmentMultiSheetExcel_WithDefaultTemplate_MapsCoordinatesAccurately`
- `ShoeExportInvoice.Tests.TemplateSchemaTests.ExportShipmentToExcel_WithCustomTemplateConfig_MapsCoordinatesDynamically`

## Appendix: temporary xUnit runner source

```csharp
using System;
using System.Threading;
using Xunit.Runners;
class Program
{
    static int Main(string[] args)
    {
        using var done = new ManualResetEvent(false);
        int failures = 0;
        using var runner = AssemblyRunner.WithoutAppDomain(args[0]);
        runner.OnTestFailed = info => { Console.WriteLine("FAIL: " + info.TestDisplayName + " " + info.ExceptionMessage); failures++; };
        runner.OnTestSkipped = info => Console.WriteLine("SKIP: " + info.TestDisplayName + " " + info.SkipReason);
        runner.OnErrorMessage = info => { Console.WriteLine("ERROR: " + info.ExceptionMessage); failures++; done.Set(); };
        runner.OnExecutionComplete = info => { Console.WriteLine($"TOTAL {info.TotalTests} FAILED {info.TestsFailed} SKIPPED {info.TestsSkipped}"); done.Set(); };
        runner.TestCaseFilter = testCase => args.Length < 2 || testCase.TestMethod.TestClass.Class.Name.Contains(args[1]);
        runner.Start(parallel: false);
        if (!done.WaitOne(TimeSpan.FromMinutes(3))) { Console.WriteLine("TIMEOUT"); return 2; }
        while (runner.Status != AssemblyRunnerStatus.Idle) Thread.Yield();
        return failures == 0 ? 0 : 1;
    }
}
```
