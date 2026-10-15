"""Synthetic regression checks for the hosted verification acceptance guards."""
import copy
import os
import sys
from pathlib import Path
import tempfile
import unittest

# verify_gate2 initializes an evidence directory on import; keep unit fixtures disposable.
_original = Path.cwd()
sys.dont_write_bytecode = True
_temporary = tempfile.TemporaryDirectory(prefix="gate3b-ci-guards-")
os.chdir(_temporary.name)
import verify_gate3b as verifier
os.chdir(_original)


def inventory():
    engine = {f"old-engine-{i}": {"method": verifier.ci.ENGINE + f"Existing.Case{i}", "outcome": "Passed", "message_sha256": "empty"} for i in range(185)}
    legacy = {f"legacy-{i}": {"method": f"Legacy.Case{i}", "outcome": "Passed", "message_sha256": "empty"} for i in range(197)}
    added = {f"new-{i}": {"method": verifier.NEW_CLASS + f"Case{i}", "outcome": "Passed", "message_sha256": "empty"} for i in range(verifier.NEW_TESTS)}
    def run(tests):
        return {"tests": copy.deepcopy(tests), "passed": len(tests), "failed": 0, "total": len(tests), "skipped": 0}
    return {"baseline-engine": run(engine), "feature-engine": run(engine | added),
            "baseline-full": run(engine | legacy), "feature-full": run(engine | legacy | added)}


class VerificationGuards(unittest.TestCase):
    def test_valid_inventory(self):
        self.assertEqual([], verifier.compare(inventory()))

    def test_new_legacy_failure(self):
        runs = inventory()
        runs["feature-full"]["tests"]["legacy-0"]["outcome"] = "Failed"
        with self.assertRaises(AssertionError): verifier.compare(runs)

    def test_changed_legacy_message(self):
        runs = inventory()
        for label in ("baseline-full", "feature-full"):
            runs[label]["tests"]["legacy-0"]["outcome"] = "Failed"
        runs["feature-full"]["tests"]["legacy-0"]["message_sha256"] = "changed"
        with self.assertRaises(AssertionError): verifier.compare(runs)

    def test_missing_baseline_case(self):
        runs = inventory()
        del runs["feature-full"]["tests"]["legacy-0"]
        with self.assertRaises(AssertionError): verifier.compare(runs)

    def test_incomplete_engine(self):
        runs = inventory()
        runs["feature-engine"]["passed"] -= 1
        with self.assertRaises(AssertionError): verifier.compare(runs)

    def test_unrelated_new_case(self):
        runs = inventory()
        runs["feature-full"]["tests"]["new-0"]["method"] = "Unrelated.Case"
        runs["feature-engine"]["tests"]["new-0"]["method"] = "Unrelated.Case"
        with self.assertRaises(AssertionError): verifier.compare(runs)

    def test_matching_legacy_failures_accepted(self):
        runs = inventory()
        for label in ("baseline-full", "feature-full"):
            runs[label]["tests"]["legacy-0"]["outcome"] = "Failed"
        self.assertEqual(["legacy-0"], verifier.compare(runs))

    def test_zero_execution_trx_rejected(self):
        path = Path(_temporary.name) / "empty.trx"
        path.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary outcome="Completed"><Counters total="0" executed="0" passed="0" failed="0"/></ResultSummary></TestRun>', encoding="utf-8")
        with self.assertRaises(AssertionError): verifier.ci.parse_trx(path)

    def test_aborted_trx_rejected(self):
        path = Path(_temporary.name) / "abort.trx"
        path.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary outcome="Aborted"><Counters total="0"/></ResultSummary></TestRun>', encoding="utf-8")
        with self.assertRaises(AssertionError): verifier.ci.parse_trx(path)


if __name__ == "__main__":
    try: unittest.main()
    finally: _temporary.cleanup()
