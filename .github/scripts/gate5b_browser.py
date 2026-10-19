import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

def browser(ci):
    feature = ci.ROOT / "feature"
    environment = os.environ.copy()
    environment.update(STUDIO_DEMO_PORT="5288", XNK_API_TARGET="http://127.0.0.1:5288", STUDIO_BASE_URL="http://127.0.0.1:5176")
    commands = [(["dotnet", str(feature / "tests/Gate5ADemoHost/bin/Debug/net8.0/Gate5ADemoHost.dll")], feature),
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
            for script, folder, summary_name, count in (("universalDocGate5B", "gate5b", "gate5b-summary", 10),):
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
