from runtime_target import host_dll
"""Gate 4A isolation and real-browser checks; no raw host logs or credentials published."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request


def isolation(feature, baseline):
    rows = subprocess.check_output(["git", "diff", "--name-status", baseline, "HEAD"], cwd=feature, text=True, encoding="utf-8").splitlines()
    modified = {"backend/ShoeExportInvoice.Api/Program.cs", "frontend/src/App.tsx"}
    prefixes = ("backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application/",
                "backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Api/", "frontend/src/features/universal-doc-engine/",
                "tests/Gate4ADemoHost/")
    added = {"frontend/tests/universalDocStudio.e2e.mjs", "tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate4AHost.cs",
             "tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate4ATests.cs", "docs/UNIVERSAL_DOC_ENGINE_GATE4A.md",
             ".github/workflows/gate4a-verification.yml", ".github/scripts/verify_gate4a.py", ".github/scripts/gate4a_checks.py"}
    for row in rows:
        status, path = row.split("\t")
        assert (status == "M" and path in modified) or (status == "A" and (path in added or path.startswith(prefixes))), "Gate 4A isolation violation"
    for path in modified:
        previous = subprocess.check_output(["git", "show", baseline + ":" + path], cwd=feature, text=True, encoding="utf-8").replace("\r\n", "\n")
        current = (feature / path).read_text(encoding="utf-8").replace("\r\n", "\n")
        if path.endswith("Program.cs"):
            current = current.replace("using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;\n", "").replace("builder.Services.AddUniversalDocEngineGate4A();\n", "")
        else:
            current = current.replace("const UniversalDocStudio = React.lazy(() => import('./features/universal-doc-engine/UniversalDocStudio'));\n\n", "")
            current = current.replace("  if (window.location.pathname.replace(/\\/$/, '') === '/universal-doc-studio') {\n    return <React.Suspense fallback={<Spin size=\"large\" />}><UniversalDocStudio /></React.Suspense>;\n  }\n\n", "")
        assert current == previous, "Existing-file edits exceed allowed integration points"
    root = feature / "backend/ShoeExportInvoice.Api/Features/UniversalDocEngine"
    callers = [path.name for folder in ("Application", "Api") for path in (root / folder).glob("*.cs")
               if "new ExcelTemplateProjector()" in path.read_text(encoding="utf-8")]
    assert callers == ["UniversalDocWorkflowOrchestrator.cs"], "Export chokepoint violation"


def frontend(ci):
    summary = {}
    for name in ("baseline", "feature"):
        directory = ci.ROOT / name / "frontend"
        for label, args in (("install", ["npm", "ci"]), ("build", ["npm", "run", "build"]), ("draft-tests", ["npm", "run", "test:drafts"])):
            # Windows resolves npm through cmd when running subprocess without a shell.
            if os.name == "nt": args[0] = "npm.cmd"
            result = ci.command(args, directory)
            assert result.returncode == 0, name + ": frontend " + label + " failed"
            summary[name + "-" + label] = "PASS"
        print(name + ": frontend lockfile install, TypeScript/build and existing draft tests passed", flush=True)
    (ci.EVIDENCE / "frontend.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")


def browser(ci):
    feature = ci.ROOT / "feature"
    result = ci.command(["dotnet", "build", "tests/Gate4ADemoHost/Gate4ADemoHost.csproj"], feature)
    assert result.returncode == 0, "Synthetic HTTP host build failed"
    environment = os.environ.copy()
    environment.update(STUDIO_DEMO_PORT="5288", XNK_API_TARGET="http://127.0.0.1:5288",
                       STUDIO_BASE_URL="http://127.0.0.1:5176", STUDIO_EVIDENCE_DIR=str(ci.EVIDENCE / "studio"))
    commands = [(["dotnet", str(host_dll(feature, 'Gate4ADemoHost'))], feature),
                (["node", str(feature / "frontend/node_modules/vite/bin/vite.js"), "--host", "127.0.0.1", "--port", "5176", "--strictPort"], feature / "frontend")]
    processes = []
    with tempfile.TemporaryDirectory(prefix="gate4a-host-logs-") as temporary:
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
            result = subprocess.run(["node", "frontend/tests/universalDocStudio.e2e.mjs"], cwd=feature, env=environment,
                                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", timeout=180)
            assert result.returncode == 0, "Real browser acceptance failed; inspect synthetic screenshot"
            summary = json.loads((ci.EVIDENCE / "studio/browser-summary.json").read_text(encoding="utf-8"))
            assert summary["status"] == "PASS" and not summary["runtimeErrors"] and len(summary["checks"]) == 13, "Incomplete browser acceptance"
            print("Real authenticated six-step browser acceptance: 13 checks PASS; zero runtime errors", flush=True)
        finally:
            for process in processes:
                if process.poll() is None: process.terminate()
            for process in processes:
                try: process.wait(timeout=10)
                except subprocess.TimeoutExpired: process.kill(); process.wait()
            for output in files: output.close()
