import importlib.util
import itertools
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('l2_gate', ROOT / 'scripts/l2-ci-gate.py')
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


class L2GateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.commit = '1' * 40
        self.jobs = dict.fromkeys(['quality', 'targeted', 'rule_suite'], 'success')
        for name, profile in [('targeted', 'focus'), ('rule_suite', 'check')]:
            folder = self.root / name
            tests = folder / 'tests'
            shard = tests / profile
            shard.mkdir(parents=True)
            self.write(folder / 'checkout.json', dict(status='FIXED_CANDIDATE_CHECKOUT_VERIFIED',
                actualSha=self.commit, expectedSha=self.commit))
            self.write(tests / 'plan.json', dict(profile=profile, testAssemblySha256='a' * 64,
                shards=[dict(name=profile, methods=['Riftbound.ConformanceTests.ExampleTests.RealAssertion'])]))
            self.write(tests / 'summary.json', dict(status='PASSED', profile=profile, total=1,
                shards=[dict(shard=profile, total=1, passed=1, failed=0, notExecuted=0)]))
            (shard / 'results.trx').write_text('''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
<Results><UnitTestResult testName="Riftbound.ConformanceTests.ExampleTests.RealAssertion" outcome="Passed" /></Results>
<ResultSummary><Counters total="1" passed="1" failed="0" notExecuted="0" /></ResultSummary></TestRun>''')

    def write(self, path, value):
        path.write_text(json.dumps(value))

    def verify(self):
        return GATE.verify(self.commit, self.jobs, self.root)

    def test_both_real_profiles_are_required_but_l3_is_not_claimed(self):
        result = self.verify()
        self.assertEqual('L2_REQUIRED_PASSED', result['status'])
        self.assertEqual(['focus', 'check'], [r['profile'] for r in result['results']])
        self.assertFalse(result['l3SqlNetworkRestartOrFullMatrixCertified'])

    def test_failed_skipped_cancelled_pending_or_missing_children_never_pass(self):
        for states in itertools.product(['success', 'failure', 'skipped', 'cancelled', '', 'in_progress'], repeat=3):
            self.jobs = dict(zip(self.jobs, states))
            if states == ('success',) * 3:
                self.verify()
            else:
                with self.subTest(states=states), self.assertRaises(ValueError):
                    self.verify()

    def test_missing_or_extra_dependencies_and_unpinned_commit_fail(self):
        for commit, jobs in [('HEAD', self.jobs), (self.commit, {'quality': 'success'}),
                              (self.commit, {**self.jobs, 'postgres': 'skipped'})]:
            with self.subTest(commit=commit, jobs=jobs), self.assertRaises(ValueError):
                GATE.verify(commit, jobs, self.root)

    def test_green_summary_cannot_hide_missing_failed_skipped_or_wrong_method_trx(self):
        path = self.root / 'targeted/tests/focus/results.trx'
        original = path.read_text()
        for text in ['', original.replace('outcome="Passed"', 'outcome="Failed"'),
                     original.replace('outcome="Passed"', 'outcome="NotExecuted"'),
                     original.replace('ExampleTests.RealAssertion', 'OtherTests.Unrelated')]:
            with self.subTest(text=text):
                path.write_text(text)
                with self.assertRaises((ValueError, GATE.RULES.ET.ParseError)):
                    self.verify()
        path.unlink()
        with self.assertRaises(FileNotFoundError):
            self.verify()

    def test_wrong_checkout_or_assembly_is_not_reusable(self):
        folder = self.root / 'targeted'
        path = folder / 'checkout.json'
        original = json.loads(path.read_text())
        self.write(path, {**original, 'actualSha': '2' * 40})
        with self.assertRaises(ValueError): self.verify()
        self.write(path, original)
        path = folder / 'tests/plan.json'
        plan = json.loads(path.read_text())
        self.write(path, {**plan, 'testAssemblySha256': 'b' * 64})
        with self.assertRaises(ValueError): self.verify()

    def test_empty_duplicate_plan_or_false_summary_count_fails(self):
        path = self.root / 'targeted/tests/plan.json'
        original = json.loads(path.read_text())
        for shards in [[], [dict(name='focus', methods=[])], original['shards'] * 2]:
            self.write(path, {**original, 'shards': shards})
            with self.assertRaises(ValueError): self.verify()
        self.write(path, original)
        path = self.root / 'targeted/tests/summary.json'
        summary = json.loads(path.read_text())
        self.write(path, {**summary, 'total': 2})
        with self.assertRaises(ValueError): self.verify()
