import copy
import hashlib
import importlib.util
import itertools
import json
from pathlib import Path
import re
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('sql_ci_gate', ROOT / 'scripts/sql-ci-gate.py')
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)
MANIFEST = ROOT / 'tests/Riftbound.PostgresAcceptance/SqlAcceptanceCases.json'


class SqlCiGateTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.root = Path(self.folder.name)
        self.commit = '1' * 40
        self.jobs = dict.fromkeys(['scope', 'postgres_partitions', 'native_network'], 'success')
        self.manifest = json.loads(MANIFEST.read_text())
        self.ids = [row['id'] for row in self.manifest['cases']]
        self.paths = []
        for index in range(4):
            parent = self.root / str(index)
            parent.mkdir()
            expected = self.ids[index::4]
            files = []
            for case in expected:
                name = 'result.json' if case == 'core-opening' else case + '/receipt.json'
                path = parent / name
                path.parent.mkdir(exist_ok=True)
                path.write_text('{"syntheticUnitFixtureNotActualDatabaseEvidence":true}\n')
                files.append({'path': name, 'bytes': path.stat().st_size, 'sha256': GATE.digest(path)})
            data = {'status': 'REAL_POSTGRES_PARTITION_COMPLETE', 'fixedCommit': self.commit,
                    'partitionIndex': index, 'partitionCount': 4, 'manifestSha256': GATE.digest(MANIFEST),
                    'orderedDefaultCases': self.ids, 'expectedCases': expected, 'completedCases': expected,
                    'executions': [{'id': case, 'status': 'PASSED', 'seconds': 1} for case in expected],
                    'outputFiles': files,
                    'runtimeAssemblies': [{'file': name, 'sha256': '2' * 64,
                        'mvid': '00000000-0000-0000-0000-000000000000'} for name in
                        ['Riftbound.PostgresAcceptance.dll', 'Riftbound.Engine.dll', 'Riftbound.Contracts.dll',
                         'Riftbound.CardCatalog.dll', 'Riftbound.Persistence.dll']],
                    'actualRuleInputs': {'schemaVersion': 1, 'region': 'CN', 'language': 'zh-CN',
                        **dict.fromkeys(['authoritySha256', 'catalogSha256', 'implementationSha256', 'identitySha256'], '3' * 64)}}
            receipt = parent / 'partition-receipt.json'
            receipt.write_text(json.dumps(data))
            self.paths.append(receipt)

    def verify(self):
        return GATE.verify(self.commit, MANIFEST, self.root, self.jobs)

    def mutate(self, change):
        path = self.paths[0]
        value = json.loads(path.read_text())
        change(value)
        path.write_text(json.dumps(value))

    def test_exact_all_default_cases_including_original_core_are_required_once(self):
        result = self.verify()
        self.assertEqual(65, result['caseCount'])
        self.assertEqual(self.ids, result['orderedDefaultCases'])
        self.assertEqual(1, self.ids.count('core-opening'))
        self.assertEqual('conditional-counter-payment', self.ids[45])
        self.assertEqual('reversal-target-choice', self.ids[46])
        self.assertEqual('spell-remake-declaration', self.ids[47])
        self.assertEqual('candlelit-private-look', self.ids[48])
        self.assertEqual('candlelit-legend-pending', self.ids[49])
        self.assertEqual('revealed-hand-prompt-order', self.ids[50])
        self.assertEqual('battlefield-spell-damage', self.ids[51])
        self.assertEqual('exhausted-legend-confirmation', self.ids[52])
        self.assertEqual('teemo-counted-damage', self.ids[53])
        self.assertEqual('controller-damage-bonus', self.ids[54])
        self.assertEqual('special-trigger-damage-bonus', self.ids[55])
        self.assertEqual('room-seat-journal', self.ids[56])
        self.assertEqual('battlefield-start-controller', self.ids[57])
        self.assertEqual('battlefield-start-lab-you', self.ids[58])
        self.assertEqual('deck-pending-parent', self.ids[59])
        self.assertEqual('wild-claw-reflexive', self.ids[60])
        self.assertEqual('lightning-rush-flow', self.ids[61])
        self.assertEqual('final-conquest-score', self.ids[62])
        self.assertEqual('burnout-action-completion', self.ids[63])
        self.assertEqual('victory-cleanup-garen', self.ids[64])

    def test_every_failed_cancelled_skipped_or_missing_child_fails_closed(self):
        for values in itertools.product(['success', 'failure', 'cancelled', 'skipped', ''], repeat=3):
            with self.subTest(values=values):
                self.jobs = dict(zip(['scope', 'postgres_partitions', 'native_network'], values))
                if values == ('success',) * 3:
                    self.verify()
                else:
                    with self.assertRaises(ValueError):
                        self.verify()

    def test_missing_and_extra_partition_receipt_are_rejected(self):
        saved = self.paths.pop()
        raw = saved.read_bytes()
        saved.unlink()
        with self.assertRaises(ValueError):
            self.verify()
        saved.write_bytes(raw)
        extra = self.root / 'extra'
        extra.mkdir()
        (extra / 'partition-receipt.json').write_bytes(raw)
        with self.assertRaises(ValueError):
            self.verify()

    def test_stale_commit_plan_wrong_count_duplicate_index_or_missing_execution_are_rejected(self):
        changes = [lambda x: x.update(fixedCommit='4' * 40), lambda x: x.update(manifestSha256='5' * 64),
                   lambda x: x.update(partitionCount=3), lambda x: x.update(partitionIndex=1),
                   lambda x: x['completedCases'].pop(), lambda x: x['executions'].pop(),
                   lambda x: x['executions'][0].update(status='SKIPPED'), lambda x: x['executions'][0].update(seconds=float('nan')),
                   lambda x: x['completedCases'].reverse()]
        raw = self.paths[0].read_bytes()
        for change in changes:
            with self.subTest(change=change):
                self.paths[0].write_bytes(raw)
                self.mutate(change)
                with self.assertRaises(ValueError):
                    self.verify()

    def test_actual_case_file_tamper_missing_data_and_escape_paths_are_rejected(self):
        raw = self.paths[0].read_bytes()
        for change in [lambda x: x['outputFiles'][0].update(sha256='7' * 64),
                       lambda x: x['outputFiles'][0].update(bytes=0), lambda x: x.update(outputFiles=[]),
                       lambda x: x['outputFiles'][0].update(path='../outside.json')]:
            self.paths[0].write_bytes(raw)
            self.mutate(change)
            with self.assertRaises(ValueError):
                self.verify()
        self.paths[0].write_bytes(raw)
        first = json.loads(raw)['outputFiles'][0]['path']
        (self.paths[0].parent / first).write_text('changed original bytes')
        with self.assertRaises(ValueError):
            self.verify()

    def test_missing_runtime_rule_identity_or_different_authority_is_rejected(self):
        raw = self.paths[0].read_bytes()
        for change in [lambda x: x.update(runtimeAssemblies=[]), lambda x: x.update(actualRuleInputs={}),
                       lambda x: x['actualRuleInputs'].update(authoritySha256='8' * 64)]:
            self.paths[0].write_bytes(raw)
            self.mutate(change)
            with self.assertRaises(ValueError):
                self.verify()

    def test_manifest_matches_every_actual_default_call_and_keeps_order(self):
        source = (ROOT / 'tests/Riftbound.PostgresAcceptance/Program.cs').read_text()
        calls = re.findall(r'await partition.RunAsync\("([^\"]+)"', source)
        self.assertEqual(self.ids, calls)
        for row in self.manifest['cases']:
            if row['id'] != 'core-opening':
                self.assertIn('() => ' + row['sourceType'] + '.RunAsync(dataSource, Path.Combine(output, "' + row['id'] + '"))', source)
        self.assertIn('await partition.CompleteAsync(output);', source)

    def test_workflow_partitions_and_network_start_in_first_wave_with_stable_gates(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        jobs = dict(re.findall(r'^  ([a-z_]+):\n(.*?)(?=^  [a-z_]+:\n|\Z)', text, re.M | re.S))
        for name in ['rule_suite', 'postgres_partitions', 'native_network']:
            self.assertIn('    needs: scope\n', jobs[name])
            self.assertNotIn('continue-on-error', jobs[name])
        self.assertIn('partition: [0, 1, 2, 3]', jobs['postgres_partitions'])
        self.assertIn('fail-fast: false', jobs['postgres_partitions'])
        self.assertIn('github.sha }}-${{ matrix.partition', jobs['postgres_partitions'])
        self.assertIn('--ci-partition ${{ matrix.partition }} 4', jobs['postgres_partitions'])
        self.assertIn('    needs: [scope, postgres_partitions, native_network]\n', jobs['postgres'])
        self.assertIn('python3 scripts/sql-ci-gate.py', jobs['postgres'])
        self.assertIn('    needs: [scope, rule_suite, postgres]\n', jobs['rules'])


if __name__ == '__main__':
    unittest.main()
