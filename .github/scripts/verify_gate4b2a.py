"""Exact-head Gate 4B.2A verification; retain prior VSTest and authenticated browser checks."""
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import tempfile
import verify_gate2 as ci
import gate4a_checks
import gate4b1_checks
import gate4b1_dependencies
from gate4b2a_inventory import inventory, PROJECTS

BASELINE = '21e48a5e2ddb426ccf57290c2e8407d8d00f774e'
ADDED = 15


def delta(before, after):
    return {key: [before.get(key), after.get(key)] for key in sorted(before.keys() | after.keys()) if before.get(key) != after.get(key)}


def package_versions(data):
    return {p: {name.split('/')[0]: name.split('/')[1] for name, row in next(iter(value['targets'].values())).items()
                if row.get('type') == 'package'} for p, value in data['dotnet'].items()}


def dependency_checks():
    data = {name: inventory(ci.ROOT / name) for name in ('baseline', 'feature')}
    for name, value in data.items():
        (ci.EVIDENCE / (name + '-inventory.json')).write_text(json.dumps(value, indent=2), encoding='utf-8')
    expected = json.loads((ci.ROOT / 'feature/docs/gate4b2a/dependency-delta.json').read_text(encoding='utf-8'))
    versions = {name: package_versions(value) for name, value in data.items()}
    actual = {p: delta(versions['baseline'][p], versions['feature'][p]) for p in PROJECTS}
    assert actual == expected['dotnet'], 'Unreviewed .NET dependency change'
    assert delta(data['baseline']['npm'], data['feature']['npm']) == expected['npm'], 'Unreviewed npm dependency change'
    nuget = {name: gate4b1_dependencies.audit(ci.ROOT / name) for name in data}
    def keys(rows):
        return {(x['package'], x['url'], x['severity']) for x in rows}
    assert not keys(nuget['feature']['findings']) - keys(nuget['baseline']['findings']), 'New NuGet advisory'
    assert {x['package'] for x in nuget['feature']['findings']} == {'sqlitepclraw.lib.e_sqlite3'}, 'Unexpected unresolved NuGet finding'
    npm = {}
    for name in data:
        result = ci.command(['npm.cmd' if os.name == 'nt' else 'npm', 'audit', '--json'], ci.ROOT / name / 'frontend')
        assert result.returncode in (0, 1), 'npm audit unavailable'
        npm[name] = json.loads(result.stdout)
        assert 'vulnerabilities' in npm[name] and 'error' not in npm[name], 'Incomplete npm audit'
    assert not set(npm['feature']['vulnerabilities']) - set(npm['baseline']['vulnerabilities']), 'New npm vulnerability'
    assert 'source-map-js' not in npm['feature']['vulnerabilities'], 'source-map-js remediation failed'
    allowed = {'braces', 'chokidar', 'fast-glob', 'micromatch', 'postcss-nested', 'postcss-selector-parser', 'tailwindcss'}
    assert set(npm['feature']['vulnerabilities']) <= allowed, 'Unclassified npm finding'
    rank = {'low': 0, 'moderate': 1, 'high': 2, 'critical': 3}
    for name, entry in npm['feature']['vulnerabilities'].items():
        old = npm['baseline']['vulnerabilities'][name]
        assert rank[entry['severity']] <= rank[old['severity']], 'Worsened npm severity'
        direct = lambda e: {v['url'] for v in e['via'] if isinstance(v, dict)}
        assert direct(entry) <= direct(old), 'New npm advisory on existing package'
    report = {'nuget': nuget, 'npm': npm, 'reviewed_delta': actual, 'production_ready': False}
    (ci.EVIDENCE / 'dependencies.json').write_text(json.dumps(report, indent=2), encoding='utf-8')


def compare(runs):
    for scope in ('engine', 'full'):
        old, new = runs['baseline-' + scope], runs['feature-' + scope]
        assert old['total'] == {'engine': 394, 'full': 591}[scope], 'Missing baseline inventory'
        assert new['total'] == old['total'] + ADDED, 'Incomplete inventory'
        for identity, previous in old['tests'].items():
            assert identity in new['tests'], 'Missing baseline test'
            current = new['tests'][identity]
            if current['outcome'] == 'Failed':
                assert previous == current, 'New/changed failure'
        added = new['tests'].keys() - old['tests'].keys()
        assert len(added) == ADDED and all(new['tests'][k]['outcome'] == 'Passed' and
            new['tests'][k]['method'].startswith(ci.ENGINE + 'Gate4B2ATests.') for k in added), 'Unverified new checks'
    for name in ('baseline', 'feature'):
        engine, full = runs[name + '-engine'], runs[name + '-full']
        assert engine['failed'] == 0, 'Security test failure'
        assert engine['tests'] == {k: v for k, v in full['tests'].items() if v['method'].startswith(ci.ENGINE)}, 'Targeted/full mismatch'


def isolation():
    allowed = {'frontend/package-lock.json', 'tests/ShoeExportInvoice.Tests/ShoeExportInvoice.Tests.csproj',
        'tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate4B2ATests.cs',
        'tests/ShoeExportInvoice.Tests/UniversalDocEngine/Gate4B2AFixtures.json',
        '.github/workflows/gate4b2a-verification.yml', 'docs/UNIVERSAL_DOC_ENGINE_GATE4B2A.md'}
    changed = subprocess.check_output(['git', 'diff', '--name-only', BASELINE, 'HEAD'], cwd=ci.ROOT / 'feature', text=True).splitlines()
    assert changed and all(p in allowed or p.startswith('docs/gate4b2a/') or
        p in {'.github/scripts/' + n for n in ('verify_gate4b2a.py', 'gate4b2a_inventory.py', 'gate4b2a_fixtures.py')} for p in changed), 'Isolation violation'
    # All production source, previous tests, browser acceptance and prior security scripts are byte-identical.


def main():
    report = {'gate': '4B.2A', 'baseline_sha': BASELINE, 'feature_sha': os.environ.get('VERIFIED_HEAD'),
              'os': platform.platform(), 'status': 'FAIL', 'production_ready': False, 'runs': {}, 'builds': {}}
    try:
        if os.environ.get('GITHUB_ACTIONS') == 'true':
            (ci.ROOT / 'global.json').write_text(json.dumps({'sdk': {'version': '8.0.426', 'rollForward': 'disable'}}), encoding='utf-8')
        isolation()
        sdk = subprocess.check_output(['dotnet', '--version'], text=True).strip()
        assert sdk.startswith('8.0.'), 'Standard .NET 8 SDK required'
        report['sdk'] = sdk
        for name, sha in (('baseline', BASELINE), ('feature', report['feature_sha'])):
            checkout = ci.ROOT / name
            assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=checkout, text=True).strip() == sha, 'Wrong SHA'
            for project in PROJECTS:
                csproj = next((checkout / project).glob('*.csproj'))
                for operation in ('restore', 'build'):
                    result = ci.command(['dotnet', operation, str(csproj)], checkout)
                    warnings = re.findall(r'warning (\w+):', result.stdout)
                    key = name + ':' + project + ':' + operation
                    report['builds'][key] = {'exit_code': result.returncode,
                        'warning_occurrences': {code: warnings.count(code) for code in sorted(set(warnings))},
                        'error_occurrences': len(re.findall(r'error \w+:', result.stdout))}
                    print(key + ': ' + json.dumps(report['builds'][key]), flush=True)
                    assert result.returncode == 0, name + ': standard ' + operation + ' failed'
        with tempfile.TemporaryDirectory(prefix='gate4b2a-trx-') as directory:
            runs = {}
            for name in ('baseline', 'feature'):
                for scope in ('engine', 'full'):
                    label = name + '-' + scope
                    runs[label] = ci.execute(label, ci.ROOT / name, scope == 'engine', Path(directory))
                    report['runs'][label] = {k: v for k, v in runs[label].items() if k != 'tests'}
            compare(runs)
        gate4a_checks.frontend(ci)
        dependency_checks()
        gate4b1_checks.browser(ci)
        report['security_gates'] = {str(i): 'PASS' for i in range(1, 6)}
        report['status'] = 'PASS'
    except Exception as error:
        report['blocker'] = str(error) if isinstance(error, AssertionError) else type(error).__name__
    finally:
        (ci.EVIDENCE / 'summary.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(json.dumps(report, indent=2), flush=True)
    return 0 if report['status'] == 'PASS' else 1


if __name__ == '__main__':
    raise SystemExit(main())
