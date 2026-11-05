import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import warnings
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'scripts/sql-runtime-report.py'
SPEC = importlib.util.spec_from_file_location('sql_runtime_report', SCRIPT)
REPORT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(REPORT)
MANIFEST = ROOT / 'tests/Riftbound.PostgresAcceptance/SqlAcceptanceCases.json'


class SqlRuntimeReportTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.manifest = json.loads(MANIFEST.read_bytes())
        self.commit = '1' * 40
        self.ids = [row['id'] for row in self.manifest['cases']]
        self.counts = {index: sum(row['defaultPartition'] == index for row in self.manifest['cases'])
                       for index in range(self.manifest['defaultPartitionCount'])}
        self.counters = dict(processCpuSeconds=3.5, allocatedBytes=1024, gcPauseSeconds=0.25, collections=[2, 1, 0])

    def archive(self, index=1, change=None, corrupt_output=False, duplicate=False):
        expected = [row['id'] for row in self.manifest['cases'] if row['defaultPartition'] == index]
        payload = b'{"syntheticUnitFixtureNotDatabaseEvidence":true}\n'
        files = [('result.json' if name == 'core-opening' else name + '/receipt.json') for name in expected]
        data = dict(status='REAL_POSTGRES_PARTITION_COMPLETE', fixedCommit=self.commit, partitionIndex=index,
                    partitionCount=4, manifestSha256=REPORT.sha(MANIFEST.read_bytes()), orderedDefaultCases=self.ids,
                    expectedCases=expected, completedCases=expected,
                    executions=[dict(id=name, ordinal=self.ids.index(name), status='PASSED', seconds=ordinal + 1)
                                for ordinal, name in enumerate(expected)],
                    outputFiles=[dict(path=name, bytes=len(payload), sha256=REPORT.sha(payload)) for name in files],
                    runtimeAssemblies=[dict(file='Riftbound.' + name + '.dll', sha256='2' * 64,
                                            mvid='00000000-0000-0000-0000-000000000000')
                                       for name in ('PostgresAcceptance', 'Engine', 'Contracts', 'CardCatalog', 'Persistence')],
                    actualRuleInputs=dict(schemaVersion=1, region='CN', language='zh-CN', **dict.fromkeys(
                        ('authoritySha256', 'catalogSha256', 'implementationSha256', 'identitySha256'), '3' * 64)))
        if change:
            change(data)
        path = self.root / ('partition-' + str(index) + '.zip')
        with warnings.catch_warnings(), zipfile.ZipFile(path, 'w') as archive:
            warnings.simplefilter('ignore', UserWarning)
            archive.writestr('ci-postgres/partition-receipt.json', json.dumps(data))
            archive.writestr('ci-postgres-partition-commit.txt', self.commit + '\n')
            for name in files:
                archive.writestr('ci-postgres/' + name, b'corrupted' if corrupt_output else payload)
            if duplicate:
                archive.writestr('ci-postgres/' + files[0], payload)
        return path, REPORT.sha(path.read_bytes())

    def observe(self, *archives):
        return REPORT.report(self.commit, MANIFEST, archives or [self.archive()])

    def test_legacy_completed_partition_is_a_partial_observation_with_unknown_counters(self):
        result = self.observe()
        self.assertEqual([1], result['observedPartitions'])
        self.assertEqual([0, 2, 3], result['missingPartitions'])
        self.assertEqual(self.counts[1], result['observedCases'])
        self.assertEqual(len(self.ids), result['manifestCases'])
        self.assertEqual({'legacy': self.counts[1]}, result['counterCoverage'])
        self.assertTrue(all(row['runtimeCounters'] is None for row in result['cases']))
        self.assertEqual(sum(range(1, self.counts[1] + 1)), result['totalCaseSeconds'])
        self.assertEqual(sorted([row['seconds'] for row in result['cases']], reverse=True),
                         [row['seconds'] for row in result['cases']])
        self.assertFalse(result['fullAcceptanceVerified'])

    def test_measured_zero_unavailable_and_legacy_counters_remain_distinct(self):
        def change(data):
            data['runtimeCounterScope'] = 'Synthetic process-wide scope, not SQL attribution.'
            data['executions'][0]['runtimeCounters'] = self.counters
            data['executions'][1]['runtimeCounters'] = None
            data['executions'][2]['runtimeCounters'] = dict(processCpuSeconds=0, allocatedBytes=0,
                                                          gcPauseSeconds=0, collections=[0, 0, 0])
        result = self.observe(self.archive(change=change))
        self.assertEqual(dict(available=2, unavailable=1, legacy=self.counts[1] - 3), result['counterCoverage'])
        rows = {row['id']: row for row in result['cases']}
        expected = [row['id'] for row in self.manifest['cases'] if row['defaultPartition'] == 1]
        self.assertEqual(self.counters, rows[expected[0]]['runtimeCounters'])
        self.assertIsNone(rows[expected[1]]['runtimeCounters'])
        self.assertEqual(0, rows[expected[2]]['runtimeCounters']['allocatedBytes'])

    def test_multiple_partitions_bind_all_outputs_modules_and_input_checksums(self):
        artifacts = [self.archive(1), self.archive(3)]
        result = self.observe(*artifacts)
        self.assertEqual([1, 3], result['observedPartitions'])
        self.assertEqual([0, 2], result['missingPartitions'])
        self.assertEqual(self.counts[1] + self.counts[3], result['observedCases'])
        self.assertEqual(3, len(result['inputs']))
        for item in result['inputs']:
            raw = Path(item['path']).read_bytes()
            self.assertEqual(len(raw), item['bytes'])
            self.assertEqual(hashlib.sha256(raw).hexdigest(), item['sha256'])
        # Even every complete partition does not establish network or full acceptance.
        result = self.observe(*(self.archive(index) for index in range(4)))
        self.assertEqual([], result['missingPartitions'])
        self.assertEqual(len(self.ids), result['observedCases'])
        self.assertFalse(result['fullAcceptanceVerified'])

    def test_wrong_zip_digest_corrupt_output_and_duplicate_members_are_rejected(self):
        path, _ = self.archive()
        with self.assertRaisesRegex(ValueError, 'SHA256'):
            self.observe((path, '0' * 64))
        for options in [dict(corrupt_output=True), dict(duplicate=True)]:
            with self.subTest(options=options), self.assertRaises(ValueError):
                self.observe(self.archive(**options))

    def test_stale_manifest_wrong_commit_duplicate_partition_and_incomplete_cases_are_rejected(self):
        changes = [lambda data: data.update(fixedCommit='9' * 40),
                   lambda data: data.update(manifestSha256='9' * 64),
                   lambda data: data.update(status='INCOMPLETE'),
                   lambda data: data['completedCases'].pop(),
                   lambda data: data['executions'].reverse(),
                   lambda data: data['executions'][0].update(status='FAILED'),
                   lambda data: data['executions'][0].update(seconds=float('nan')),
                   lambda data: data['executions'][0].update(seconds=True),
                   lambda data: data['executions'][0].update(ordinal=True)]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.observe(self.archive(change=change))
        original = self.archive()
        duplicate_path = self.root / 'another.zip'
        duplicate_path.write_bytes(original[0].read_bytes())
        with self.assertRaisesRegex(ValueError, 'duplicate partition'):
            self.observe(original, (duplicate_path, original[1]))

    def test_output_coverage_paths_and_original_bytes_are_required(self):
        changes = [lambda data: data.update(outputFiles=[]),
                   lambda data: data['outputFiles'][0].update(path='../escape'),
                   lambda data: data['outputFiles'][0].update(path='C:\\escape'),
                   lambda data: data['outputFiles'][0].update(bytes=True),
                   lambda data: data['outputFiles'].append(data['outputFiles'][0])]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.observe(self.archive(change=change))

    def test_different_modules_or_rule_inputs_cannot_be_aggregated(self):
        original = self.archive(1)
        for change in [lambda data: data['runtimeAssemblies'][0].update(sha256='9' * 64),
                       lambda data: data['actualRuleInputs'].update(implementationSha256='9' * 64)]:
            with self.subTest(change=change), self.assertRaisesRegex(ValueError, 'different modules or rule inputs'):
                self.observe(original, self.archive(3, change=change))

    def test_malformed_counters_and_missing_scope_are_rejected(self):
        invalid = [True, {}, dict(self.counters, processCpuSeconds=-1),
                   dict(self.counters, gcPauseSeconds=float('inf')), dict(self.counters, allocatedBytes=1.5),
                   dict(self.counters, collections=[True, 0, 0]), dict(self.counters, collections=[])]
        for value in invalid:
            def change(data):
                data['runtimeCounterScope'] = 'Synthetic unit scope'
                data['executions'][0]['runtimeCounters'] = copy.deepcopy(value)
            with self.subTest(value=value), self.assertRaises(ValueError):
                self.observe(self.archive(change=change))
        def missing_scope(data):
            data['executions'][0]['runtimeCounters'] = None
        with self.assertRaisesRegex(ValueError, 'recorded scope'):
            self.observe(self.archive(change=missing_scope))

    def test_real_cli_writes_one_observation_and_never_overwrites_it(self):
        path, digest = self.archive()
        output = self.root / 'report.json'
        command = [sys.executable, str(SCRIPT), '--commit', self.commit, '--manifest', str(MANIFEST),
                   '--archive', str(path), 'sha256:' + digest, '--output', str(output)]
        first = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(0, first.returncode, first.stderr)
        original = output.read_bytes()
        self.assertIn('observation only', first.stdout)
        second = subprocess.run(command, capture_output=True, text=True)
        self.assertNotEqual(0, second.returncode)
        self.assertEqual(original, output.read_bytes())

    def test_artifact_changed_during_observation_is_rejected(self):
        artifact = self.archive()
        original = REPORT.process_counters

        def mutate_after_read(row):
            artifact[0].write_bytes(b'replaced after the original ZIP was read')
            return original(row)

        with patch.object(REPORT, 'process_counters', side_effect=mutate_after_read):
            with self.assertRaisesRegex(ValueError, 'input changed'):
                self.observe(artifact)


if __name__ == '__main__':
    unittest.main()
