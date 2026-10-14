"""PR #7 only. Standard VSTest evidence; never print/archive legacy workbook data.

Full TRX/console output stays in runner temp storage. Artifacts contain test identities,
counts and normalized error-message hashes, sufficient for exact regression comparison.
No failure allow-list: legacy failures are accepted only against this run's baseline.
"""
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET

BASELINE_SHA = "823e7d276468c8ff64711f755c21fdd9cf3250cd"
ROOT = Path.cwd()
EVIDENCE = ROOT / "verification-evidence"
EVIDENCE.mkdir(exist_ok=True)
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
ENGINE = "ShoeExportInvoice.Tests.UniversalDocEngine."
API = "backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj"
TESTS = "tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj"


def normalized_message(message):
    # Only checkout prefixes, line endings and random UUIDs are normalized.
    # Substantive text, assertion values and exception messages remain hash-sensitive.
    for folder in (ROOT / "baseline", ROOT / "feature"):
        for prefix in (str(folder), folder.as_posix()):
            message = message.replace(prefix, "<checkout>")
    message = re.sub(r"\b[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\b", "<uuid>", message)
    return "\n".join(line.rstrip() for line in message.replace("\r\n", "\n").splitlines()).strip()


def parse_trx(path):
    root = ET.parse(path).getroot()
    summary = root.find("t:ResultSummary", NS)
    assert summary is not None, "TRX result summary absent"
    counters = summary.find("t:Counters", NS)
    assert counters is not None, "TRX counters absent"
    # A failed run is permitted ONLY when it contains normal failed test results.
    assert summary.get("outcome") in ("Completed", "Passed", "Failed"), "Runner aborted"
    runner_errors = summary.findall(".//t:RunInfo[@outcome='Error']", NS)
    definitions = {}
    for unit in root.findall("t:TestDefinitions/t:UnitTest", NS):
        method = unit.find("t:TestMethod", NS)
        assert method is not None, "Missing test method identity"
        definitions[unit.get("id")] = method.get("className") + "." + method.get("name")
    results = {}
    for result in root.findall("t:Results/t:UnitTestResult", NS):
        method = definitions[result.get("testId")]
        identity = method + " | " + result.get("testName")
        outcome = result.get("outcome")
        assert identity not in results, "Duplicate test identity"
        assert outcome in ("Passed", "Failed"), "Skipped/aborted/nonexecuted test"
        message = result.findtext("t:Output/t:ErrorInfo/t:Message", "", NS)
        if outcome == "Failed":
            assert message.strip(), "Failure has no comparison message"
        results[identity] = {
            "method": method,
            "outcome": outcome,
            "message_sha256": hashlib.sha256(normalized_message(message).encode()).hexdigest(),
        }
    count = len(results)
    assert count > 0, "Runner executed zero tests"
    assert int(counters.get("total", "0")) == count, "Incomplete result set"
    assert int(counters.get("executed", "0")) == count, "Not all tests executed"
    passed = sum(r["outcome"] == "Passed" for r in results.values())
    failed = count - passed
    assert int(counters.get("passed", "0")) == passed, "Passed count mismatch"
    assert int(counters.get("failed", "0")) == failed, "Failed count mismatch"
    for key in ("error", "timeout", "aborted", "notRunnable", "notExecuted", "disconnected"):
        assert int(counters.get(key, "0")) == 0, "Runner error counter: " + key
    for info in runner_errors:
        text = info.findtext("t:Text", "", NS)
        # xUnit's standard VSTest adapter logs individual failed tests as RunInfo
        # Error too. Accept ONLY a [FAIL] diagnostic naming a recorded failed test.
        # Any other runner error still fails; complete inventories are checked below.
        recorded_failure = any(value["outcome"] == "Failed" and value["method"] in text
                               for value in results.values())
        assert "[xUnit.net" in text and "[FAIL]" in text and recorded_failure, "Runner infrastructure error"
    return {"passed": passed, "failed": failed, "skipped": 0, "total": count, "tests": results}


def command(args, checkout):
    # No console forwarding: legacy assertions can contain workbook/customer values.
    return subprocess.run(args, cwd=checkout, stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace",
                          timeout=600)


def execute(label, checkout, targeted, temporary):
    args = ["dotnet", "test", TESTS, "--no-restore", "--logger", "trx;LogFileName=results.trx",
            "--results-directory", str(temporary / label)]
    if targeted:
        args.extend(["--filter", "FullyQualifiedName~UniversalDocEngine"])
    result = command(args, checkout)
    trx = temporary / label / "results.trx"
    assert trx.is_file(), label + ": runner did not produce TRX; exit=" + str(result.returncode)
    raw = ET.parse(trx).getroot()
    summary = raw.find("t:ResultSummary", NS)
    diagnostics = {"exit_code": result.returncode,
                   "summary_outcome": summary.get("outcome") if summary is not None else None,
                   "counters": summary.find("t:Counters", NS).attrib if summary is not None and summary.find("t:Counters", NS) is not None else {},
                   "runner_errors": []}
    for info in raw.findall(".//t:RunInfo[@outcome='Error']", NS):
        text = info.findtext("t:Text", "", NS)
        diagnostics["runner_errors"].append({
            "xunit_failure_diagnostic": "[xUnit.net" in text and "[FAIL]" in text,
            "exception_types": re.findall(r"\b(?:System\.|Microsoft\.)[A-Za-z0-9_.]+Exception\b", text),
            "message_sha256": hashlib.sha256(normalized_message(text).encode()).hexdigest()})
    (EVIDENCE / (label + "-runner.json")).write_text(json.dumps(diagnostics, indent=2), encoding="utf-8")
    print(label + " runner: " + json.dumps(diagnostics), flush=True)
    data = parse_trx(trx)
    expected_exit = 1 if data["failed"] else 0
    assert result.returncode == expected_exit, label + ": exit code disagrees with TRX"
    (EVIDENCE / (label + ".json")).write_text(json.dumps(data, indent=2), encoding="utf-8")
    print(label + ": " + str({k: v for k, v in data.items() if k != "tests"}), flush=True)
    return data


def compare(runs):
    baseline, feature = runs["baseline-full"], runs["feature-full"]
    for label, expected in (("baseline-engine", 85), ("feature-engine", 139)):
        data = runs[label]
        assert data["passed"] == expected and data["failed"] == 0 and data["total"] == expected, label + ": engine acceptance failed"
        full = runs[label.replace("engine", "full")]["tests"]
        engine = {k: v for k, v in full.items() if v["method"].startswith(ENGINE)}
        assert engine == data["tests"], label + ": full/targeted engine identities or outcomes differ"
    assert baseline["total"] == 282 and feature["total"] == 336, "Unexpected full-suite test inventory"
    for identity, previous in baseline["tests"].items():
        assert identity in feature["tests"], "Baseline test disappeared: " + identity
        current = feature["tests"][identity]
        if current["outcome"] == "Failed":
            assert previous["outcome"] == "Failed", "New regression: " + identity
            assert current["message_sha256"] == previous["message_sha256"], "Changed failure message: " + identity
    added = set(feature["tests"]) - set(baseline["tests"])
    assert len(added) == 54, "Expected 54 additional Gate 2 tests"
    assert all(feature["tests"][k]["method"].startswith(ENGINE) and
               feature["tests"][k]["outcome"] == "Passed" for k in added), "New test outside Gate 2 or failing"
    return [k for k, v in feature["tests"].items() if v["outcome"] == "Failed"]


def main():
    report = {"baseline_sha": BASELINE_SHA, "feature_sha": os.environ.get("VERIFIED_HEAD"),
              "os": platform.platform(), "status": "FAIL", "runs": {}}
    try:
        # setup-dotnet installs 8 alongside newer hosted SDKs. Pin selection in the
        # disposable workspace parent, outside both source checkouts.
        (ROOT / "global.json").write_text(json.dumps({"sdk": {
            "version": "8.0.426", "rollForward": "disable", "allowPrerelease": False}}), encoding="utf-8")
        report["sdk"] = subprocess.check_output(["dotnet", "--version"], text=True).strip()
        assert report["sdk"] == "8.0.426", "Unexpected SDK"
        for name, expected in (("baseline", BASELINE_SHA), ("feature", report["feature_sha"])):
            checkout = ROOT / name
            actual = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=checkout, text=True).strip()
            assert actual == expected, "Checkout SHA mismatch"
            for project in (API, TESTS):
                result = command(["dotnet", "build", project], checkout)
                assert result.returncode == 0, name + ": build failed for " + project
            print(name + ": standard API/test builds passed", flush=True)
        with tempfile.TemporaryDirectory(prefix="gate2-vstest-") as directory:
            temporary = Path(directory)
            # Run all four even if one aborts, so both checkouts have comparable evidence.
            errors = []
            for name in ("baseline", "feature"):
                for scope in ("engine", "full"):
                    label = name + "-" + scope
                    try:
                        data = execute(label, ROOT / name, scope == "engine", temporary)
                        report["runs"][label] = {k: v for k, v in data.items() if k != "tests"}
                    except Exception as error:
                        # Only our controlled assertion messages reach artifacts, never test output.
                        errors.append(label + ": " + (str(error) if isinstance(error, AssertionError) else type(error).__name__))
            assert not errors, "; ".join(errors)
            runs = {label: json.loads((EVIDENCE / (label + ".json")).read_text(encoding="utf-8"))
                    for label in report["runs"]}
            report["matched_legacy_failures"] = compare(runs)
            report["status"] = "PASS"
    except Exception as error:
        report["blocker"] = str(error) if isinstance(error, AssertionError) else type(error).__name__
    finally:
        (EVIDENCE / "summary.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps(report, indent=2), flush=True)
        summary = os.environ.get("GITHUB_STEP_SUMMARY")
        if summary:
            with open(summary, "a", encoding="utf-8") as output:
                output.write("## Gate 2 standard verification\n\n```json\n" + json.dumps(report, indent=2) + "\n```\n")
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
