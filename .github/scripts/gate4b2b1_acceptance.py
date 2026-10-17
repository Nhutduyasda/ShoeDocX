"""Fail-closed host evidence evaluator. No source documents are read by this module."""
import json
from pathlib import Path
import sys

HOSTS = ('windows-2022', 'ubuntu-24.04')
SECURITY = {'network', 'read', 'write', 'spawn', 'hang', 'crash', 'memory', 'cpu',
            'oversize', 'malformed', 'incomplete', 'metadata', 'hash', 'handle-inheritance',
            'parent-death', 'descendant-cleanup', 'concurrent-admission', 'process-count',
            'wall-timeout', 'bounded-output', 'crash-recovery', 'pdf-expanded-stream-budget',
            'pdf-object-recursion-policy', 'pdf-feature-policy', 'per-page-cpu', 'native-review'}
FIXTURES = {'jpeg-baseline', 'jpeg-progressive', 'jpeg-exif-orientation', 'png-rgb', 'png-rgba',
            'png-palette', 'webp-lossy', 'webp-lossless', 'webp-alpha', 'webp-animation',
            'image-truncated', 'pdf-selectable-text', 'pdf-rotated', 'pdf-multiple-pages',
            'pdf-scanned-raster', 'pdf-layout', 'pdf-encrypted', 'pdf-action', 'pdf-external',
            'pdf-malformed', 'pdf-compressed-quota'}
REQUIRED = SECURITY | FIXTURES


def evaluate(hosts, head):
    reasons = []
    try:
        if not isinstance(head, str) or len(head) != 40 or any(c not in '0123456789abcdef' for c in head):
            raise ValueError('HEAD_INVALID')
        for host in HOSTS:
            report = hosts.get(host)
            if not report:
                reasons.append(host + ':HOST_MISSING'); continue
            if report['feature_sha'] != head:
                reasons.append(host + ':HEAD_MISMATCH')
            if report.get('status') != 'PASS':
                reasons.append(host + ':HOST_NOT_PASSED')
            enforcement = report['enforcement']
            if enforcement.get('confinementChecksCompleted') is not True or enforcement.get('blockers') != []:
                reasons.append(host + ':CONFINEMENT_OR_POLICY_INCOMPLETE')
            checks = enforcement['checks']
            names = [c['check'] for c in checks]
            if len(names) != len(set(names)):
                reasons.append(host + ':DUPLICATE_CHECK')
            passed = {c['check'] for c in checks if c['status'] == 'PASS'}
            reasons.extend(host + ':MISSING_OR_UNPASSED:' + n for n in sorted(REQUIRED - passed))
            if any(c['status'] != 'PASS' for c in checks):
                reasons.append(host + ':UNPASSED_CHECK')
            measurements = enforcement['measurements']
            operations = {m.get('operation') for m in measurements}
            if not {'hang', 'crash', 'memory', 'cpu', 'oversize', 'malformed', 'incomplete', 'metadata', 'hash'} <= operations:
                reasons.append(host + ':RESOURCE_MEASUREMENTS_ABSENT')
            measured = {m.get('fixture') for m in measurements}
            if not (FIXTURES - {'webp-animation', 'image-truncated', 'pdf-encrypted', 'pdf-action', 'pdf-external', 'pdf-malformed', 'pdf-compressed-quota'}) <= measured:
                reasons.append(host + ':DECODE_MEASUREMENTS_ABSENT')
            # Each successful real decode uses the supervisor Measurement schema.
            for m in measurements:
                v = m.get('Measurement')
                if v is None:
                    if m.get('operation') is not None and not (0 < m['peakMemoryBytes'] <= 256 * 1024 * 1024 and
                            0 <= m['cpuMs'] <= 15000 and 0 < m['wallMs'] <= 30000):
                        reasons.append(host + ':PROBE_RESOURCE_BUDGET_INVALID')
                    if m.get('fixture') is not None: reasons.append(host + ':DECODE_MEASUREMENT_MALFORMED')
                    continue
                if not (0 < v['PeakMemoryBytes'] <= 256 * 1024 * 1024 and
                        0 < v['CpuMilliseconds'] <= 15000 and 0 < v['WallMilliseconds'] <= 30000 and
                        v['ExitCode'] == 0 and 0 < v['InputBytes'] <= 1048576 and
                        0 < v['OutputBytes'] <= 81048576 + 65536):
                    reasons.append(host + ':RESOURCE_BUDGET_INVALID')
            native = report.get('native_review', {})
            if any(native.get(k) is not True for k in ('provenance', 'nested_advisories', 'notices', 'runtime_servicing')):
                reasons.append(host + ':NATIVE_REVIEW_INCOMPLETE')
            if native.get('unresolved_high_critical') != 0:
                reasons.append(host + ':NATIVE_HIGH_CRITICAL_UNRESOLVED')
    except (KeyError, TypeError, ValueError, AttributeError) as error:
        reasons.append('MALFORMED_EVIDENCE:' + type(error).__name__)
    return {'status': 'PASS' if not reasons else 'PARTIAL', 'phase2Authorized': not reasons, 'reasons': reasons}


if __name__ == '__main__':
    root, head = Path(sys.argv[1]), sys.argv[2]
    hosts = {}
    for host in HOSTS:
        path = root / ('gate4b2b1-' + host) / 'summary.json'
        if path.exists():
            try:
                hosts[host] = json.loads(path.read_text(encoding='utf-8-sig'))
            except (ValueError, OSError):
                hosts[host] = {'status': 'FAIL'}
    result = evaluate(hosts, head)
    print(json.dumps(result, indent=2))
    raise SystemExit(0 if result['status'] == 'PASS' else 1)
