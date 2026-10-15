"""Gate 4A standard hosted verification; shares the unchanged Gate 2 TRX/privacy guards."""
import json
import os
from pathlib import Path
import platform
import subprocess
import tempfile
import verify_gate2 as ci
import gate4a_checks

BASELINE = "1d8c535c6a239091942a103d297ee0473c07d1fc"
NEW_TESTS = 56
NEW_CLASS = ci.ENGINE + "Gate4ATests."


def compare(runs):
    baseline, feature = runs["baseline-full"], runs["feature-full"]
    for label, expected in (("baseline-engine", 280), ("feature-engine", 280 + NEW_TESTS)):
        data = runs[label]
        assert data["passed"] == expected and data["failed"] == 0 and data["total"] == expected, label + ": engine acceptance failed"
        full = runs[label.replace("engine", "full")]["tests"]
        engine = {k: v for k, v in full.items() if v["method"].startswith(ci.ENGINE)}
        assert engine == data["tests"], label + ": full/targeted engine results differ"
    assert baseline["total"] == 477 and feature["total"] == 477 + NEW_TESTS, "Incomplete full-suite inventory"
    for identity, previous in baseline["tests"].items():
        assert identity in feature["tests"], "Missing baseline test: " + identity
        current = feature["tests"][identity]
        if current["outcome"] == "Failed":
            assert previous["outcome"] == "Failed", "New regression: " + identity
            assert current["message_sha256"] == previous["message_sha256"], "Changed failure message: " + identity
    added = set(feature["tests"]) - set(baseline["tests"])
    assert len(added) == NEW_TESTS, "Unexpected Gate 4A test count"
    assert all(feature["tests"][k]["method"].startswith(NEW_CLASS) and feature["tests"][k]["outcome"] == "Passed"
               for k in added), "Failing or unrelated new test"
    return [k for k, v in feature["tests"].items() if v["outcome"] == "Failed"]


def main():
    report = {"gate": "4A", "baseline_sha": BASELINE, "feature_sha": os.environ.get("VERIFIED_HEAD"),
              "os": platform.platform(), "status": "FAIL", "new_tests": NEW_TESTS, "runs": {}}
    try:
        (ci.ROOT / "global.json").write_text(json.dumps({"sdk": {
            "version": "8.0.426", "rollForward": "disable", "allowPrerelease": False}}), encoding="utf-8")
        report["sdk"] = subprocess.check_output(["dotnet", "--version"], text=True).strip()
        assert report["sdk"] == "8.0.426", "Wrong SDK"
        gate4a_checks.isolation(ci.ROOT / "feature", BASELINE)
        for name, expected in (("baseline", BASELINE), ("feature", report["feature_sha"])):
            checkout = ci.ROOT / name
            assert subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=checkout, text=True).strip() == expected, "Wrong checkout SHA"
            for project in (ci.API, ci.TESTS):
                result = ci.command(["dotnet", "build", project], checkout)
                assert result.returncode == 0, name + ": standard build failed: " + project
            print(name + ": standard API/test builds passed", flush=True)
        with tempfile.TemporaryDirectory(prefix="gate4a-vstest-") as directory:
            failures = []
            for name in ("baseline", "feature"):
                for scope in ("engine", "full"):
                    label = name + "-" + scope
                    try:
                        data = ci.execute(label, ci.ROOT / name, scope == "engine", Path(directory))
                        report["runs"][label] = {k: v for k, v in data.items() if k != "tests"}
                    except Exception as error:
                        failures.append(label + ": " + (str(error) if isinstance(error, AssertionError) else type(error).__name__))
            assert not failures, "; ".join(failures)
            runs = {label: json.loads((ci.EVIDENCE / (label + ".json")).read_text(encoding="utf-8")) for label in report["runs"]}
            report["matched_legacy_failures"] = compare(runs)
        gate4a_checks.frontend(ci)
        gate4a_checks.browser(ci)
        report["security_gates"] = {str(i): "PASS" for i in range(1, 6)}
        report["status"] = "PASS"
    except Exception as error:
        report["blocker"] = str(error) if isinstance(error, AssertionError) else type(error).__name__
    finally:
        (ci.EVIDENCE / "summary.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps(report, indent=2), flush=True)
        if os.environ.get("GITHUB_STEP_SUMMARY"):
            with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
                output.write("## Gate 4A standard verification\n\n```json\n" + json.dumps(report, indent=2) + "\n```\n")
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
