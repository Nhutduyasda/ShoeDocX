"""Synthetic evaluator unit tests; never represented as OS enforcement evidence."""
import copy
import unittest
from gate4b2b1_acceptance import evaluate, HOSTS, REQUIRED, FIXTURES
HEAD = 'a' * 40


def valid():
    report = {'feature_sha': HEAD, 'status': 'PASS', 'enforcement': {
        'confinementChecksCompleted': True, 'blockers': [],
        'checks': [{'check': n, 'status': 'PASS'} for n in sorted(REQUIRED)],
        'measurements': [{'fixture': n, 'Measurement': {'PeakMemoryBytes': 10000000, 'CpuMilliseconds': 100,
                         'WallMilliseconds': 200, 'ExitCode': 0, 'InputBytes': 100, 'OutputBytes': 400}} for n in FIXTURES]},
        'native_review': dict(provenance=True, nested_advisories=True, notices=True, runtime_servicing=True, unresolved_high_critical=0)}
    report['enforcement']['measurements'] += [dict(operation=n, peakMemoryBytes=10000000, cpuMs=100, wallMs=200)
        for n in ('hang', 'crash', 'memory', 'cpu', 'oversize', 'malformed', 'incomplete', 'metadata', 'hash')]
    return {host: copy.deepcopy(report) for host in HOSTS}


class AcceptanceTests(unittest.TestCase):
    def test_complete_synthetic_schema(self):
        self.assertEqual('PASS', evaluate(valid(), HEAD)['status'])

    def test_negative_mutations(self):
        def failed_host(h): h[HOSTS[1]]['status'] = 'FAIL'
        def missing_host(h): del h[HOSTS[1]]
        mutations = [failed_host, missing_host,
            lambda h: h[HOSTS[0]].update(feature_sha='b' * 40),
            lambda h: h[HOSTS[0]]['enforcement'].update(confinementChecksCompleted=False),
            lambda h: h[HOSTS[0]]['enforcement'].update(checks=[]),
            lambda h: h[HOSTS[0]]['enforcement']['checks'][0].update(status='NOT_RUN'),
            lambda h: h[HOSTS[0]]['enforcement']['checks'][0].update(status='FAIL'),
            lambda h: h[HOSTS[0]]['enforcement']['checks'][0].update(status='BLOCKED'),
            lambda h: h[HOSTS[0]]['enforcement'].update(measurements=[]),
            lambda h: h[HOSTS[0]]['enforcement']['measurements'][0]['Measurement'].update(CpuMilliseconds=15001),
            lambda h: h[HOSTS[0]]['enforcement']['measurements'][0]['Measurement'].update(PeakMemoryBytes=300000000),
            lambda h: h[HOSTS[0]].update(native_review={}),
            lambda h: h[HOSTS[0]]['native_review'].update(unresolved_high_critical=1),
            lambda h: h[HOSTS[0]].update(enforcement=None),
            lambda h: h[HOSTS[0]]['enforcement'].update(blockers=['WORKER_STARTUP_FAILED'])]
        for i, mutate in enumerate(mutations):
            with self.subTest(i=i):
                hosts = valid(); mutate(hosts)
                self.assertNotEqual('PASS', evaluate(hosts, HEAD)['status'])
                self.assertFalse(evaluate(hosts, HEAD)['phase2Authorized'])


if __name__ == '__main__': unittest.main()
