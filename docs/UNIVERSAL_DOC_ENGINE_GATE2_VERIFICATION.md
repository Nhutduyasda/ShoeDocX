# Gate 2 verification — 2026-10-14

**Final decision: PASS.** The final standard CI evidence below supersedes the earlier Work-host PARTIAL investigations, retained as history.

## Verified Git baseline

- Starting feature HEAD: `c6e70b4e08d290db2b089642f8910acbf5cbbfc0`.
- Main / accepted Gate 1 baseline: `823e7d276468c8ff64711f755c21fdd9cf3250cd`.
- PR #7: `codex/universal-doc-engine-gate-2` → `main`, open, non-draft.
- No other PR, merge, deployment or Gate 3 work.

## Required commands

Run from the relevant baseline/feature checkout using .NET 8:

```sh
dotnet build backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --filter "FullyQualifiedName~UniversalDocEngine"
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj
```

Environment: Ubuntu 24.04 linux-x64; SDK 8.0.426, .NET runtime 8.0.32; ClosedXML 0.104.2. Also tried SDK 8.0.408. SDK installation is temporary, outside the repository.

| Evidence | Passed | Failed | Skipped | Result |
|---|---:|---:|---:|---|
| Baseline standard build | — | — | — | PASS; zero warnings/errors |
| Feature standard API and test-project builds | — | — | — | PASS; zero warnings/errors |
| Standard VSTest baseline full | — | — | — | BLOCKED before execution |
| Standard VSTest feature targeted/full | — | — | — | BLOCKED before execution |
| Supplementary direct xUnit Gate 1 | 85 | 0 | 0 | PASS |
| Supplementary direct xUnit Gate 2 | 54 | 0 | 0 | PASS |
| Supplementary direct xUnit combined | 139 | 0 | 0 | PASS |
| Supplementary direct xUnit baseline full | 272 | 10 | 0 | Baseline failures |
| Supplementary direct xUnit feature full | 326 | 10 | 0 | Identical failed-test set |

Standard VSTest 17.11.1 aborts with `System.NullReferenceException` in `Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.TestEngine.ShouldRunInProcess`, before any test runs. Diagnostic tracing also encountered a null reference in `PlatformEqtTrace.EnsureTraceIsInitialized`. One CLI attempt failed reading `Process.StartTime`; retry reached the same VSTest failure. Setting DOTNET_ROOT/PATH and disabling tiered compilation/ReadyToRun did not resolve it. SDK 8.0.408 reproduced the same VSTest blocker. These results must not be reported as standard test passes.

An early fallback run used the wrong runner base directory and reported 16 legacy failures. That exploratory count is superseded: placing the runner beside each built test assembly restores the expected fixture base directory and yields the final 10 matching failures below. Native SQLite was resolved through the build output's linux-x64 native directory, in both checkouts.

## Remaining baseline failures (same names on feature)

Eight report `Memory stream is not expandable.`:

- `VietnameseNumberToWordsTests.ExportShipmentMultiSheetExcel_ShouldUpdateBangChuCellInInvSheet`
- `UnitTest1.CalculatePklBreakdown_WithDynamicPairPerCarton_CalculatesAccurately`
- `UnitTest1.ExportShipmentMultiSheetExcel_ShouldPreserveThreeSheetsAndFormulas`
- `TemplateSchemaTests.ExportShipmentMultiSheetExcel_WithCustomTemplateConfig_MapsCoordinatesDynamically`
- `TemplateSchemaTests.ExportShipmentMultiSheetExcel_WithDefaultTemplate_MapsCoordinatesAccurately`
- `TemplateSchemaTests.ExportShipmentToExcel_WithCustomTemplateConfig_MapsCoordinatesDynamically`
- `MasterDataFolderTests.ExportShipment_Sheet2_ContainsOnlyTargetPartnerAndAllDescendantProducts`
- `BatchOcrTests.BatchExportZip_ShouldAllocateConsecutiveSequences_AndSaveToDb`

Two require unavailable developer-machine Windows fixture paths:

- `ProductMasterExcelImportTests.Import_ActualWorkbook1File_AdaptiveParser_ShouldDetectColumnsAndSucceed`
- `ProductMasterExcelImportTests.Import_ActualBook1File_AdaptiveParser_ShouldDetectColumnsAndSucceed`

No legacy service or test was modified to hide these failures. All new Gate 2 tests build synthetic workbooks in memory.

## Supplementary runner reproduction

This is additional evidence only; it does **not** replace mandatory standard VSTest acceptance. Build this temporary project outside the source tree, then copy its generated runner DLL, runtimeconfig and deps JSON beside each built test DLL. Use the existing xUnit utility DLL in that folder. Supply `LD_LIBRARY_PATH=<test-output>/runtimes/linux-x64/native` on Linux so SQLite resolves. Invoke `dotnet <test-output>/runner.dll <absolute-test-dll>`; append `UniversalDocEngine` to filter display names. Both baseline and feature used identical runner code and environment.

```xml
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><PackageReference Include="xunit.runner.utility" Version="2.5.3" /><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup></Project>
```

```csharp
using Xunit;
using Xunit.Abstractions;
using System.Reflection;
var path = Path.GetFullPath(args[0]);
System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) => {
var file = Path.Combine(Path.GetDirectoryName(path)!, name.Name + ".dll");
return File.Exists(file) ? context.LoadFromAssemblyPath(file) : null;
};
using var controller = new XunitFrontController(AppDomainSupport.Denied, path);
using var discovery = new TestDiscoverySink();
controller.Find(false, discovery, TestFrameworkOptions.ForDiscovery());
discovery.Finished.WaitOne();
var cases = discovery.TestCases.Where(t => args.Length < 2 || t.DisplayName.Contains(args[1])).ToList();
using var sink = new Sink();
controller.RunTests(cases, sink, TestFrameworkOptions.ForExecution());
sink.Done.WaitOne();
Console.WriteLine($"Passed={sink.Passed} Failed={sink.Failed} Skipped={sink.Skipped}");
return sink.Failed == 0 ? 0 : 1;
class Sink : IMessageSink, IDisposable {
 public ManualResetEvent Done = new(false); public int Passed, Failed, Skipped;
 public bool OnMessage(IMessageSinkMessage message) {
  if(message is ITestPassed) Passed++;
  if(message is ITestSkipped) Skipped++;
  if(message is ITestFailed f) { Failed++; Console.WriteLine("FAIL " + f.Test.DisplayName + "\n" + string.Join("\n",f.Messages) + "\n" + string.Join("\n",f.StackTraces)); }
  if(message is IErrorMessage e) Console.WriteLine("ERROR " + string.Join("\n",e.Messages));
  if(message is ITestAssemblyFinished) Done.Set(); return true;
 }
 public void Dispose() => Done.Dispose();
}
```

## Acceptance decision

Implementation and supplementary synthetic workbook assertions pass. Standard test execution remains blocked. **PARTIAL / NOT MERGE READY**. Leave PR #7 open and non-draft; rerun mandatory commands in a working standard .NET 8 test environment, compare the exact baseline failed-test set, and merge only when the owner's acceptance conditions are fulfilled.


## Follow-up: standard isolation verification (2026-10-14)

Read the latest owner review: https://github.com/Nhutduyasda/ShoeDocX/pull/7#issuecomment-6163481195 . The reviewed implementation remains `37f858633166a90a49e442ccbc71008cf3374f54`; baseline remains accepted main `823e7d276468c8ff64711f755c21fdd9cf3250cd`. This follow-up changes documentation/evidence only, with no application, tests, dependencies or CI modifications.

Environment: the same Ubuntu 24.04 linux-x64 host, .NET SDK 8.0.426 / runtime 8.0.32 / VSTest 17.11.1. PATH and DOTNET_ROOT explicitly reference the installed SDK. Baseline and feature use identical SDK, runsettings and command patterns in separate checkouts.

Commands executed in **both** baseline and feature checkouts:

```sh
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --filter "FullyQualifiedName~UniversalDocEngine" --logger "trx;LogFileName=<label>-isolated.trx" -- RunConfiguration.InIsolation=true
dotnet test tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj --logger "trx;LogFileName=<label>-full-isolated.trx" -- RunConfiguration.InIsolation=true
```

Direct standard VSTest also attempted on feature:

```sh
dotnet vstest tests/ShoeExportInvoice.Tests/bin/Debug/net8.0/ShoeExportInvoice.Tests.dll --InIsolation --TestCaseFilter:FullyQualifiedName~UniversalDocEngine --Logger:"trx;LogFileName=gate2-vstest-isolated.trx"
```

All five attempts returned exit code 1, matched one test assembly and then **aborted before executing any tests**. TRX result summary says Failed with total/executed/passed/failed counters all zero; those zero failed-test counters do not indicate a passing run.

Isolation bypassed the original `TestEngine.ShouldRunInProcess` failure but exposed a second host problem:

```text
System.NullReferenceException
Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Hosting.DotnetTestHostManager
  .<GetTestHostProcessStartInfo>g__IsRunningWithDotnetMuxer|48_0(String currentProcessPath)
  DotnetTestHostManager.cs:536
GetTestHostProcessStartInfo:436
ProxyOperationManager.SetupChannel:226
ProxyExecutionManager.InitializeTestRun:182
```

The upstream VSTest v17.11.1 implementation at https://github.com/microsoft/vstest/blob/v17.11.1/src/Microsoft.TestPlatform.TestHostProvider/Hosting/DotnetTestHostManager.cs obtains `currentProcessPath` from `GetCurrentProcessFileName()` then unconditionally calls `.EndsWith` in `IsRunningWithDotnetMuxer`. This stack is consistent with a null process path on this host. It occurs before the custom dotnet host-path branch, so merely passing a custom host path would not bypass that call. No runner binaries or tests were patched.

Raw logs, original TRX files and SHA-256 manifest are in `docs/verification/gate2-isolation-20261014/`.

### Independent-host attempt

- No GitHub workflow run exists for either reviewed HEAD or baseline; no workflow files are present in the repository checkout. No accessible Windows host, Docker or Podman runtime is available in this session.
- Applied the Vercel Sandbox skill to attempt a short-lived, nonpersistent independent Linux VM. A scoped create request using an existing account project returned **403 Forbidden: not authorized for the requested scope**. No sandbox was created. No Vercel CLI or alternate Vercel credential is available here. Independent testing therefore could not be executed.
- CI configuration was not created or changed, as separately required by the owner. No infrastructure or application deployment occurred.

### Supplementary workbook/regression recheck

The documented direct xUnit fallback was rerun against the unchanged reviewed assemblies. It is explicitly supplementary, not a replacement for standard VSTest:

| Scope | Passed | Failed | Skipped |
|---|---:|---:|---:|
| UniversalDocEngine (85 Gate 1 + 54 Gate 2) | 139 | 0 | 0 |
| Accepted baseline full suite | 272 | 10 | 0 |
| Reviewed feature full suite | 326 | 10 | 0 |

Compared exact failed-test identities **and their first failure messages**: equal. No new legacy failures appeared in supplementary execution. The same synthetic Excel integrity assertions pass again: reopened workbooks, relative/absolute/mixed formulas, footer/totals after expansion, typed values, Unicode/literal text, styles, merges, worksheets and template/review identity safety.

**Decision remains PARTIAL / DO NOT MERGE.** Standard tests still execute zero tests, and an independent standard runner is inaccessible. To unblock, provide an accessible clean .NET 8 Windows/CI runner with repository access, or separately authorize a concrete test-only CI workflow. Existing PR #7 remains open, non-draft, targeting main. No Gate 3 work.


## Owner-authorized standard CI verification

The owner explicitly authorized a minimal test-only GitHub Actions workflow for PR #7. `.github/workflows/gate2-verification.yml` runs only the existing in-repository Gate 2 PR targeting main, using GitHub-hosted Ubuntu 24.04 and Windows Server 2022 with SDK 8.0.426. Both accepted baseline `823e7d276468c8ff64711f755c21fdd9cf3250cd` and the exact PR HEAD are checked out separately; checkout credentials are not persisted. Workflow permissions are `contents: read` only. No deployment secrets or credentials are used.

`.github/scripts/verify_gate2.py` executes standard API/test-project builds and targeted/full `dotnet test` in both checkouts under identical configuration on each runner. It requires 85 passing baseline engine tests, 139 passing feature engine tests, full-suite inventories of 282/336, matching targeted/full engine identities, and no new failures or changed legacy failure messages. Aborted/empty/incomplete/skipped results, infrastructure errors, build failures and inconsistent exit codes fail the job. Expected legacy failures are compared against the freshly executed baseline rather than a hard-coded allow-list or `continue-on-error`.

Full legacy console output and raw TRX stay in temporary runner storage and are never uploaded or printed because legacy assertions may contain template values. Artifacts contain counts, test identities and SHA-256 hashes of complete normalized failure messages. Normalization removes only checkout prefixes, line-ending differences and random UUIDs. No application/test assertions were changed; all new Gate 2 scenarios remain synthetic. The comparison guards were exercised locally with synthetic Python/TRX data; no further local VSTest experiments were performed.

CI result and merge decision remain pending until both runner jobs finish and their exact tested HEAD is verified.


## Final independent standard CI result — PASS

- Run: https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38532488449
- Ubuntu job: https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38532488449/job/115747399683
- Windows job: https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38532488449/job/115747399577
- Tested HEAD: `9588439290c13431057374cedf3cd1281a2b2ef9`.
- Both jobs and the overall run concluded **success**.

| Host | Baseline targeted P/F/S | Gate 2 targeted P/F/S | Baseline full P/F/S | Gate 2 full P/F/S |
|---|---|---|---|---|
| Ubuntu 24.04 | 85/0/0 | 139/0/0 | 272/10/0 | 326/10/0 |
| Windows Server 2022 | 85/0/0 | 139/0/0 | 280/2/0 | 334/2/0 |

Standard API and test-project builds passed on both checkouts/platforms. All 139 UniversalDocEngine tests also passed within each full suite. Baseline full inventory is 282 and feature inventory 336; all 54 added Gate 2 cases pass. Failure identity and full normalized message SHA-256 comparisons passed independently on each OS. No new legacy failure was introduced. The two Windows failures are the existing hard-coded Book1/Workbook1 developer-machine fixture paths. Ubuntu additionally retains the eight baseline fixed-capacity stream failures listed above. Test assertions and application/legacy source were not modified during CI troubleshooting.

The first CI run (https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38532029670) correctly rejected the hosted default SDK 10 selection. The harness now writes an SDK 8.0.426 global.json in the disposable workspace parent, outside both source checkouts. The next run (https://github.com/Nhutduyasda/ShoeDocX/actions/runs/38532151382) obtained all targeted passes but initially treated xUnit's recorded `[FAIL]` RunInfo diagnostics as infrastructure errors. The final harness accepts those diagnostics only if they name an actually recorded failed test; all other error diagnostics still fail, and complete test inventories, counters and exit codes remain mandatory. No application/test behavior was weakened.

Sanitized artifacts in the successful run contain complete test identities/outcomes, counts, runner counters and full normalized error-message hashes. Raw legacy workbook values, raw test console output and full legacy TRX are not published. All new Gate 2 workbook fixtures remain synthetic and in memory.

**Gate 2 PASS:** explicit mapping, source immutability, typed values, literal unsafe text, template/layout/review binding, eligibility enforcement, row expansion, relative/absolute/mixed formulas, styles, merged footer, multi-sheet integrity and negative cases pass through standard VSTest on both platforms. Supported/unsupported feature boundaries remain as documented. The final documentation-only HEAD is also required to pass the same workflow before merge. No deploy, migration, unrelated business logic or Gate 3 work.
