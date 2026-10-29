import copy
import hashlib
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
        self.scope = dict(schemaVersion=1, sourceHead=self.commit,
            discoverySha256=hashlib.sha256(self.text.encode()).hexdigest(),
            discoveredMethods=len(D.RULES.discover(self.text)), declaredTestMethods=len(D.RULES.discover(self.text)),
            unmatchedDeclaredMethods=[], unmatchedDiscoveredMethods=[], sourceRelatedMethodCount=3, sourceGuardBatchCount=2,
            semanticTestsExecuted=False, existingLanesChanged=False, unknownMethodsRemainInExistingRuleSuite=True,
            methods=[dict(method=self.fact, includeInSourceGuardBatch=True, existingLane='L2'),
                     dict(method=self.dynamic, includeInSourceGuardBatch=False, existingLane='L2'),
                     dict(method=sorted(D.RULES.l3_acceptance_methods())[0], includeInSourceGuardBatch=True, existingLane='L3')])
        self.plan = D.make_plan(self.text, self.inventory, self.scope, {self.dynamic: 100, self.theory: 50}, 6)
        self.checkout = dict(status='FIXED_CANDIDATE_CHECKOUT_VERIFIED', actualSha=self.commit,
                             expectedSha=self.commit, eventSha=self.commit, treeSha='d' * 40, runId='123', runAttempt='1')
        self.put(self.prepared / 'checkout.json', self.checkout)
        self.put(self.prepared / 'case-inventory.json', self.inventory)
        self.put(self.prepared / 'source-scope/source-guard-methods.json', self.scope)
        self.put(self.prepared / 'bundle-manifest.json', {name + '.dll': 'a' * 64 for name in D.MODULES})
        self.probe = self.probe_fixture()
        self.put(self.prepared / 'row-filter-probe/receipt.json', self.probe)
        (self.prepared / 'discovery.log').write_text(self.text)
        self.plan.update(commit=self.commit, sdkVersion='10.0.403', runId='123', runAttempt='1', testAssemblySha256='a' * 64,
                         bundleManifestSha256=D.digest(self.prepared / 'bundle-manifest.json'),
                         caseInventorySha256=D.digest(self.prepared / 'case-inventory.json'),
                         sourceScopeSha256=D.digest(self.prepared / 'source-scope/source-guard-methods.json'),
                         rowProbeSha256=D.digest(self.prepared / 'row-filter-probe/receipt.json'),
                         discoverySha256=D.digest(self.prepared / 'discovery.log'))
        self.materialize()
        self.jobs = dict.fromkeys(['quality', 'prepare', 'rule_suite', 'targeted'], 'success')

    def probe_fixture(self):
        calls = {'slice-1': ['Expanded:red', 'Expanded:green', 'Fact:single'],
                 'slice-2': ['Expanded:blue', 'Expanded:orange'], 'slice-3': ['Expanded:purple', 'Expanded:yellow'],
                 'escaped-values': ['edge1', 'edge2'], 'case-fold-collision': ['case', 'CASE'],
                 'case-fold-whole-fallback': ['case', 'CASE'], 'unexpanded-whole-fallback': ['opaque1', 'opaque2'],
                 'unexpanded-row-refused': []}
        return dict(status='LINUX_ROW_SELECTION_PROBE_PASSED', commit=self.commit, sdk='10.0.403',
            platform='Linux', runId='123', runAttempt='1', riftboundGameAssemblyLoaded=False,
            negativeControlsPassed=True, bodyUnionExact=True,
            controls=['missing-row', 'repeated-row', 'whole-method-row-overlap', 'ambiguous-row', 'unexpanded-row'],
            observations=[dict(label=label, actualNames=body, bodyInvocations=body,
                exitCode=int(label == 'unexpanded-row-refused'), expectedExitCode=int(label == 'unexpanded-row-refused'),
                trxSha256='c' * 64) for label, body in calls.items()])

    def materialize(self, targeted=True):
        self.put(self.prepared / 'plan.json', self.plan)
        for shard in self.plan['shards']:
            folder = self.inputs / shard['name']
            self.put(folder / 'checkout.json', self.checkout)
            names = D.SELECT.case_names(shard, self.plan['cases'])
            self.trx(folder / 'results.trx', names)
            D.SELECT.write_settings(folder / 'execution.runsettings', shard)
            (folder / 'execution-runtime.jsonl').write_text(json.dumps(dict(processId=1)) + '\n')
            self.put(folder / 'execution-runtime.json', dict(commit=self.commit, sdkVersion='10.0.403',
                testAssemblySha256='a' * 64, exitCode=0, samples=[dict(processId=1)],
                samplesSha256=D.digest(folder / 'execution-runtime.jsonl')))
            self.put(folder / 'module-audit.json', dict(cases={}, modules=self.inventory['modules']))
            self.put(folder / 'receipt.json', dict(status='PASSED', exitCode=0, commit=self.commit, shard=shard['name'],
                sdkVersion='10.0.403', seconds=1, planSha256=D.digest(self.prepared / 'plan.json'),
                bundleManifestSha256=self.plan['bundleManifestSha256'], testAssemblySha256='a' * 64,
                moduleAuditSha256=D.digest(folder / 'module-audit.json'),
                executionSettingsSha256=D.digest(folder / 'execution.runsettings'),
                runtimeReceiptSha256=D.digest(folder / 'execution-runtime.json'),
                trxSha256=D.digest(folder / 'results.trx'), counters=dict(total=len(names), passed=len(names), failed=0, notExecuted=0)))
        if targeted:
            self.put(self.target / 'view.json', D.derived_view(self.prepared, self.inputs, self.commit))
        self.put(self.target / 'checkout.json', self.checkout)
        self.put(self.quality / 'checkout.json', self.checkout)

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
        self.assertEqual(1, result['sourceGuardMethods'])
        self.assertEqual([self.fact], [row['method'] for row in result['sourceGuardCaseMapping']])
        self.assertNotIn(self.plan['sourceGuardNightlyMethods'][0], self.plan['methods'])

    def full_fixture(self):
        game = D.RULES.FULL_CLASS + 'NightlyMatrix'
        names = [game + '(seat: "P1")', game + '(seat: "P2")']
        text = self.text + ''.join('    ' + name + '\n' for name in names)
        inventory = copy.deepcopy(self.inventory)
        for method in D.RULES.l3_acceptance_methods():
            inventory['cases'][method] = [method]
        scope = copy.deepcopy(self.scope)
        scope.update(discoverySha256=hashlib.sha256(text.encode()).hexdigest(),
                     discoveredMethods=len(D.RULES.discover(text)),
                     declaredTestMethods=len(D.RULES.discover(text)))
        plan = D.make_plan(text, inventory, scope, {game: 2400}, 8, profile='full')
        plan.update({key: self.plan[key] for key in ('commit', 'sdkVersion', 'runId', 'runAttempt',
                    'testAssemblySha256', 'bundleManifestSha256', 'rowProbeSha256')})
        (self.prepared / 'discovery.log').write_text(text)
        self.put(self.prepared / 'case-inventory.json', inventory)
        self.put(self.prepared / 'source-scope/source-guard-methods.json', scope)
        plan.update(discoverySha256=D.digest(self.prepared / 'discovery.log'),
                    caseInventorySha256=D.digest(self.prepared / 'case-inventory.json'),
                    sourceScopeSha256=D.digest(self.prepared / 'source-scope/source-guard-methods.json'))
        self.plan = plan
        self.materialize(targeted=False)
        return game, names

    def full_verify(self):
        return D.aggregate_full(self.prepared, self.inputs, self.commit,
                                dict(prepare='success', rule_shards='success'))

    def test_full_profile_runs_all_discovered_rules_deferred_openings_and_every_game_row_once(self):
        game, names = self.full_fixture()
        result = self.full_verify()
        self.assertEqual('FULL_RULE_SUITE_PASSED', result['status'])
        self.assertEqual(8, result['shards'])
        self.assertEqual([], self.plan['deferredL3Methods'])
        self.assertEqual(D.RULES.discover((self.prepared / 'discovery.log').read_text()), set(self.plan['methods']))
        self.assertTrue(D.RULES.l3_acceptance_methods() <= set(self.plan['methods']))
        self.assertCountEqual(names, [row['name'] for row in result['rawCaseMapping'] if row['method'] == game])
        actual = [row['name'] for row in result['rawCaseMapping']]
        expected = [name for rows in self.plan['cases'].values() for name in rows]
        self.assertCountEqual(expected, actual)

    def test_full_gate_rejects_the_valid_l2_subset_and_missing_failed_or_cancelled_jobs(self):
        with self.assertRaisesRegex(ValueError, 'full profile'):
            self.full_verify()
        self.full_fixture()
        for state in ('failure', 'cancelled', 'skipped', ''):
            for job in ('prepare', 'rule_shards'):
                jobs = dict(prepare='success', rule_shards='success')
                jobs[job] = state
                with self.subTest(job=job, state=state), self.assertRaises(ValueError):
                    D.aggregate_full(self.prepared, self.inputs, self.commit, jobs)
        with self.assertRaises(ValueError):
            D.aggregate_full(self.prepared, self.inputs, self.commit, dict(prepare='success'))
        with self.assertRaisesRegex(ValueError, 'L2 profile'):
            D.derived_view(self.prepared, self.inputs, self.commit)
        with self.assertRaisesRegex(ValueError, 'L2 profile'):
            self.verify()

    def test_full_gate_rejects_an_omitted_deferred_opening_or_original_trx_row(self):
        self.full_fixture()
        plan = copy.deepcopy(self.plan)
        omitted = sorted(D.RULES.l3_acceptance_methods())[0]
        self.plan['methods'].remove(omitted)
        self.plan['cases'].pop(omitted)
        self.put(self.prepared / 'plan.json', self.plan)
        with self.assertRaises(ValueError):
            self.full_verify()
        self.plan = plan
        self.materialize(targeted=False)
        shard = next(row for row in self.plan['shards'] if omitted in row['methods'])
        folder = self.inputs / shard['name']
        names = D.SELECT.case_names(shard, self.plan['cases'])
        names.remove(omitted)
        self.trx(folder / 'results.trx', names)
        receipt = D.read(folder / 'receipt.json')
        receipt.update(trxSha256=D.digest(folder / 'results.trx'),
                       counters=dict(total=len(names), passed=len(names), failed=0, notExecuted=0))
        self.put(folder / 'receipt.json', receipt)
        with self.assertRaises(ValueError):
            self.full_verify()

    def test_bounded_conquest_prefix_has_all_five_rows_once_in_distributed_plan_and_filter(self):
        method = next(iter(D.RULES.L2_BOUNDED_GAMES))
        names = [method + '(source: "' + value + '")'
                 for value in ('vayne', 'zaun', 'shirana', 'moonveil', 'minefield')]
        text = self.text + ''.join('    ' + name + '\n' for name in names)
        scope = copy.deepcopy(self.scope)
        scope.update(discoverySha256=hashlib.sha256(text.encode()).hexdigest(),
                     discoveredMethods=len(D.RULES.discover(text)),
                     declaredTestMethods=len(D.RULES.discover(text)))
        plan = D.make_plan(text, self.inventory, scope, {}, 6)
        self.assertEqual(names, plan['cases'][method])
        selected = [(shard, unit) for shard in plan['shards'] for unit in shard['selectors']
                    if unit['method'] == method]
        self.assertEqual(1, len(selected))
        shard, unit = selected[0]
        self.assertEqual('method', unit['kind'])
        self.assertIn('FullyQualifiedName=' + method, D.SELECT.expression(shard))
        actual = [case for s in plan['shards'] for case in D.SELECT.case_names(s, plan['cases'])
                  if D.RULES.method(case) == method]
        self.assertCountEqual(names, actual)
        self.assertEqual(5, len(actual))
        for changed in (names[:-1], names + [names[0]]):
            broken = copy.deepcopy(plan)
            broken['cases'][method] = changed
            with self.subTest(changed=changed), self.assertRaisesRegex(ValueError, 'discovery parameter rows'):
                D.validate_plan(broken, text)

        # Exercise the aggregate with the five original row identities, then
        # prove that removing one from the shard fails even with matching counters.
        plan.update({key: self.plan[key] for key in ('commit', 'sdkVersion', 'runId', 'runAttempt',
                    'testAssemblySha256', 'bundleManifestSha256', 'caseInventorySha256', 'rowProbeSha256')})
        (self.prepared / 'discovery.log').write_text(text)
        self.put(self.prepared / 'source-scope/source-guard-methods.json', scope)
        plan['discoverySha256'] = D.digest(self.prepared / 'discovery.log')
        plan['sourceScopeSha256'] = D.digest(self.prepared / 'source-scope/source-guard-methods.json')
        self.plan = plan
        self.materialize()
        result = self.verify()
        mapped = [row['name'] for row in result['rawCaseMapping'] if row['method'] == method]
        self.assertCountEqual(names, mapped)
        self.assertEqual(5, len(mapped))
        folder = self.inputs / shard['name']
        retained = [case for case in D.SELECT.case_names(shard, plan['cases']) if case != names[0]]
        self.trx(folder / 'results.trx', retained)
        receipt = D.read(folder / 'receipt.json')
        receipt.update(trxSha256=D.digest(folder / 'results.trx'),
                       counters=dict(total=len(retained), passed=len(retained), failed=0, notExecuted=0))
        self.put(folder / 'receipt.json', receipt)
        with self.assertRaisesRegex(ValueError, 'parameter rows'):
            self.verify()

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
        rows = D.SELECT.case_names(shard, self.plan['cases'])
        target_row = self.plan['cases'][self.dynamic][0]
        missing = rows.copy()
        missing.remove(target_row)
        for changed in [missing, rows + [target_row]]:
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

    def test_source_scope_is_bound_to_current_sha_discovery_and_original_l3_lane(self):
        variations = [{**self.scope, 'sourceHead': '2' * 40}, {**self.scope, 'discoverySha256': 'b' * 64},
                      {**self.scope, 'declaredTestMethods': self.scope['declaredTestMethods'] - 1}]
        duplicate = copy.deepcopy(self.scope);duplicate['methods'][1]['method'] = self.fact;variations.append(duplicate)
        wrong_lane = copy.deepcopy(self.scope);wrong_lane['methods'][2]['existingLane'] = 'L2';variations.append(wrong_lane)
        for scope in variations:
            self.put(self.prepared / 'source-scope/source-guard-methods.json', scope)
            self.put(self.prepared / 'plan.json', {**self.plan, 'sourceScopeSha256': D.digest(self.prepared / 'source-scope/source-guard-methods.json')})
            with self.subTest(scope=scope), self.assertRaises(ValueError): D.check_prepared(self.prepared, self.commit)

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

    def test_safe_heavy_rows_split_but_unexpanded_member_data_stays_whole(self):
        plan = D.make_plan(self.text, self.inventory, self.scope, {self.theory: 2400, self.dynamic: 3000}, 6)
        for key in ['commit', 'sdkVersion', 'runId', 'runAttempt', 'testAssemblySha256', 'bundleManifestSha256',
                    'caseInventorySha256', 'sourceScopeSha256', 'rowProbeSha256', 'discoverySha256']:
            plan[key] = self.plan[key]
        self.plan = plan
        self.materialize()
        result = self.verify()
        self.assertEqual(1, result['splitMethods'])
        self.assertEqual(2, result['rowSelectors'])
        self.assertEqual(len(self.plan['methods']), result['methods'])
        self.assertEqual(2, len({r['shard'] for r in result['rawCaseMapping'] if r['method'] == self.theory}))
        self.assertEqual(1, len({r['shard'] for r in result['rawCaseMapping'] if r['method'] == self.dynamic}))

    def test_measured_case_costs_balance_unequal_rows_without_changing_membership(self):
        rows = [self.theory + '(x: 1)', self.theory + '(x: 2)']
        other = self.theory + 'Other'
        cases = {self.theory: rows, other: [other]}
        discovery = [*rows, other]
        weights = {self.theory: 3600, rows[0]: 3400, rows[1]: 200, other: 3200,
                   self.theory + '(x: 999)': 1000000}
        units = D.SELECT.selectors(discovery, cases, weights)
        shards = D.SELECT.partition(units, cases, weights, 2)
        D.SELECT.validate(shards, cases, discovery)
        self.assertEqual([3400, 3400], [shard['estimatedSeconds'] for shard in shards])
        self.assertCountEqual(discovery, [name for shard in shards for name in D.SELECT.case_names(shard, cases)])
        self.assertEqual(1, len(next(shard for shard in shards if rows[0] in D.SELECT.case_names(shard, cases))['selectors']))
        fallback = D.SELECT.partition(units, cases, {self.theory: 3600, other: 3200}, 2)
        self.assertEqual([3200, 3600], sorted(shard['estimatedSeconds'] for shard in fallback))

    def test_case_duration_cannot_make_an_ineligible_method_split(self):
        row = self.theory + '(x: 1)'
        self.assertEqual([dict(kind='method', method=self.theory)],
                         D.SELECT.selectors([row], {self.theory: [row]}, {row: 1000000}))

    def test_serial_class_work_cannot_hide_behind_equal_total_shard_weights(self):
        prefix = D.RULES.PREFIX
        weights = {prefix + 'Serial.First': 100, prefix + 'Serial.Second': 90,
                   prefix + 'Other.First': 99, prefix + 'Third.First': 98}
        cases = {method: [method] for method in weights}
        units = [dict(kind='method', method=method) for method in cases]
        shards = D.SELECT.partition(units, cases, weights, 2)
        D.SELECT.validate(shards, cases, list(cases))
        self.assertCountEqual(list(cases), [name for shard in shards for name in D.SELECT.case_names(shard, cases)])
        serial_shards = {shard['name'] for shard in shards
                         if any(unit['method'].startswith(prefix + 'Serial.') for unit in shard['selectors'])}
        self.assertEqual(2, len(serial_shards))
        bounds = []
        for shard in shards:
            classes = {}
            for method in shard['methods']:
                cls = method.rsplit('.', 1)[0]
                classes[cls] = classes.get(cls, 0) + weights[method]
            bounds.append(max(shard['estimatedSeconds'] / 4, max(classes.values())))
        self.assertEqual(100, max(bounds))  # Equal total work alone puts 190 serial seconds together.

    def test_class_spread_remains_useful_when_another_class_sets_the_shard_budget(self):
        prefix = D.RULES.PREFIX
        weights = {prefix + 'Giant.First': 1000, prefix + 'Serial.First': 100,
                   prefix + 'Serial.Second': 90, prefix + 'Other.First': 99, prefix + 'Third.First': 98}
        cases = {method: [method] for method in weights}
        shards = D.SELECT.partition([dict(kind='method', method=method) for method in cases], cases, weights, 2)
        D.SELECT.validate(shards, cases, list(cases))
        # A dominant unrelated class must not make both smaller serial methods
        # accumulate in the same shard just because their combined time fits its budget.
        serial_shards = {shard['name'] for shard in shards
                         if any(unit['method'].startswith(prefix + 'Serial.') for unit in shard['selectors'])}
        self.assertEqual(2, len(serial_shards))

    def test_partial_rows_never_overlap_a_whole_method_or_repeat_in_another_shard(self):
        plan = D.make_plan(self.text, self.inventory, self.scope, {self.theory: 2400}, 6)
        row_shard = next(s for s in plan['shards'] if any(u['kind'] == 'case' for u in s['selectors']))
        row = next(u for u in row_shard['selectors'] if u['kind'] == 'case')
        for extra in [dict(kind='method', method=self.theory), row]:
            changed = copy.deepcopy(plan)
            changed['shards'][-1]['selectors'].append(copy.deepcopy(extra))
            changed['shards'][-1]['methods'] = sorted({u['method'] for u in changed['shards'][-1]['selectors']})
            with self.subTest(extra=extra), self.assertRaisesRegex(ValueError, 'partition'):
                D.validate_plan(changed, self.text)
        changed = copy.deepcopy(plan)
        shard = next(s for s in changed['shards'] if row in s['selectors'])
        shard['selectors'].remove(row)
        shard['methods'] = sorted({u['method'] for u in shard['selectors']})
        with self.assertRaisesRegex(ValueError, 'partition'): D.validate_plan(changed, self.text)

    def test_ambiguity_unicode_and_runtime_only_rows_conservatively_fall_back(self):
        m = self.theory
        for rows, discovery in [([m + '(x: "a")', m + '(x: "A")'], None),
                                ([m + '(x: "中文")', m + '(x: "other")'], None),
                                ([m + '(x: 1)', m + '(x: 2)'], [m])]:
            units = D.SELECT.selectors(discovery or rows, {m: rows}, {m: 3000})
            self.assertEqual([dict(kind='method', method=m)], units)
            with self.assertRaisesRegex(ValueError, 'unsafe'):
                D.SELECT.validate([dict(name='shard-1', methods=[m], selectors=[dict(kind='case', method=m, case=rows[0])])],
                                  {m: rows}, discovery or rows)

    def test_linux_probe_cannot_be_an_empty_or_wrong_sdk_success_boolean(self):
        bad = [{**self.probe, 'platform': 'Darwin'}, {**self.probe, 'sdk': '10.0.301'},
               {**self.probe, 'observations': []}, {**self.probe, 'controls': []},
               {**self.probe, 'riftboundGameAssemblyLoaded': True}]
        for change in ['missing-body', 'repeated-body']:
            value = copy.deepcopy(self.probe)
            row = value['observations'][0]
            row['bodyInvocations'] = row['bodyInvocations'][1:] if change == 'missing-body' else [row['bodyInvocations'][0]] * 3
            bad.append(value)
        for probe in bad:
            self.put(self.prepared / 'row-filter-probe/receipt.json', probe)
            self.put(self.prepared / 'plan.json', {**self.plan, 'rowProbeSha256': D.digest(self.prepared / 'row-filter-probe/receipt.json')})
            with self.subTest(probe=probe), self.assertRaises(ValueError): D.check_prepared(self.prepared, self.commit)

    def test_execution_filter_and_runtime_context_cannot_be_rebound(self):
        folder = self.inputs / 'shard-1'
        settings = folder / 'execution.runsettings'
        original = settings.read_bytes()
        settings.write_bytes(original.replace(b'FullyQualifiedName=', b'DisplayName='))
        receipt = D.read(folder / 'receipt.json')
        receipt['executionSettingsSha256'] = D.digest(settings)
        self.put(folder / 'receipt.json', receipt)
        with self.assertRaisesRegex(ValueError, 'settings'): self.verify()
        settings.write_bytes(original)
        receipt['executionSettingsSha256'] = D.digest(settings)
        runtime_path = folder / 'execution-runtime.json'
        self.put(runtime_path, {**D.read(runtime_path), 'commit': '2' * 40})
        receipt['runtimeReceiptSha256'] = D.digest(runtime_path)
        self.put(folder / 'receipt.json', receipt)
        with self.assertRaisesRegex(ValueError, 'Runtime resource'): self.verify()
