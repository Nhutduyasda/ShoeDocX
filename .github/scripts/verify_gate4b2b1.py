"""Experimental POC: compile and regress first; actual enforcement remains a mandatory failing gate."""
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
import gate4b2b1_dependencies

BASELINE = '5ff844e3761de65b14acf9a69b8a090d43a40152'


def main():
    feature = ci.ROOT / 'feature'
    report = {'gate': '4B.2B-1', 'status': 'FAIL', 'baseline_sha': BASELINE, 'feature_sha': os.environ.get('VERIFIED_HEAD'),
              'os': platform.platform(), 'phase2_authorized': False, 'runs': {}, 'builds': {}}
    try:
        (ci.ROOT / 'global.json').write_text(json.dumps({'sdk': {'version': '8.0.426', 'rollForward': 'disable'}}), encoding='utf-8')
        assert subprocess.check_output(['dotnet', '--version'], text=True).strip() == '8.0.426', 'Wrong SDK'
        changed = subprocess.check_output(['git', 'diff', '--name-only', BASELINE, 'HEAD'], cwd=feature, text=True).splitlines()
        assert changed and all(p.startswith(('tools/UniversalDocDecoder.', 'docs/gate4b2b1/')) or p in {
            'docs/UNIVERSAL_DOC_ENGINE_GATE4B2B1.md', '.github/workflows/gate4b2b1-decoder-verification.yml',
            '.github/scripts/verify_gate4b2b1.py', '.github/scripts/gate4b2b1_dependencies.py',
            '.github/scripts/gate4b2b1_acceptance.py', '.github/scripts/test_gate4b2b1_acceptance.py'} for p in changed), 'Production isolation violation'
        for name, expected in [('baseline', BASELINE), ('feature', report['feature_sha'])]:
            root = ci.ROOT / name
            assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip() == expected, 'Wrong checkout'
            projects = [ci.API, ci.TESTS, 'tests/Gate4B1DemoHost/Gate4B1DemoHost.csproj']
            if name == 'feature':
                projects += [f'tools/UniversalDocDecoder.{p}/UniversalDocDecoder.{p}.csproj' for p in ('Worker', 'Probe', 'Verifier')]
            for project in projects:
                args = ['dotnet', 'build', project]
                if 'UniversalDocDecoder.Worker' in project: args.append('-p:RestoreLockedMode=true')
                result = ci.command(args, root)
                warnings = re.findall(r'warning (\w+):', result.stdout)
                report['builds'][name + ':' + project] = {'exit_code': result.returncode,
                    'warnings': {w: warnings.count(w) for w in set(warnings)}}
                assert result.returncode == 0, 'Standard build failed: ' + name + ':' + project
        with tempfile.TemporaryDirectory(prefix='gate4b2b1-trx-') as temporary:
            runs = {}
            for name in ('baseline', 'feature'):
                for scope in ('engine', 'full'):
                    label = name + '-' + scope
                    runs[label] = ci.execute(label, ci.ROOT / name, scope == 'engine', Path(temporary))
                    report['runs'][label] = {k: v for k, v in runs[label].items() if k != 'tests'}
            for scope, count in [('engine', 409), ('full', 606)]:
                before, after = runs['baseline-' + scope], runs['feature-' + scope]
                assert before['total'] == after['total'] == count and before['tests'].keys() == after['tests'].keys(), 'Missing existing test inventory'
                for key, current in after['tests'].items():
                    if current['outcome'] == 'Failed': assert current == before['tests'][key], 'New or changed regression'
                if scope == 'engine': assert before['failed'] == after['failed'] == 0, 'Security regression'
        gate4a_checks.frontend(ci)
        gate4b1_checks.dependencies(ci)  # Existing manifests/locks and inherited findings must be unchanged.
        gate4b1_checks.browser(ci)
        gate4b2b1_dependencies.inspect(feature, ci.EVIDENCE)
        rid = 'win-x64' if os.name == 'nt' else 'linux-x64'
        runtime = ci.ROOT / 'isolated-decoder-runtime'
        for project in ('Worker', 'Probe'):
            args = ['dotnet', 'publish', f'tools/UniversalDocDecoder.{project}/UniversalDocDecoder.{project}.csproj',
                    '-r', rid, '--self-contained', 'true', '-o', str(runtime / project.lower())]
            if project == 'Worker': args.append('-p:RestoreLockedMode=true')
            result = ci.command(args, feature)
            assert result.returncode == 0, 'Self-contained publish failed'
        assert ci.command(['git', 'diff', '--exit-code', '--', 'tools/UniversalDocDecoder.Worker/packages.lock.json'], feature).returncode == 0, 'Worker publish changed lockfile'
        diagnostic = ci.command([str(runtime / 'probe' / ('UniversalDocDecoder.Probe.exe' if os.name == 'nt' else 'UniversalDocDecoder.Probe')),
                                 '--trusted-startup-diagnostic'], feature)
        report['trusted_startup_diagnostic'] = {'exit_code': diagnostic.returncode,
            'clr_started': diagnostic.stdout.strip() == 'TRUSTED_CLR_STARTUP_OK', 'security_evidence': False}
        result = ci.command(['dotnet', 'tools/UniversalDocDecoder.Verifier/bin/Debug/net8.0/UniversalDocDecoder.Verifier.dll',
                             str(runtime), 'tools/UniversalDocDecoder.Verifier/fixtures.json', str(ci.EVIDENCE / 'enforcement.json')], feature)
        assert (ci.EVIDENCE / 'enforcement.json').exists(), 'Actual enforcement run produced no evidence'
        enforcement = json.loads((ci.EVIDENCE / 'enforcement.json').read_text(encoding='utf-8'))
        report['enforcement'] = enforcement
        report['status'] = enforcement['status']
        assert result.returncode == 0 and enforcement['status'] == 'PASS' and enforcement['phase2Authorized'], 'Mandatory Phase 1 security acceptance not passed'
    except Exception as error:
        if report['status'] == 'PASS': report['status'] = 'FAIL'
        report['blocker'] = str(error) if isinstance(error, AssertionError) else type(error).__name__
    finally:
        (ci.EVIDENCE / 'summary.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(json.dumps(report, indent=2), flush=True)
    return 0 if report['status'] == 'PASS' else 1


if __name__ == '__main__':
    raise SystemExit(main())
