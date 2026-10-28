import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('preflight', ROOT / 'scripts/l2-db-preflight.py')
P = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(P)


class DatabasePreflightTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.commit = '1' * 40
        self.checkout = dict(status='FIXED_CANDIDATE_CHECKOUT_VERIFIED', actualSha=self.commit,
                             expectedSha=self.commit, eventSha=self.commit, treeSha='2' * 40,
                             runId='123', runAttempt='1')
        self.write('checkout.json', self.checkout)
        self.manifest = P.read(P.MANIFEST)
        self.selected = P.selected_cases(self.manifest)
        self.identities = [x['id'] for x in self.manifest['cases']]
        for name, ordinal in self.selected.items():
            data = self.root / 'sql' / name / name / 'original-output.json'
            data.parent.mkdir(parents=True, exist_ok=True)
            data.write_text('{"original": true}\n')
            self.write(f'sql/{name}/partition-receipt.json', dict(
                status='REAL_POSTGRES_PARTITION_COMPLETE', fixedCommit=self.commit,
                partitionIndex=ordinal, partitionCount=len(self.identities), manifestSha256=P.digest(P.MANIFEST),
                orderedDefaultCases=self.identities, expectedCases=[name], completedCases=[name],
                executions=[dict(id=name, status='PASSED')], runtimeAssemblies=[dict(file='original.dll')],
                outputFiles=[dict(path=f'{name}/original-output.json', bytes=data.stat().st_size,
                                  sha256=P.digest(data))]))
        self.write('restart/process-restart-receipt.json', dict(
            status='ACTUAL_SQL_SIGNALR_PROCESS_RESTART_PASSED',
            checkout=dict(actualSha=self.commit, expectedSha=self.commit),
            originalSqlHistoryEqual=True, runtimeSha256={'actual.dll': '3' * 64},
            processes=[dict(phase='restart-prepare', processId=10, normalExitCode=0),
                       dict(phase='restart-recover', processId=20, normalExitCode=0)]))
        for phase in ['restart-prepare', 'restart-recover']:
            self.write(f'restart/{phase}.json', dict(status='PASSED', actualJoinedPlayers=2))
        history = dict.fromkeys(['command_log', 'game_events', 'snapshots', 'state_snapshots', 'action_prompts'], [1])
        self.write('restart/sql-process-10.json', history)
        self.write('restart/sql-process-20.json', history)
        self.receipt = dict(status='REAL_POSTGRES_SLICE_AND_PROCESS_RESTART_PASSED', commit=self.commit,
                            runId='123', runAttempt='1', manifestSha256=P.digest(P.MANIFEST),
                            cases=self.selected, partitionCount=len(self.identities),
                            stages={name: dict(exitCode=0) for name in ['build', 'restart', *P.CASES]},
                            seconds=42.0, fullL3Certified=False)
        self.save_receipt()

    def write(self, relative, value):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value))

    def save_receipt(self):
        self.receipt['files'] = P.file_inventory(self.root)
        self.write('preflight-receipt.json', self.receipt)

    def verify(self, quality=None):
        return P.verify(self.root, self.commit, quality or self.checkout)

    def test_original_sql_receipts_and_two_process_history_are_required(self):
        result = self.verify()
        self.assertEqual(list(P.CASES), result['cases'])
        self.assertFalse(result['fullL3Certified'])
        changed = copy.deepcopy(self.checkout)
        changed['runAttempt'] = '2'
        with self.assertRaisesRegex(ValueError, 'workflow attempt'):
            self.verify(changed)
        self.receipt['stages']['combat-assignment']['exitCode'] = 134
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'every|Every'):
            self.verify()

    def test_rehashing_cannot_hide_missing_case_or_a_fake_restart(self):
        case = 'recycled-card-boon'
        path = self.root / 'sql' / case / 'partition-receipt.json'
        record = P.read(path)
        record['completedCases'] = []
        path.write_text(json.dumps(record))
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'Original SQL partition'):
            self.verify()
        record['completedCases'] = [case]
        path.write_text(json.dumps(record))
        restart = self.root / 'restart/process-restart-receipt.json'
        record = P.read(restart)
        record['processes'][1]['processId'] = record['processes'][0]['processId']
        restart.write_text(json.dumps(record))
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'two-process'):
            self.verify()

    def test_changed_original_sql_or_history_is_rejected(self):
        path = self.root / 'sql/room-seat-journal/room-seat-journal/original-output.json'
        path.write_text('{"changed": true}\n')
        with self.assertRaisesRegex(ValueError, 'evidence'):
            self.verify()
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'Original SQL output'):
            self.verify()
        path.write_text('{"original": true}\n')
        self.write('restart/sql-process-20.json', dict(command_log=[2]))
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'history'):
            self.verify()

    def test_required_case_identity_cannot_silently_change(self):
        manifest = copy.deepcopy(self.manifest)
        manifest['cases'][self.selected['combat-assignment']]['sourceType'] = 'DifferentAcceptance'
        with self.assertRaisesRegex(ValueError, 'identity'):
            P.selected_cases(manifest)


if __name__ == '__main__':
    unittest.main()
