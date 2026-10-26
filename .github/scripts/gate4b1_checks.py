from runtime_target import host_dll
"""Scoped Gate 4B.1 isolation, live advisory review, and actual authenticated browser checks."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import gate4b1_dependencies


def isolation(feature, baseline):
    rows = subprocess.check_output(["git", "diff", "--name-status", baseline, "HEAD"], cwd=feature, text=True, encoding="utf-8").splitlines()
    prefixes = ("backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application/",
                "backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Api/", "frontend/src/features/universal-doc-engine/")
    added = {"frontend/tests/universalDocIntake.e2e.mjs", "tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate4B1Tests.cs",
             "tests/Gate4B1DemoHost/Gate4B1DemoHost.csproj", "tests/Gate4B1DemoHost/Program.cs", "docs/UNIVERSAL_DOC_ENGINE_GATE4B1.md",
             ".github/workflows/gate4b1-verification.yml", ".github/scripts/verify_gate4b1.py", ".github/scripts/gate4b1_checks.py",
             ".github/scripts/gate4b1_dependencies.py"}
    assert rows, "Empty feature"
    for row in rows:
        status, path = row.split("\t")
        fixture = path == "tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate4AHost.cs" and status == "M"
        assert fixture or (status in ("A", "M") and path.startswith(prefixes)) or (status == "A" and path in added), "Gate 4B.1 isolation violation: " + path
        if fixture:
            previous = subprocess.check_output(["git", "show", baseline + ":" + path], cwd=feature, text=True, encoding="utf-8").replace("\r\n", "\n")
            current = (feature / path).read_text(encoding="utf-8").replace("\r\n", "\n")
            current = current.replace(", Action<WebApplication>? configure = null", "").replace("        configure?.Invoke(host.App);\n", "")
            assert current == previous, "Existing fixture changed beyond optional pre-start callback"
    root = feature / "backend/ShoeExportInvoice.Api/Features/UniversalDocEngine"
    callers = [path.name for folder in ("Application", "Api") for path in (root / folder).glob("*.cs")
               if "new ExcelTemplateProjector()" in path.read_text(encoding="utf-8")]
    assert callers == ["UniversalDocWorkflowOrchestrator.cs"], "Export chokepoint violation"
    print("Isolation: existing domain/extraction/export/auth/business/UI/test/lockfiles unchanged", flush=True)


def dependencies(ci):
    baseline, feature = ci.ROOT / "baseline", ci.ROOT / "feature"
    for name in ("package.json", "package-lock.json"):
        assert (baseline / "frontend" / name).read_bytes() == (feature / "frontend" / name).read_bytes(), "Dependency churn"
    reports = {name: gate4b1_dependencies.audit(ci.ROOT / name) for name in ("baseline", "feature")}
    assert reports["baseline"]["resolved_packages"] == reports["feature"]["resolved_packages"], "New or changed .NET package"
    assert reports["baseline"]["findings"] == reports["feature"]["findings"], "New or changed .NET dependency advisory"
    args = ["npm.cmd" if os.name == "nt" else "npm", "audit", "--json"]
    audit = ci.command(args, feature / "frontend")
    assert audit.returncode in (0, 1), "npm audit unavailable"
    npm = json.loads(audit.stdout)
    assert "vulnerabilities" in npm and "metadata" in npm and "error" not in npm, "npm advisory source unavailable"
    lock = json.loads((feature / "frontend/package-lock.json").read_text(encoding="utf-8"))["packages"]
    findings = [{"package": name, "severity": entry["severity"], "installed": [lock[node]["version"] for node in entry["nodes"]],
                 "affected_range": entry["range"], "via": entry["via"]} for name, entry in npm["vulnerabilities"].items()]
    report = {"nuget": reports, "npm_baseline_and_feature": findings, "npm_counts": npm["metadata"]["vulnerabilities"],
              "new_packages": 0, "new_dependency_findings": 0}
    (ci.EVIDENCE / "dependencies.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("Working official NuGet + npm advisory sources checked; unchanged dependencies; no new advisories", flush=True)


def browser(ci):
    feature = ci.ROOT / "feature"
    environment = os.environ.copy()
    environment.update(STUDIO_DEMO_PORT="5288", XNK_API_TARGET="http://127.0.0.1:5288", STUDIO_BASE_URL="http://127.0.0.1:5176")
    commands = [(["dotnet", str(host_dll(feature, 'Gate4B1DemoHost'))], feature),
                (["node", str(feature / "frontend/node_modules/vite/bin/vite.js"), "--host", "127.0.0.1", "--port", "5176", "--strictPort"], feature / "frontend")]
    processes = []
    with tempfile.TemporaryDirectory(prefix="gate4b1-host-logs-") as temporary:
        files = []
        try:
            for i, (args, cwd) in enumerate(commands):
                output = open(Path(temporary) / f"host-{i}.log", "wb"); files.append(output)
                processes.append(subprocess.Popen(args, cwd=cwd, env=environment, stdout=output, stderr=subprocess.STDOUT))
            for url in ("http://127.0.0.1:5288/api/universal-doc/bootstrap", "http://127.0.0.1:5176/universal-doc-studio"):
                deadline = time.monotonic() + 90
                while True:
                    assert all(p.poll() is None for p in processes), "Synthetic backend/frontend exited"
                    try:
                        with urllib.request.urlopen(url, timeout=2): break
                    except urllib.error.HTTPError as error:
                        if error.code == 401: break
                        raise AssertionError("Unexpected readiness status") from None
                    except (urllib.error.URLError, TimeoutError):
                        assert time.monotonic() < deadline, "Synthetic host startup timed out"
                        time.sleep(.2)
            for script, folder, summary_name, count in (("universalDocStudio", "demo", "browser-summary", 13), ("universalDocIntake", "intake", "intake-summary", 11)):
                environment["STUDIO_EVIDENCE_DIR"] = str(ci.EVIDENCE / folder)
                result = subprocess.run(["node", f"frontend/tests/{script}.e2e.mjs"], cwd=feature, env=environment,
                                        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", timeout=180)
                assert result.returncode == 0, "Real browser acceptance failed: " + script + "; inspect synthetic screenshot"
                summary = json.loads((ci.EVIDENCE / folder / (summary_name + ".json")).read_text(encoding="utf-8"))
                assert summary["status"] == "PASS" and not summary["runtimeErrors"] and len(summary["checks"]) == count, "Incomplete browser acceptance"
                print(script + f": {count} actual browser checks PASS; zero runtime exceptions", flush=True)
        finally:
            for process in processes:
                if process.poll() is None: process.terminate()
            for process in processes:
                try: process.wait(timeout=10)
                except subprocess.TimeoutExpired: process.kill(); process.wait()
            for output in files: output.close()
