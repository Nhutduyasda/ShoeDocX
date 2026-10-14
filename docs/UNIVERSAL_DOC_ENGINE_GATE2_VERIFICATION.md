# Gate 2 verification — 2026-10-14

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
