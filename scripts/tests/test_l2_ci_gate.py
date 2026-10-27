import importlib.util
import itertools
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

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
        policy = patch.object(GATE.RULES, 'l3_acceptance_methods', return_value=set())
        policy.start()
        self.addCleanup(policy.stop)
        for name, profile in [('targeted', 'focus'), ('rule_suite', 'l2')]:
            folder = self.root / name
            tests = folder / 'tests'
            shard = tests / profile
            shard.mkdir(parents=True)
            methods = ['Riftbound.ConformanceTests.ExampleTests.RealAssertion']
            if profile == 'l2':
                methods += [GATE.RULES.FULL_CLASS + name for name in GATE.RULES.SMOKE]
            self.write(folder / 'checkout.json', dict(status='FIXED_CANDIDATE_CHECKOUT_VERIFIED',
                actualSha=self.commit, expectedSha=self.commit))
            self.write(tests / 'plan.json', dict(profile=profile, testAssemblySha256='a' * 64,
                l2Only=True, deferredL3Methods=[], shards=[dict(name=profile, methods=methods)]))
            self.write(tests / 'summary.json', dict(status='PASSED', profile=profile, total=len(methods),
                shards=[dict(shard=profile, total=len(methods), passed=len(methods), failed=0, notExecuted=0)]))
            (tests / 'discovery.log').write_text('Tests:\n' + ''.join('    ' + name + '\n' for name in methods))
            rows = ''.join('<UnitTestResult testName="' + name + '" outcome="Passed" />' for name in methods)
            (shard / 'results.trx').write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                + '<Results>' + rows + '</Results><ResultSummary><Counters total="' + str(len(methods))
                + '" passed="' + str(len(methods)) + '" failed="0" notExecuted="0" /></ResultSummary></TestRun>')

    def write(self, path, value):
        path.write_text(json.dumps(value))

    def verify(self):
        return GATE.verify(self.commit, self.jobs, self.root)

    def write_two_targeted_shards(self):
        tests = self.root / 'targeted/tests'
        single = 'Riftbound.ConformanceTests.ExampleTests.SingleAssertion'
        theory = 'Riftbound.ConformanceTests.ExampleTests.TheoryAssertion'
        plan = json.loads((tests / 'plan.json').read_text())
        plan['shards'] = [dict(name='focus-one', methods=[single]),
                          dict(name='focus-two', methods=[theory])]
        self.write(tests / 'plan.json', plan)
        (tests / 'discovery.log').write_text('Tests:\n    ' + single + '\n    ' + theory + '\n')
        for name, cases in [('focus-one', [single]),
                            ('focus-two', [theory + '(value: 1)', theory + '(value: 2)'])]:
            shard = tests / name
            shard.mkdir()
            rows = ''.join('<UnitTestResult testName="' + case + '" outcome="Passed" />' for case in cases)
            (shard / 'results.trx').write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                + '<Results>' + rows + '</Results><ResultSummary><Counters total="' + str(len(cases))
                + '" passed="' + str(len(cases)) + '" failed="0" notExecuted="0" /></ResultSummary></TestRun>')
        summary = dict(status='PASSED', profile='focus', total=3, shards=[
            dict(shard='focus-one', total=1, passed=1, failed=0, notExecuted=0, seconds=0.1),
            dict(shard='focus-two', total=2, passed=2, failed=0, notExecuted=0, seconds=0.2)])
        self.write(tests / 'summary.json', summary)
        return summary

    def test_both_real_profiles_are_required_but_l3_is_not_claimed(self):
        result = self.verify()
        self.assertEqual('L2_REQUIRED_PASSED', result['status'])
        self.assertEqual(['focus', 'l2'], [r['profile'] for r in result['results']])
        self.assertFalse(result['l3SqlNetworkRestartOrFullMatrixCertified'])

    def test_single_shard_summary_cannot_inflate_actual_trx_counts(self):
        self.assertEqual('L2_REQUIRED_PASSED', self.verify()['status'])
        path = self.root / 'targeted/tests/summary.json'
        summary = json.loads(path.read_text())
        summary['shards'][0].update(total=999, passed=999)
        self.assertEqual(1, summary['total'])
        self.write(path, summary)
        with self.assertRaisesRegex(ValueError, 'summary differs'):
            self.verify()

    def test_swapped_shard_counts_fail_even_when_aggregate_total_matches(self):
        summary = self.write_two_targeted_shards()
        self.assertEqual('L2_REQUIRED_PASSED', self.verify()['status'])
        summary['shards'][0].update(total=2, passed=2)
        summary['shards'][1].update(total=1, passed=1)
        self.assertEqual(3, summary['total'])
        self.assertEqual(summary['total'], sum(shard['total'] for shard in summary['shards']))
        self.write(self.root / 'targeted/tests/summary.json', summary)
        with self.assertRaisesRegex(ValueError, 'summary differs'):
            self.verify()

    def test_matching_multishard_summary_may_use_a_different_order(self):
        summary = self.write_two_targeted_shards()
        summary['shards'].reverse()
        self.write(self.root / 'targeted/tests/summary.json', summary)
        result = self.verify()
        self.assertEqual('L2_REQUIRED_PASSED', result['status'])
        self.assertEqual(2, result['results'][0]['methods'])
        self.assertEqual(3, result['results'][0]['cases'])
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

    def test_plan_cannot_silently_drop_a_discovered_mechanism_method(self):
        tests = self.root / 'rule_suite/tests'
        with (tests / 'discovery.log').open('a') as stream:
            stream.write('    Riftbound.ConformanceTests.NewMechanismTests.MustStillRun\n')
        with self.assertRaisesRegex(ValueError, 'omits a required mechanism'):
            self.verify()

    def test_extra_deferred_method_is_not_a_valid_exemption(self):
        path = self.root / 'targeted/tests/plan.json'
        plan = json.loads(path.read_text())
        plan['deferredL3Methods'] = ['Riftbound.ConformanceTests.ExampleTests.RealAssertion']
        self.write(path, plan)
        with self.assertRaisesRegex(ValueError, 'explicitly declared L3'):
            self.verify()
