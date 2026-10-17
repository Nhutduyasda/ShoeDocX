"""Same-host baseline/feature VSTest identities, unchanged legacy browser checks, new real browser/export checks."""
import json
import os
from pathlib import Path
import platform
import subprocess
import tempfile
import verify_gate2 as ci
import gate4a_checks
import gate4b1_checks
from gate4b2a_inventory import PROJECTS
BASELINE = '5ff844e3761de65b14acf9a69b8a090d43a40152'

def compare(runs):
    for scope in ('engine', 'full'):
        old, new = runs['baseline-' + scope], runs['feature-' + scope]
        assert old['total'] > 0 and old['skipped'] == new['skipped'] == 0
        for identity, previous in old['tests'].items():
            assert identity in new['tests'], 'Missing baseline test: ' + identity
            current = new['tests'][identity]
            if current['outcome'] == 'Failed': assert previous == current, 'New/changed failure'
        added = new['tests'].keys() - old['tests'].keys()
        assert len(added) >= 25 and all(new['tests'][k]['outcome'] == 'Passed' and new['tests'][k]['method'].startswith(ci.ENGINE + 'Gate5ATests.') for k in added), 'New checks incomplete/failing'
    for name in ('baseline', 'feature'):
        engine, full = runs[name + '-engine'], runs[name + '-full']
        assert engine['failed'] == 0 and engine['tests'] == {k: v for k, v in full['tests'].items() if v['method'].startswith(ci.ENGINE)}

def isolation():
    changed = subprocess.check_output(['git', 'diff', '--name-only', BASELINE, 'HEAD'], cwd=ci.ROOT / 'feature', text=True).splitlines()
    prefixes = ('backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application/', 'backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Api/',
        'frontend/src/features/universal-doc-engine/', 'tests/Gate5ADemoHost/')
    added = {'backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/TemplateUnderstanding/ExampleSchemaLearner.cs',
        'tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate5ATests.cs', 'tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate5AFixtures.cs',
        'frontend/tests/universalDocExample.e2e.mjs', '.github/scripts/verify_gate5a.py', '.github/scripts/gate5a_browser.py',
        '.github/workflows/gate5a-verification.yml', 'docs/UNIVERSAL_DOC_ENGINE_GATE5A.md'}
    assert changed and all(p.startswith(prefixes) or p in added for p in changed), 'Scope violation'
    for folder in ('Application', 'Api'):
        for path in (ci.ROOT / 'feature/backend/ShoeExportInvoice.Api/Features/UniversalDocEngine' / folder).glob('*.cs'):
            if 'new ExcelTemplateProjector()' in path.read_text(): assert path.name == 'UniversalDocWorkflowOrchestrator.cs', 'Export bypass'

def main():
    report = {'gate': '5A', 'baseline_sha': BASELINE, 'feature_sha': os.environ.get('VERIFIED_HEAD'), 'os': platform.platform(), 'status': 'FAIL', 'runs': {}, 'builds': {}}
    try:
        if os.environ.get('GITHUB_ACTIONS') == 'true': (ci.ROOT / 'global.json').write_text(json.dumps({'sdk': {'version': '8.0.426', 'rollForward': 'disable'}}))
        isolation()
        for name, sha in (('baseline', BASELINE), ('feature', report['feature_sha'])):
            checkout = ci.ROOT / name
            assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=checkout, text=True).strip() == sha
            projects = list(PROJECTS) + (['tests/Gate5ADemoHost'] if name == 'feature' else [])
            for project in projects:
                csproj = next((checkout / project).glob('*.csproj'))
                result = ci.command(['dotnet', 'build', str(csproj)], checkout)
                key = name + ':' + project
                report['builds'][key] = result.returncode
                print(key + ': exit=' + str(result.returncode), flush=True)
                if result.returncode:
                    # Build diagnostics only; no test/customer document output.
                    print('\n'.join(line for line in result.stdout.splitlines() if 'error ' in line), flush=True)
                assert result.returncode == 0, 'Standard .NET 8 build failed: ' + key
        with tempfile.TemporaryDirectory(prefix='gate5a-trx-') as directory:
            runs = {}
            for name in ('baseline', 'feature'):
                for scope in ('engine', 'full'):
                    label = name + '-' + scope
                    runs[label] = ci.execute(label, ci.ROOT / name, scope == 'engine', Path(directory))
                    report['runs'][label] = {k: v for k, v in runs[label].items() if k != 'tests'}
            compare(runs)
        gate4a_checks.frontend(ci)
        gate4b1_checks.dependencies(ci)
        gate4b1_checks.browser(ci)
        import gate5a_browser
        gate5a_browser.browser(ci)
        report['status'] = 'PASS'
    except Exception as error:
        report['blocker'] = str(error) if isinstance(error, AssertionError) else type(error).__name__
    finally:
        (ci.EVIDENCE / 'summary.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(json.dumps(report, indent=2), flush=True)
    return 0 if report['status'] == 'PASS' else 1
if __name__ == '__main__': raise SystemExit(main())
