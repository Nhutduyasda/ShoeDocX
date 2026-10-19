"""Same-host baseline/feature VSTest identities, unchanged legacy browser checks, new real browser/export checks."""
import json
import os
from pathlib import Path
import platform
import subprocess
import tempfile
import re
import xml.etree.ElementTree as ET
import verify_gate2 as ci
import gate4a_checks
import gate4b1_checks
from gate4b2a_inventory import PROJECTS
BASELINE = '5dee473d6e791c5ff5f7312520c000860cdca275'

def discover_gate5b():
    result = ci.command(['dotnet', 'test', ci.TESTS, '--no-build', '--list-tests', '--filter',
                         'FullyQualifiedName~UniversalDocEngine.Gate5B'], ci.ROOT / 'feature')
    assert result.returncode == 0, 'Gate 5B test discovery failed'
    names = [line.strip() for line in result.stdout.splitlines() if line.strip().startswith(ci.ENGINE + 'Gate5B')]
    assert names and len(names) == len(set(names)), 'Empty/duplicate Gate 5B discovery'
    (ci.EVIDENCE / 'gate5b-discovered.json').write_text(json.dumps(sorted(names), indent=2), encoding='utf-8')
    return set(names)

def compare(runs, discovered):
    for scope in ('engine', 'full'):
        old, new = runs['baseline-' + scope], runs['feature-' + scope]
        assert old['total'] > 0 and old['skipped'] == new['skipped'] == 0
        for identity, previous in old['tests'].items():
            assert identity in new['tests'], 'Missing baseline test: ' + identity
            current = new['tests'][identity]
            if current['outcome'] == 'Failed': assert previous == current, 'New/changed failure'
        added = new['tests'].keys() - old['tests'].keys()
        assert {k.split(' | ', 1)[1] for k in added} == discovered, 'Discovered/executed Gate 5B inventory mismatch'
        assert all(new['tests'][k]['outcome'] == 'Passed' and new['tests'][k]['method'].startswith(ci.ENGINE + 'Gate5B') for k in added), 'New checks incomplete/failing'
    for name in ('baseline', 'feature'):
        engine, full = runs[name + '-engine'], runs[name + '-full']
        assert engine['failed'] == 0 and engine['tests'] == {k: v for k, v in full['tests'].items() if v['method'].startswith(ci.ENGINE)}

def isolation():
    changed = subprocess.check_output(['git', 'diff', '--name-only', BASELINE, 'HEAD'], cwd=ci.ROOT / 'feature', text=True).splitlines()
    prefixes = ('backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application/', 'backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Api/',
        'frontend/src/features/universal-doc-engine/', 'tests/Gate5ADemoHost/')
    added = {'backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/TemplateUnderstanding/ExampleSchemaLearner.cs',
        'backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/TemplateUnderstanding/ExampleRowCorrespondence.cs',
        'tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate5BReviewTests.cs', 'tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate5BTests.cs', 'tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate5BFixtures.cs', 'frontend/tests/universalDocGate5B.e2e.mjs',
        '.github/scripts/verify_gate5b.py', '.github/scripts/gate5b_browser.py', '.github/workflows/gate5b-verification.yml', 'docs/UNIVERSAL_DOC_ENGINE_GATE5B.md'}
    assert changed and all(p.startswith(prefixes) or p in added for p in changed), 'Scope violation'
    for folder in ('Application', 'Api'):
        for path in (ci.ROOT / 'feature/backend/ShoeExportInvoice.Api/Features/UniversalDocEngine' / folder).glob('*.cs'):
            if 'new ExcelTemplateProjector()' in path.read_text(): assert path.name == 'UniversalDocWorkflowOrchestrator.cs', 'Export bypass'

def main():
    report = {'gate': '5B', 'baseline_sha': BASELINE, 'feature_sha': os.environ.get('VERIFIED_HEAD'), 'os': platform.platform(), 'status': 'FAIL', 'runs': {}, 'builds': {}}
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
        discovered = discover_gate5b()
        with tempfile.TemporaryDirectory(prefix='gate5b-trx-') as directory:
            runs = {}
            for name in ('baseline', 'feature'):
                for scope in ('engine', 'full'):
                    label = name + '-' + scope
                    runs[label] = ci.execute(label, ci.ROOT / name, scope == 'engine', Path(directory))
                    report['runs'][label] = {k: v for k, v in runs[label].items() if k != 'tests'}
                    if label == 'feature-engine':
                        root = ET.parse(Path(directory) / label / 'results.trx').getroot()
                        measurements = []
                        for result in root.findall('t:Results/t:UnitTestResult', ci.NS):
                            for line in result.findtext('t:Output/t:StdOut', '', ci.NS).splitlines():
                                if 'allocation_bytes=' not in line or 'generated_cells=' not in line: continue
                                row = dict(re.findall(r'(concurrent_session|sheets|rows|wall_ms|allocation_bytes|output_bytes|generated_cells)=([0-9.]+)', line))
                                assert len(row) >= 6, 'Incomplete resource evidence'
                                measurements.append(row)
                        assert len(measurements) == 8, 'Missing bounded resource measurements'
                        (ci.EVIDENCE / 'resources.json').write_text(json.dumps(measurements, indent=2), encoding='utf-8')
                        print('Bounded synthetic resource measurements: ' + json.dumps(measurements), flush=True)
            compare(runs, discovered)
        gate4a_checks.frontend(ci)
        gate4b1_checks.dependencies(ci)
        dependency_report = json.loads((ci.EVIDENCE / 'dependencies.json').read_text())
        reviewed = {tuple(p) for p in dependency_report['nuget']['baseline']['resolved_packages']}
        assets = json.loads((ci.ROOT / 'feature/tests/Gate5ADemoHost/obj/project.assets.json').read_text(encoding='utf-8-sig'))
        host_packages = {tuple(identity.lower().split('/')) for identity, row in assets['libraries'].items() if row['type'] == 'package'}
        assert host_packages <= reviewed, 'Unreviewed synthetic-host dependency'
        gate4b1_checks.browser(ci)
        import gate5a_browser
        gate5a_browser.browser(ci)
        import gate5b_browser
        gate5b_browser.review_regressions(ci)
        gate5b_browser.browser(ci)
        report['status'] = 'PASS'
    except Exception as error:
        report['blocker'] = str(error) if isinstance(error, AssertionError) else type(error).__name__
    finally:
        (ci.EVIDENCE / 'summary.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(json.dumps(report, indent=2), flush=True)
    return 0 if report['status'] == 'PASS' else 1
if __name__ == '__main__': raise SystemExit(main())
