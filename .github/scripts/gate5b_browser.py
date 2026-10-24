from runtime_target import host_dll
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

def browser(ci, probe=None):
    feature = ci.ROOT / "feature"
    environment = os.environ.copy()
    environment.update(STUDIO_DEMO_PORT="5288", XNK_API_TARGET="http://127.0.0.1:5288", STUDIO_BASE_URL="http://127.0.0.1:5176")
    if probe: environment['STUDIO_REVIEW_PROBE'] = probe
    commands = [(["dotnet", str(host_dll(feature, 'Gate5ADemoHost'))], feature),
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
            for script, folder, summary_name, count in (("universalDocGate5B", "gate5b", "gate5b-summary", 12),):
                if probe: folder += "-before-fix-" + probe
                environment["STUDIO_EVIDENCE_DIR"] = str(ci.EVIDENCE / folder)
                result = subprocess.run(["node", f"frontend/tests/{script}.e2e.mjs"], cwd=feature, env=environment,
                                        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", timeout=180)
                if probe:
                    marker = {'ids': 'REVIEW_DUPLICATE_GENERATED_RULE_ID', 'unsaved': 'REVIEW_UNSAVED_NAVIGATION_ALLOWED'}[probe]
                    assert result.returncode != 0 and marker in result.stdout, 'Before-fix regression did not reproduce: ' + probe
                    (ci.EVIDENCE / folder / 'negative-control.json').write_text(json.dumps({'status': 'REPRODUCED', 'source_sha': '5a543b82531b082d3ac593122658151837d1fed6', 'assertion': marker}), encoding='utf-8')
                    print('Before-fix browser regression reproduced: ' + marker, flush=True)
                    continue
                if result.returncode:
                    print('\n'.join(line for line in result.stdout.splitlines() if any(token in line for token in ('Error:', 'waiting for', 'locator.', 'Expected:', 'Actual:'))), flush=True)
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


def review_regressions(ci):
    # Run the same new assertions against the actual pre-fix UI, then restore byte-for-byte.
    feature = ci.ROOT / 'feature'
    paths = [feature / 'frontend/src/features/universal-doc-engine' / name for name in
             ('studioApi.ts', 'WorkflowConfigurationEditor.tsx', 'ExampleTemplateEditor.tsx', 'UniversalDocStudio.tsx')]
    saved = {path: path.read_bytes() for path in paths}
    try:
        for path in paths:
            path.write_bytes(subprocess.check_output(['git', 'show', '5a543b82531b082d3ac593122658151837d1fed6:' + path.relative_to(feature).as_posix()], cwd=feature))
        for probe in ('ids', 'unsaved'): browser(ci, probe)
    finally:
        for path, content in saved.items(): path.write_bytes(content)
    assert subprocess.run(['git', 'diff', '--exit-code', '--', *[str(p.relative_to(feature)) for p in paths]], cwd=feature, stdout=subprocess.DEVNULL).returncode == 0, 'UI negative-control restoration failed'
