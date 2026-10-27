import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('l2_discovery_gate', ROOT / 'scripts/l2-ci-gate.py')
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


class L2DiscoveryMembershipTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.commit = '1' * 40
        self.jobs = dict.fromkeys(['quality', 'targeted', 'rule_suite'], 'success')
        self.method = 'Riftbound.ConformanceTests.ExampleTests.RealAssertion'
        deferred = sorted(GATE.RULES.l3_acceptance_methods())
        rule_methods = [self.method] + [GATE.RULES.FULL_CLASS + name for name in GATE.RULES.SMOKE]
        self.discovered = set(rule_methods + deferred)
        for name, profile in [('targeted', 'focus'), ('rule_suite', 'l2')]:
            folder = self.root / name
            tests = folder / 'tests'
            shard = tests / profile
            shard.mkdir(parents=True)
            methods = [self.method] if profile == 'focus' else rule_methods
            (folder / 'checkout.json').write_text(json.dumps(dict(
                status='FIXED_CANDIDATE_CHECKOUT_VERIFIED', actualSha=self.commit, expectedSha=self.commit)))
            (tests / 'plan.json').write_text(json.dumps(dict(profile=profile, testAssemblySha256='a' * 64,
                l2Only=True, deferredL3Methods=deferred, shards=[dict(name=profile, methods=methods)])))
            (tests / 'summary.json').write_text(json.dumps(dict(status='PASSED', profile=profile, total=len(methods),
                shards=[dict(shard=profile, total=len(methods), passed=len(methods), failed=0, notExecuted=0)])))
            (tests / 'discovery.log').write_text('Tests:\n' + ''.join('    ' + method + '\n' for method in sorted(self.discovered)))
            tree = ET.Element('TestRun', xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
            results = ET.SubElement(tree, 'Results')
            for method in methods:
                ET.SubElement(results, 'UnitTestResult', testName=method, outcome='Passed')
            summary = ET.SubElement(tree, 'ResultSummary')
            ET.SubElement(summary, 'Counters', total=str(len(methods)), passed=str(len(methods)), failed='0', notExecuted='0')
            ET.ElementTree(tree).write(shard / 'results.trx', encoding='utf-8', xml_declaration=True)

    def test_focus_may_select_a_strict_subset_of_discovered_methods(self):
        self.assertLess({self.method}, self.discovered)
        result = GATE.verify(self.commit, self.jobs, self.root)
        self.assertEqual('L2_REQUIRED_PASSED', result['status'])
        self.assertEqual(1, result['results'][0]['methods'])
        self.assertFalse(result['l3SqlNetworkRestartOrFullMatrixCertified'])

    def test_matching_plan_and_passing_trx_cannot_add_an_undiscovered_method(self):
        self.assertEqual('L2_REQUIRED_PASSED', GATE.verify(self.commit, self.jobs, self.root)['status'])
        missing = 'Riftbound.ConformanceTests.OtherTests.NotInDiscovery'
        self.assertNotIn(missing, self.discovered)
        tests = self.root / 'targeted/tests'
        plan = json.loads((tests / 'plan.json').read_text())
        plan['shards'][0]['methods'] = [missing]
        (tests / 'plan.json').write_text(json.dumps(plan))
        trx_path = tests / 'focus/results.trx'
        tree = ET.parse(trx_path)
        row = tree.find('{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Results/{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
        row.set('testName', missing)
        tree.write(trx_path, encoding='utf-8', xml_declaration=True)
        with self.assertRaisesRegex(ValueError, 'absent from discovery'):
            GATE.verify(self.commit, self.jobs, self.root)
