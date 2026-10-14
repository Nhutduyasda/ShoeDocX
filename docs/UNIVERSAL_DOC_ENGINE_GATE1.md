# Universal Document Engine — Gate 1 implementation report

Updated for final hardening on 2026-10-14. This report supersedes the review/export safety claims in the initial report at `c5de05151782dcf10c4faf8a38045a5d24898365`. PR #6 remains Draft; Gate 2 has not begun.

## A. Repository baseline

- Starting branch: `main`; clean fresh clone, no pre-existing local changes.
- Verified remote main and baseline commit: `ab4160bf1ba39cb4f0178b421ad4642e9af12293`.
- Feature branch: `codex/universal-doc-engine-gate-1`; branch did not already exist remotely.
- No AGENTS.md instructions were present in the checkout.
- Existing backend and xUnit test projects target .NET 8. Existing domain models use EF/footwear entities; the new module imports none of them.
- The initial baseline command could not start because dotnet was absent. SDK 8.0.408 was downloaded into `/tmp/shoedocx-dotnet` without changing repository configuration. A detached baseline worktree was then tested separately at the original SHA.
- Standard VSTest baseline built both projects, then crashed before running tests. Direct xUnit baseline executed 197 tests: 187 passed, 10 failed, 0 skipped. The same runner and dependencies were used for regression comparison.

## B. Files introduced by PR #6

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
- `tests/ShoeExportInvoice.Tests/UniversalDocEngine/ReviewHardeningTests.cs`
- `docs/UNIVERSAL_DOC_ENGINE_GATE1.md`

The initial PR introduced 14 files. Hardening modifies six of those files and adds `ReviewHardeningTests.cs`, so the final PR introduces 15 files versus main. Hardening changes only:

- `Domain/Contracts.cs`
- `Validation/ExportEligibilityPolicy.cs`
- `Fixture.cs`
- `ReviewStateTests.cs`
- `ExportEligibilityTests.cs`
- `ReviewHardeningTests.cs` (new)
- This report.

No project/package/CI/configuration edits. All seven hardening paths remain within the three approved directories.
Structural deviation: the small related domain types are grouped in `Domain/Contracts.cs`; configuration-only records are grouped in `Configuration/Contracts.cs`; check/issue/result records share `ValidationResult.cs`. This avoids empty wrappers and keeps the contracts together. The validator, interface, and export policy remain separate components. Seven test suite files plus a shared synthetic fixture follow the requested test layout.

## C. Architecture delivered

### Contracts and isolation

`DocumentWorkflowProfile` separates versioned extraction fields, enrichment rules with approved reference-source IDs, computation definitions, validation policies, and Excel mappings. Schema validation checks declarations across all three data sources; an Excel destination never creates an extraction field. Projection may refer to enriched/computed fields while extraction-only fields can stay out of Excel.

`TypedFieldValue` has private construction and explicit Text/Decimal/Date/Boolean factories. Null and absent values stay missing; numeric zero and boolean false are real values. Decimal arithmetic never uses double. Each value carries source provenance, field review, and optional provider-neutral evidence. Provider confidence is raw metadata and grants no approval or certainty. No extraction, enrichment execution, computation pipeline execution, or Excel writing is implemented.

`CanonicalDocument` owns immutable dictionaries and arrays. Identity/content/revision are read-only. `Edit` increments revision, clears completed-review evidence, approval and warning acknowledgements, and requests review. Profile-version edits also clear all three. Even an edit submitted with unchanged data conservatively requires another review. Human approval requires a named reviewer and ReadyForApproval, records revision/version, and additionally binds to a SHA-256 fingerprint of the entire profile configuration. Reusing a profile version with changed configuration cannot silently preserve approval. The fingerprint uses built-in deterministic JSON serialization of immutable profile arrays; configuration order is intentionally part of identity. Profile authors should increment version for every configuration change.

The module depends only on .NET libraries and itself: no EF, HTTP, footwear models/services, Excel packages, AI, storage, DI, or production configuration.

### Deterministic validation

`IDocumentValidator` exposes configuration validation and document validation. Results contain structured stable issue codes, severity, required-check flags, and Passed/Failed/NotEvaluated outcomes. Validation never changes its inputs.

Configuration errors include duplicate fields, invalid versions/types/ranges/operators/tolerances/rounding, unknown or incompatible references, invalid scopes, duplicate rule IDs and destinations, missing approved sources, self-verification and computation cycles. Printed arithmetic targets must be extraction-schema fields.

Document validation checks tenant/profile/version identity, required fields, declared types, numeric limits, provenance, and unknown fields. Arithmetic uses allowlisted addition/subtraction/multiplication/division/comparison; no expression interpreter or dynamic code. Comparison supports equal, less/greater and inclusive variants. Arithmetic output is rounded when configured; its distance from the unrounded printed value is compared to absolute tolerance. Equality comparison uses the same numeric policy; ordered comparisons use exact decimal operands. Division by zero and decimal overflow produce issues.

Cross-foot compares the decimal sum of every line value with the header printed total. Missing operands, missing totals, and empty line lists produce NotEvaluated, never a verified zero. Neither discrepancies nor passing rules diagnose OCR correctness or establish factual source accuracy.

### Review/export root cause and correction

The previous `Review(state)` copied arbitrary enum values into a new snapshot. It could manufacture ReadyForApproval/Approved or reopen a Rejected snapshot without a reviewer or review record. The export policy treated a non-pending state as reviewed. When `RequireHumanApproval = false`, the final-approval-record check was skipped, so direct ReadyForApproval **and** direct Approved both permitted export. Default approval-required export was already safe against Approved-without-record; a regression test proves that observation. The initial report's claim that optional approval still required explicit review was too strong and is corrected here.

The minimal correction separates `HumanReview` from `HumanApproval`. `CompleteReview(reviewerId, profile)` requires NeedsReview plus a nonblank trusted reviewer, checks tenant/profile/version identity, and stores immutable revision/version/profile-fingerprint evidence. `Approve` now additionally requires this evidence to match the current snapshot/profile. There are no public setters or constructors that attach arbitrary review/approval records to a document.

| Operation | Allowed source | Result |
| --- | --- | --- |
| `Review(NeedsReview)` | Draft, ReadyForApproval, Approved | NeedsReview; clears review, approval and acknowledgements |
| `CompleteReview(reviewer, profile)` | NeedsReview | ReadyForApproval with current review evidence |
| `Approve(approver, profile)` | ReadyForApproval with current review evidence | Approved with approval evidence |
| `Review(Rejected)` | Draft, NeedsReview, ReadyForApproval, Approved | Rejected; clears evidence and acknowledgements |
| `Edit` / `WithProfileVersion` | Any snapshot | New revision, NeedsReview, cleared evidence |

All other `Review(state)` requests throw, including direct ReadyForApproval, direct Approved, unknown enums, and every state assignment from Rejected. Rejection is terminal for that revision; deliberate content/profile revision can start a new review, which must be completed again. Reopening review explicitly also clears old warning acknowledgements. Reviewer and approver may be the same trusted identity; separation of duties is not configured in Gate 1.

`ExportEligibilityPolicy` always checks current completed review, regardless of `RequireHumanApproval`. Missing and stale evidence produce `REVIEW_EVIDENCE_MISSING` / `REVIEW_STALE`. A properly reviewed ReadyForApproval document can export when final approval is optional. An Approved state must always have an approval record, even if the final-approval requirement is optional. Default approval-required behavior is preserved.

Review evidence matches content revision and the entire profile fingerprint (including tenant/profile/version), so same-version configuration edits also make it stale. Profile changes do not mutate an existing snapshot; evaluation and approval reject its stale evidence until review is restarted/completed against the new profile. Editing clears evidence at construction of the new snapshot. Acknowledging warnings preserves current evidence but cannot erase blocking errors.

Validation still runs independently against the snapshot on each export evaluation. Pending/rejected state, rejected fields, blocking errors, required unevaluated checks, warning acknowledgement and stale approval remain enforced. Completing human review records a domain decision, not mathematical verification or factual correctness.

**Integration requirement:** reviewer/approver identity is trusted domain input in Gate 1. Any future API MUST authenticate the caller, authorize review/approval and tenant access, derive reviewer identity from the authenticated principal, and persist/audit decisions safely. Do not accept a client-supplied reviewer string or client-supplied review records as authorization. No controller, authentication, EF or cloud implementation was added here.

## D. Test-first and final verification evidence

Repository/PR audit freshly confirmed:

- PR #6 open, Draft, unmerged; head `c5de05151782dcf10c4faf8a38045a5d24898365` before hardening.
- Base/main `ab4160bf1ba39cb4f0178b421ad4642e9af12293`, same original baseline.
- Existing implementation, report and all 14 initial changed paths inspected; initial Gate 1 work preserved.

### Reproduction before implementation changes

Seven regression cases were written and compiled against the reviewed implementation **before** domain/policy corrections. The fallback xUnit runner executed all seven: **2 passed, 5 failed, 0 skipped**. Failing assertions:

- Direct ReadyForApproval assignment must throw — did not throw.
- Direct Approved assignment must throw — did not throw.
- Rejected document must reject reopening via state assignment — did not throw.
- Direct ReadyForApproval must not permit optional-approval export — actual CanExport was true.
- Direct Approved must not permit optional-approval export — actual CanExport was true.

Passing observations: Draft without completed review already could not export; default approval-required policy already blocked Approved without a record. These observations are retained as tests, not recast as vulnerabilities. An early runner invocation before the test build finished discovered zero new cases; it is excluded from evidence. The completed-build reproduction above discovered all seven and verified five real failures.

After correcting the implementation, twelve additional cases cover named-reviewer/state preconditions, identity binding, optional approval after completed review, default final approval, revision/profile invalidation including same-version profile changes, return-to-review from both completed/approved states, invalid enums, warnings and blocking errors. Existing tests now enter ReadyForApproval via actual review completion rather than unsafe enum assignment; no legacy test was changed and no assertion was weakened to accept a bypass.

### Standard commands

SDK 8.0.408 and runtime 8.0.15 were available locally in `/tmp/shoedocx-dotnet`. Commands below used that executable:

```bash
# Test-first build/run attempt, before domain changes.
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --no-restore --filter FullyQualifiedName~UniversalDocEngine

# Standard backend build (repeated only after final code changes).
dotnet build backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj

# Standard full-suite attempt at final implementation.
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj

# Separate detached original-main baseline.
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --no-restore
```

Backend build **succeeded with 0 warnings/errors**; the test project compiled successfully. Standard VSTest 17.11.1 crashed before test execution in both baseline and feature:

```text
System.NullReferenceException
Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.TestEngine.ShouldRunInProcess(...): line 625
```

Therefore there are no standard VSTest passed/failed/skipped test counts or standard-suite PASS. The host test runner is blocked; a normal-host run remains required. This session has no separate normal .NET host available. No existing GitHub Actions workflows are present; no workflow/CI configuration was created or changed, and no cloud execution environment was provisioned.

### Final fallback results (actual compiled xUnit tests)

The temporary existing `Xunit.Runners.AssemblyRunner` harness executes actual xUnit tests serially, using each built test manifest plus the runner-utility assembly. Native dependencies resolve from the manifest; no test behavior/source is replaced. Same SDK, runner, dependency strategy and original-main checkout are used for comparison.

| Execution | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Original-main baseline | 197 | 187 | 10 | 0 |
| Gate 1 including hardening | 85 | 85 | 0 | 0 |
| Full feature suite | 282 | 272 | 10 | 0 |

The final Gate 1 total adds **19 cases** to the original 66. All five reproduced weaknesses now pass their unchanged regression assertions. The exact failing test identities and messages in the full feature run match the freshly executed original-main baseline (comparison recorded below). No new legacy failures and no skips. Passing deterministic checks still establish internal consistency only.

Fallback commands (the temporary harness is not a repository artifact):

```bash
dotnet tests/ShoeExportInvoice.Tests/bin/Debug/net8.0/Gate1Runner.dll \
  "$PWD/tests/ShoeExportInvoice.Tests/bin/Debug/net8.0/ShoeExportInvoice.Tests.dll" UniversalDocEngine
dotnet tests/ShoeExportInvoice.Tests/bin/Debug/net8.0/Gate1Runner.dll \
  "$PWD/tests/ShoeExportInvoice.Tests/bin/Debug/net8.0/ShoeExportInvoice.Tests.dll"
```

Normal-host reviewers should run the standard build/test commands; no fallback harness is needed. The harness source is retained in the appendix for audit.

## E. Regression assessment and remaining risks

- No legacy services/models/controllers/frontend/tests were modified. Only isolated Gate 1 files and its report/tests changed.
- No migrations, endpoints, external API calls, authentication, tenant resolution, DI registrations, secrets, database connections, cloud changes, or deployment were added.
- Independent module compilation and source import scan succeeded; no forbidden dependencies found. `git diff --check` passed.
- Baseline failures: eight legacy cases report “Memory stream is not expandable”; two existing actual-workbook tests depend on developer-specific Windows fixture paths. The failure identities are recorded below. These existing tests were left intact.
- The standard VSTest path remains unverified because of an environment crash; direct xUnit results provide regression evidence but do not prove the host's standard test infrastructure is healthy. This is why the gate decision is conservative.
- Gate 1 is an in-memory foundation. API-bound malformed/null object deserialization, persistence, authorization of reviewer IDs, audit storage, tenant enforcement at infrastructure boundaries and Excel output remain future integration responsibilities. Reviewer identity is supplied by a trusted caller; API authentication and reviewer/approver authorization are mandatory at integration time, and are not supplied by this module. Immutable profile versions should be persisted by a later gate.
- Evidence coordinates/confidence are metadata contracts only; provider normalization is not performed.

## F. Git delivery

Feature branch: `codex/universal-doc-engine-gate-1`, targeting `main` as a Draft PR. The implementation commit and PR URL are supplied in the delivery response because a commit cannot embed its own SHA. Main is unchanged; no merge, force push or deployment. Only the feature branch is published.

## G. Gate decision

**PARTIAL** — review/export hardening and all 85 Gate 1 xUnit cases are validated; full direct-xUnit regression has no new failures, but standard VSTest regression execution remains blocked by the host crash and the existing suite still has 10 baseline failures. Do not treat this as a clean standard-suite PASS. Obtain normal-host/CI test evidence before changing the decision.

## H. Gate 2 readiness

The schema, immutable typed canonical fields, extraction/enrichment/computation separation, Excel source mapping contracts, and export eligibility policy are ready to guide a future ExcelTemplateProjector. Gate 2 must explicitly specify destination grammar, row expansion, formatting, template/version identity and file handling. It should call eligibility before output and retain provenance and approvals. No projector or Gate 2 execution has begun.

## Appendix: exact original-main versus feature failures

Fresh baseline and feature failure sets compare equal, including the entire exception message printed by the same xUnit runner. Every row below occurred in both runs, with identical text.

| Test identity | Exact exception message in both runs |
| --- | --- |
| `ShoeExportInvoice.Tests.BatchOcrTests.BatchExportZip_ShouldAllocateConsecutiveSequences_AndSaveToDb` | Memory stream is not expandable. |
| `ShoeExportInvoice.Tests.MasterDataFolderTests.ExportShipment_Sheet2_ContainsOnlyTargetPartnerAndAllDescendantProducts` | Memory stream is not expandable. |
| `ShoeExportInvoice.Tests.ProductMasterExcelImportTests.Import_ActualBook1File_AdaptiveParser_ShouldDetectColumnsAndSucceed` | File Book1.xlsx must exist at C:\Users\nhutd\Documents\antigravity\clever-faraday\backend\ShoeExportInvoice.Api\Templates\Book1.xlsx |
| `ShoeExportInvoice.Tests.ProductMasterExcelImportTests.Import_ActualWorkbook1File_AdaptiveParser_ShouldDetectColumnsAndSucceed` | File Workbook1.xlsx must exist at C:\Users\nhutd\Documents\antigravity\clever-faraday\backend\ShoeExportInvoice.Api\Templates\Workbook1.xlsx |
| `ShoeExportInvoice.Tests.TemplateSchemaTests.ExportShipmentMultiSheetExcel_WithCustomTemplateConfig_MapsCoordinatesDynamically` | Memory stream is not expandable. |
| `ShoeExportInvoice.Tests.TemplateSchemaTests.ExportShipmentMultiSheetExcel_WithDefaultTemplate_MapsCoordinatesAccurately` | Memory stream is not expandable. |
| `ShoeExportInvoice.Tests.TemplateSchemaTests.ExportShipmentToExcel_WithCustomTemplateConfig_MapsCoordinatesDynamically` | Memory stream is not expandable. |
| `ShoeExportInvoice.Tests.UnitTest1.CalculatePklBreakdown_WithDynamicPairPerCarton_CalculatesAccurately` | Memory stream is not expandable. |
| `ShoeExportInvoice.Tests.UnitTest1.ExportShipmentMultiSheetExcel_ShouldPreserveThreeSheetsAndFormulas` | Memory stream is not expandable. |
| `ShoeExportInvoice.Tests.VietnameseNumberToWordsTests.ExportShipmentMultiSheetExcel_ShouldUpdateBangChuCellInInvSheet` | Memory stream is not expandable. |

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
