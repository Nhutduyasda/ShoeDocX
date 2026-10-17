import unittest
from gate4b2bf_linux import CHECKS, classify, evaluate, audit


class FeasibilityVerifierTests(unittest.TestCase):
    def test_missing_and_unrun_are_blocked(self):
        for report in ({},{'checks':{}},{'checks':{n:{'status':'NOT_RUN'} for n in CHECKS}}):
            self.assertEqual(evaluate(report),'BLOCKED')

    def test_schema_conjunction(self):
        good={'checks':{n:{'status':'SUPPORTED'} for n in CHECKS}}
        self.assertEqual(evaluate(good),'PASS')
        for name in CHECKS:
            for status in ('BLOCKED','NOT_RUN','UNKNOWN','HOST_RESTRICTION','FAIL'):
                with self.subTest(name=name,status=status):
                    changed={'checks':{n:dict(v) for n,v in good['checks'].items()}}
                    changed['checks'][name]['status']=status
                    self.assertNotEqual(evaluate(changed),'PASS')

    def test_trace_classification_and_redaction(self):
        result=classify('Permission denied /private/path', '12 unshare(CLONE_NEWUSER) = -1 EACCES (Permission denied)\n')
        self.assertEqual(result['category'],'SECURITY_POLICY_OR_HOST_DENIAL')
        self.assertEqual(result['denied_syscalls'],[{'operation':'unshare','errno':'EACCES'}])
        self.assertNotIn('/private',str(result))

    def test_generic_denial_is_not_namespace_proof(self):
        self.assertEqual(classify('Permission denied')['category'],'PERMISSION_DENIED_UNKNOWN_OPERATION')

    def test_audit_is_sanitized(self):
        x=audit(['apparmor="DENIED" operation="userns_create" comm="probe" error=-13 name="/private"'])
        self.assertEqual(x['events'],[{'operation':'userns_create','errno':-13}])
        self.assertNotIn('/private',str(x))


if __name__=='__main__':unittest.main()
