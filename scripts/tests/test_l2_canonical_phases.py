import copy
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET


SCRIPT = Path(__file__).resolve().parents[1] / 'l2-canonical-phases.py'
SPEC = importlib.util.spec_from_file_location('canonical_phases', SCRIPT)
P = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(P)


class CanonicalPhaseReportTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.trx = self.root / 'results.trx'
        self.folder = self.root / 'phases'
        self.folder.mkdir()
        self.cases = [('Tests.Exhaustion(winOnDraw: True)', self.root / 'true.json'),
                      ('Tests.Exhaustion(winOnDraw: False)', self.root / 'false.json')]
        self.histories, self.traces = [], []
        # Both rows use the same room and final command count. The entire command
        # sequence distinguishes them, independently of random trace filenames.
        for middle in ['END_TURN', 'PASS_PRIORITY']:
            self.histories.append(dict(frame=dict(isConsistent=True, validationErrors=[], roomId='ROOM',
                currentTick=3, commands=[dict(accepted=True, completedTick=i, commandType=kind)
                    for i, kind in enumerate(['READY', middle, 'MOVE_UNIT'], 1)])))
            trace = []
            for i, command in enumerate(self.histories[-1]['frame']['commands'], 1):
                trace.append(dict(phase='command', room='ROOM', commandCount=i, tick=i,
                                  command=command['commandType'], totalMilliseconds=i * 100))
                if i in (1, 3):
                    trace.append(dict(phase='cold-recovery', room='ROOM', commandCount=i, tick=i,
                                      totalMilliseconds=500, **{part: 100 for part in P.PARTS}))
            self.traces.append(trace)
        self.write_inputs()

    def write_inputs(self, names=None, outcome='Passed', counters=None):
        tree = ET.Element('TestRun', xmlns=P.NS['t'])
        results = ET.SubElement(tree, 'Results')
        for name in names if names is not None else [name for name, _ in self.cases]:
            ET.SubElement(results, 'UnitTestResult', testName=name, outcome=outcome, duration='00:00:10.000')
        summary = ET.SubElement(tree, 'ResultSummary')
        ET.SubElement(summary, 'Counters', **(counters or dict(total='2', passed='2', failed='0', notExecuted='0')))
        ET.ElementTree(tree).write(self.trx)
        for i, ((_, path), history, trace) in enumerate(zip(self.cases, self.histories, self.traces)):
            path.write_text(json.dumps(history))
            (self.folder / f'{1-i}-random.jsonl').write_text(''.join(json.dumps(row) + '\n' for row in trace))

    def report(self):
        return P.report(self.trx, self.cases, self.folder, {name: 2 for name, _ in self.cases})

    def test_assigns_full_sequences_and_reports_components_and_unattributed_wall_time_read_only(self):
        before = {p: p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        result = self.report()
        self.assertEqual('CANONICAL_PHASE_TIMINGS_REPORTED', result['status'])
        self.assertEqual(2, result['executions'])
        by_name = {row['name']: row for row in result['cases']}
        for i, (name, path) in enumerate(self.cases):
            row = by_name[name]
            self.assertEqual(str((self.folder / f'{1-i}-random.jsonl').resolve()), row['phaseFile'])
            self.assertEqual(str(path.resolve()), row['history'])
            self.assertEqual((3, 2), (row['commands'], row['coldRecoveries']))
            self.assertAlmostEqual(0.6, row['commandSeconds'])
            self.assertEqual(1, row['coldSeconds'])
            self.assertAlmostEqual(8.4, row['unattributedSeconds'])
            for part in P.PARTS:
                self.assertEqual(0.2, row[part.replace('Milliseconds', 'Seconds')])
            self.assertEqual([1, 3], [c['commandCount'] for c in row['checkpoints']])
            self.assertEqual('MOVE_UNIT', row['slowestCommands'][0]['command'])
            self.assertAlmostEqual(0.3, row['commandTypes']['MOVE_UNIT']['totalSeconds'])
        self.assertEqual(before, {p: p.read_bytes() for p in self.root.rglob('*') if p.is_file()})
        self.assertEqual({str(p.resolve()): hashlib.sha256(data).hexdigest() for p, data in before.items()},
                         {row['path']: row['sha256'] for row in result['inputs']})
        self.assertIn('wall time', result['limitation'])
        self.assertIn('not assigned', result['limitation'])

    def test_missing_extra_duplicate_and_substituted_trx_rows_are_rejected(self):
        names = [name for name, _ in self.cases]
        for rows in [[], names[:1], names + names[:1], [names[0], names[0]], [names[0], 'Other.Method']]:
            with self.subTest(rows=rows):
                self.write_inputs(names=rows)
                with self.assertRaisesRegex(ValueError, 'exactly'):
                    self.report()

    def test_failed_skipped_or_incomplete_counters_are_rejected(self):
        for outcome in ['Failed', 'NotExecuted']:
            with self.subTest(outcome=outcome):
                self.write_inputs(outcome=outcome)
                with self.assertRaisesRegex(ValueError, 'passing TRX'):
                    self.report()
        self.write_inputs(counters=dict(total='2', passed='2', failed='0', notExecuted='1'))
        with self.assertRaisesRegex(ValueError, 'passing TRX'):
            self.report()

    def test_duplicate_requested_names_are_rejected(self):
        self.cases[1] = (self.cases[0][0], self.cases[1][1])
        with self.assertRaisesRegex(ValueError, 'uniquely named'):
            self.report()

    def test_ambiguous_complete_histories_are_rejected(self):
        self.histories[1] = copy.deepcopy(self.histories[0])
        self.traces[1] = copy.deepcopy(self.traces[0])
        self.write_inputs()
        with self.assertRaisesRegex(ValueError, 'unique complete'):
            self.report()

    def test_missing_extra_truncated_and_duplicate_phase_files_are_rejected(self):
        path = self.folder / '0-random.jsonl'
        original = path.read_bytes()
        path.unlink()
        with self.assertRaisesRegex(ValueError, 'every history'):
            self.report()
        path.write_bytes(original)
        extra = self.folder / 'extra.jsonl'
        extra.write_bytes(original)
        with self.assertRaisesRegex(ValueError, 'every history'):
            self.report()
        extra.unlink()
        path.write_text('\n'.join(original.decode().splitlines()[:-2]) + '\n')
        with self.assertRaisesRegex(ValueError, 'unique complete'):
            self.report()
        path.write_bytes((self.folder / '1-random.jsonl').read_bytes())
        with self.assertRaisesRegex(ValueError, 'unique complete'):
            self.report()

    def test_wrong_room_command_order_and_tick_are_rejected(self):
        original = copy.deepcopy(self.traces[0])
        for field, value in [('room', 'OTHER'), ('command', 'FOREIGN'), ('tick', 40), ('commandCount', 3)]:
            with self.subTest(field=field):
                self.traces[0] = copy.deepcopy(original)
                self.traces[0][0][field] = value
                self.write_inputs()
                with self.assertRaisesRegex(ValueError, 'unique complete'):
                    self.report()

    def test_truncated_final_cold_checkpoint_cannot_hide_in_unattributed_time(self):
        self.traces[0].pop()
        self.write_inputs()
        with self.assertRaisesRegex(ValueError, 'expected cold recovery count'):
            self.report()

    def test_cold_recovery_must_reference_current_accepted_prefix(self):
        original = copy.deepcopy(self.traces[0])
        for field, value in [('commandCount', 2), ('tick', 2)]:
            with self.subTest(field=field):
                self.traces[0] = copy.deepcopy(original)
                self.traces[0][1][field] = value
                self.write_inputs()
                with self.assertRaisesRegex(ValueError, 'current accepted'):
                    self.report()

    def test_invalid_durations_and_cold_components_are_rejected(self):
        original = copy.deepcopy(self.traces[0])
        for value in [-1, float('nan'), float('inf'), True, '10']:
            for index, field in [(0, 'totalMilliseconds'), (1, 'registryMilliseconds')]:
                with self.subTest(value=value, field=field):
                    self.traces[0] = copy.deepcopy(original)
                    self.traces[0][index][field] = value
                    self.write_inputs()
                    with self.assertRaisesRegex(ValueError, 'finite nonnegative'):
                        self.report()

    def test_mismatched_component_totals_and_excessive_measured_time_are_rejected(self):
        self.traces[0][1]['totalMilliseconds'] += 1
        self.write_inputs()
        with self.assertRaisesRegex(ValueError, 'components'):
            self.report()
        self.traces[0][1]['totalMilliseconds'] -= 1
        self.traces[0][0]['totalMilliseconds'] = 10000
        self.write_inputs()
        with self.assertRaisesRegex(ValueError, 'exceed'):
            self.report()

    def test_unknown_or_missing_cold_phase_is_rejected(self):
        original = copy.deepcopy(self.traces[0])
        self.traces[0][1]['phase'] = 'forgery'
        self.write_inputs()
        with self.assertRaisesRegex(ValueError, 'Unknown'):
            self.report()
        self.traces[0] = [row for row in original if row['phase'] == 'command']
        self.write_inputs()
        with self.assertRaisesRegex(ValueError, 'At least one'):
            self.report()

    def test_inconsistent_rejected_or_unbound_history_is_rejected(self):
        original = copy.deepcopy(self.histories[0])
        for key, value in [('isConsistent', False), ('validationErrors', ['bad history']), ('commands', [])]:
            with self.subTest(key=key):
                self.histories[0] = copy.deepcopy(original)
                self.histories[0]['frame'][key] = value
                self.write_inputs()
                with self.assertRaisesRegex(ValueError, 'consistent nonempty'):
                    self.report()
        self.histories[0] = copy.deepcopy(original)
        self.histories[0]['frame']['commands'][0]['accepted'] = False
        self.write_inputs()
        with self.assertRaisesRegex(ValueError, 'accepted commands only'):
            self.report()
        self.histories[0] = copy.deepcopy(original)
        self.histories[0]['frame']['currentTick'] = 9
        self.write_inputs()
        with self.assertRaisesRegex(ValueError, 'final tick'):
            self.report()

    def test_boolean_and_fractional_command_counts_and_ticks_are_rejected(self):
        original = copy.deepcopy(self.traces[0])
        for field in ['tick', 'commandCount']:
            for value in [True, 1.0, -1]:
                with self.subTest(field=field, value=value):
                    self.traces[0] = copy.deepcopy(original)
                    self.traces[0][0][field] = value
                    self.write_inputs()
                    with self.assertRaisesRegex(ValueError, 'integers'):
                        self.report()

    def test_input_mutation_during_report_is_rejected(self):
        read_bytes = Path.read_bytes
        calls = Counter()

        def reading(path):
            calls[path] += 1
            value = read_bytes(path)
            return value + b' ' if path == self.trx.resolve() and calls[path] > 1 else value

        with mock.patch.object(Path, 'read_bytes', reading):
            with self.assertRaisesRegex(ValueError, 'changed'):
                self.report()

    def test_cli_writes_one_new_report_and_preserves_inputs_and_existing_output(self):
        output = self.root / 'output' / 'report.json'
        command = [sys.executable, str(SCRIPT), '--trx', str(self.trx), '--phases', str(self.folder), '--output', str(output)]
        for name, path in self.cases:
            command.extend(['--case', name, str(path), '2'])
        result = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(2, json.loads(output.read_text())['executions'])
        original = output.read_bytes()
        self.assertNotEqual(0, subprocess.run(command, capture_output=True).returncode)
        self.assertEqual(original, output.read_bytes())
