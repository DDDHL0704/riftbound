import copy
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('distributed_l2', ROOT / 'scripts/l2-distributed.py')
D = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(D)


class DistributedL2Tests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.prepared = self.root / 'prepared'
        self.inputs = self.root / 'shards'
        self.target = self.root / 'targeted'
        self.quality = self.root / 'quality'
        self.commit = '1' * 40
        self.fact = D.RULES.PREFIX + 'ActualTests.Plain'
        self.theory = D.RULES.PREFIX + 'ActualTests.Theory'
        self.dynamic = D.RULES.PREFIX + 'ActualTests.MemberData'
        names = [self.fact, self.theory + '(value: 1)', self.theory + '(value: 2)', self.dynamic]
        names += [D.RULES.PREFIX + f'NewTests.New{i}' for i in range(6)]
        names += [D.RULES.FULL_CLASS + name for name in D.RULES.SMOKE]
        names += sorted(D.RULES.l3_acceptance_methods())
        self.text = 'Tests:\n' + ''.join('    ' + name + '\n' for name in names)
        selected = D.RULES.l2_rule_methods(D.RULES.discover(self.text))
        bare = {D.RULES.method(name): [name] for name in names if name in selected}
        bare[self.dynamic] = [self.dynamic + '(profile: first)', self.dynamic + '(profile: second)']
        self.inventory = dict(cases=bare, modules=[dict(name=name, sha256='a' * 64,
            mvid='12345678-1234-1234-1234-123456789abc', informationalVersion='1.0.0+' + self.commit)
            for name in sorted(D.MODULES)])
        self.plan = D.make_plan(self.text, self.inventory, {self.dynamic: 100, self.theory: 50}, 6)
        self.checkout = dict(status='FIXED_CANDIDATE_CHECKOUT_VERIFIED', actualSha=self.commit,
                             expectedSha=self.commit, eventSha=self.commit, treeSha='d' * 40, runId='123', runAttempt='1')
        self.put(self.prepared / 'checkout.json', self.checkout)
        self.put(self.prepared / 'case-inventory.json', self.inventory)
        self.put(self.prepared / 'bundle-manifest.json', {name + '.dll': 'a' * 64 for name in D.MODULES})
        (self.prepared / 'discovery.log').write_text(self.text)
        self.plan.update(commit=self.commit, sdkVersion='10.0.100', runId='123', runAttempt='1', testAssemblySha256='a' * 64,
                         bundleManifestSha256=D.digest(self.prepared / 'bundle-manifest.json'),
                         caseInventorySha256=D.digest(self.prepared / 'case-inventory.json'),
                         discoverySha256=D.digest(self.prepared / 'discovery.log'))
        self.put(self.prepared / 'plan.json', self.plan)
        for shard in self.plan['shards']:
            folder = self.inputs / shard['name']
            self.put(folder / 'checkout.json', self.checkout)
            names = [case for method in shard['methods'] for case in self.plan['cases'][method]]
            self.trx(folder / 'results.trx', names)
            self.put(folder / 'module-audit.json', dict(cases={}, modules=self.inventory['modules']))
            self.put(folder / 'receipt.json', dict(status='PASSED', exitCode=0, commit=self.commit, shard=shard['name'],
                sdkVersion='10.0.100', seconds=1, planSha256=D.digest(self.prepared / 'plan.json'),
                bundleManifestSha256=self.plan['bundleManifestSha256'], testAssemblySha256='a' * 64,
                moduleAuditSha256=D.digest(folder / 'module-audit.json'),
                trxSha256=D.digest(folder / 'results.trx'), counters=dict(total=len(names), passed=len(names), failed=0, notExecuted=0)))
        self.put(self.target / 'view.json', D.derived_view(self.prepared, self.inputs, self.commit))
        self.put(self.target / 'checkout.json', self.checkout)
        self.put(self.quality / 'checkout.json', self.checkout)
        self.jobs = dict.fromkeys(['quality', 'prepare', 'rule_suite', 'targeted'], 'success')

    def put(self, path, value):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value))

    def trx(self, path, names, outcome='Passed'):
        tree = ET.Element('TestRun', xmlns=D.RULES.NS['t'])
        results = ET.SubElement(tree, 'Results')
        for name in names:
            ET.SubElement(results, 'UnitTestResult', testName=name, outcome=outcome)
        summary = ET.SubElement(tree, 'ResultSummary')
        ET.SubElement(summary, 'Counters', total=str(len(names)), passed=str(len(names) if outcome == 'Passed' else 0),
                      failed=str(len(names) if outcome == 'Failed' else 0), notExecuted=str(len(names) if outcome == 'NotExecuted' else 0))
        ET.ElementTree(tree).write(path)

    def verify(self):
        return D.aggregate(self.prepared, self.inputs, self.target, self.quality, self.commit, self.jobs)

    def test_exact_once_union_retains_smokes_and_runtime_member_data(self):
        result = self.verify()
        self.assertEqual('L2_REQUIRED_PASSED', result['status'])
        self.assertEqual(6, result['shards'])
        self.assertEqual(sum(map(len, self.plan['cases'].values())), result['cases'])
        self.assertEqual(2, len([row for row in result['rawCaseMapping'] if row['method'] == self.dynamic]))
        self.assertFalse(result['l3SqlNetworkRestartOrFullMatrixCertified'])
        self.assertEqual('DERIVED_FROM_ORIGINAL_SHARD_TRX', D.read(self.target / 'view.json')['execution'])

    def test_targeted_cli_derives_original_rows_without_executing_a_test_runner(self):
        output = self.root / 'cli-targeted'
        result = subprocess.run([sys.executable, '-B', str(ROOT / 'scripts/run-l2-targeted.py'), '--commit', self.commit,
            '--prepared', str(self.prepared), '--inputs', str(self.inputs), '--output', str(output)],
            env={**os.environ, 'PATH': ''}, capture_output=True, text=True, check=True)
        self.assertEqual('', result.stdout)
        self.assertEqual(D.read(self.target / 'view.json'), D.read(output / 'view.json'))

    def test_long_methods_are_separated_and_new_untimed_methods_are_not_dropped(self):
        shards = D.partition({'a', 'b', 'new', 'c'}, {'a': 100, 'b': 90}, 2)
        self.assertNotEqual(next(s['name'] for s in shards if 'a' in s['methods']), next(s['name'] for s in shards if 'b' in s['methods']))
        self.assertEqual({'a', 'b', 'new', 'c'}, {m for s in shards for m in s['methods']})

    def test_missing_or_repeated_parameter_row_fails_even_with_matching_trx_counters(self):
        shard = next(s for s in self.plan['shards'] if self.dynamic in s['methods'])
        folder = self.inputs / shard['name']
        rows = [case for m in shard['methods'] for case in self.plan['cases'][m]]
        for changed in [rows[:-1], rows + [rows[0]]]:
            with self.subTest(changed=changed):
                self.trx(folder / 'results.trx', changed)
                receipt = D.read(folder / 'receipt.json')
                receipt.update(trxSha256=D.digest(folder / 'results.trx'), counters=dict(total=len(changed), passed=len(changed), failed=0, notExecuted=0))
                self.put(folder / 'receipt.json', receipt)
                with self.assertRaisesRegex(ValueError, 'parameter rows'):
                    self.verify()

    def test_missing_extra_or_repeated_shard_fails(self):
        folder = self.inputs / 'shard-1'
        folder.rename(self.root / 'retained')
        with self.assertRaisesRegex(ValueError, 'shard artifacts'): self.verify()
        (self.root / 'retained').rename(folder)
        shutil.copytree(folder, self.inputs / 'extra-shard')
        with self.assertRaisesRegex(ValueError, 'shard artifacts'): self.verify()

    def test_plan_union_cannot_drop_duplicate_or_add_a_method(self):
        for kind in ['drop', 'duplicate', 'extra']:
            plan = copy.deepcopy(self.plan)
            if kind == 'drop': plan['shards'][0]['methods'].pop()
            if kind == 'duplicate': plan['shards'][1]['methods'].append(plan['shards'][0]['methods'][0])
            if kind == 'extra': plan['shards'][0]['methods'].append(D.RULES.PREFIX + 'Foreign.Tests')
            with self.subTest(kind=kind), self.assertRaisesRegex(ValueError, 'partition'):
                D.validate_plan(plan, self.text)

    def test_targeted_dropped_case_forged_origin_or_wrong_checkout_fails(self):
        original = D.read(self.target / 'view.json')
        for changed in [{**original, 'cases': original['cases'][1:]}, {**original, 'execution': 'EXECUTED_TWICE'}]:
            self.put(self.target / 'view.json', changed)
            with self.assertRaisesRegex(ValueError, 'Targeted coverage'): self.verify()
        self.put(self.target / 'view.json', original)
        self.put(self.target / 'checkout.json', {**self.checkout, 'actualSha': '2' * 40})
        with self.assertRaisesRegex(ValueError, 'Targeted view checkout'): self.verify()

    def test_wrong_sha_assembly_sdk_attempt_or_raw_trx_hash_fails(self):
        path = self.inputs / 'shard-1/receipt.json';original = D.read(path)
        for key, value in [('commit', '2' * 40), ('testAssemblySha256', 'b' * 64), ('sdkVersion', 'different'),
                           ('bundleManifestSha256', 'b' * 64), ('trxSha256', 'b' * 64), ('exitCode', 1)]:
            self.put(path, {**original, key: value})
            with self.subTest(key=key), self.assertRaises(ValueError): self.verify()
        self.put(path, original)
        self.put(path.parent / 'checkout.json', {**self.checkout, 'runAttempt': '2'})
        with self.assertRaises(ValueError): self.verify()

    def test_failed_skipped_or_missing_child_never_passes(self):
        for job in self.jobs:
            for state in ['failure', 'skipped', 'cancelled', '', 'in_progress']:
                with self.subTest(job=job, state=state), self.assertRaises(ValueError):
                    D.aggregate(self.prepared, self.inputs, self.target, self.quality, self.commit, {**self.jobs, job: state})

    def test_failed_skip_and_missing_counter_in_raw_trx_fail(self):
        folder = self.inputs / 'shard-1';path = folder / 'results.trx';original = path.read_text()
        for changed in [original.replace('outcome="Passed"', 'outcome="NotExecuted"'),
                        original.replace('outcome="Passed"', 'outcome="Failed"'), original.replace(' notExecuted="0"', '')]:
            path.write_text(changed)
            receipt = D.read(folder / 'receipt.json');receipt['trxSha256'] = D.digest(path);self.put(folder / 'receipt.json', receipt)
            with self.assertRaises(ValueError): self.verify()

    def test_unknown_impact_cannot_silently_become_filename_only_subset(self):
        plan = copy.deepcopy(self.plan);plan['targeted']['methods'] = [self.fact]
        with self.assertRaisesRegex(ValueError, 'Unknown impact'): D.validate_plan(plan, self.text)

    def test_actual_shard_module_metadata_cannot_rebind_mvid(self):
        path = self.inputs / 'shard-1/module-audit.json';audit = D.read(path)
        audit['modules'][0]['mvid'] = '99999999-1234-1234-1234-123456789abc'
        self.put(path, audit)
        receipt = D.read(path.parent / 'receipt.json');receipt['moduleAuditSha256'] = D.digest(path)
        self.put(path.parent / 'receipt.json', receipt)
        with self.assertRaisesRegex(ValueError, 'loaded-module audit'): self.verify()

    def test_prepared_requires_exact_six_unique_complete_module_identities(self):
        self.assertEqual(self.commit, D.check_prepared(self.prepared, self.commit)['commit'])
        modules = self.inventory['modules']
        bad = [[], modules[:-1], modules[:-1] + [modules[0]], modules + [modules[0]],
               [{**modules[0], 'name': 'Foreign.Module'}] + modules[1:]]
        for key, value in [('sha256', 'missing'), ('mvid', ''), ('informationalVersion', '1.0.0+wrong'), ('sha256', 'b' * 64)]:
            bad.append([{**modules[0], key: value}] + modules[1:])
        for changed in bad:
            inventory = {**self.inventory, 'modules': changed}
            self.put(self.prepared / 'case-inventory.json', inventory)
            self.put(self.prepared / 'plan.json', {**self.plan, 'caseInventorySha256': D.digest(self.prepared / 'case-inventory.json')})
            with self.subTest(modules=changed), self.assertRaises(ValueError):
                D.check_prepared(self.prepared, self.commit)

    def test_inventory_or_discovery_tampering_and_l3_exemption_fail(self):
        plan = copy.deepcopy(self.plan);plan['cases'][self.dynamic].pop()
        self.put(self.prepared / 'plan.json', plan)
        with self.assertRaisesRegex(ValueError, 'parameter rows'): self.verify()
        self.put(self.prepared / 'plan.json', self.plan)
        (self.prepared / 'discovery.log').write_text(self.text + '    ' + D.RULES.PREFIX + 'NewTests.Omitted\n')
        with self.assertRaises(ValueError): self.verify()
