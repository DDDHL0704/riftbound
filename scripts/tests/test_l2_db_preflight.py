import copy
import hashlib
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
        self.built = {name: hashlib.sha256(name.encode()).hexdigest() for name in P.MODULES}
        self.modules = [dict(file=name, sha256=sha, mvid=f'00000000-0000-0000-0000-{i:012x}')
                        for i, (name, sha) in enumerate(sorted(self.built.items()), 1)]
        self.write('runtime-build.json', self.built)
        for name, ordinal in self.selected.items():
            data = self.root / 'sql' / name / name / 'original-output.json'
            data.parent.mkdir(parents=True, exist_ok=True)
            data.write_text('{"original": true}\n')
            self.write(f'sql/{name}/partition-receipt.json', dict(
                status='REAL_POSTGRES_PARTITION_COMPLETE', fixedCommit=self.commit,
                partitionIndex=ordinal, partitionCount=len(self.identities), manifestSha256=P.digest(P.MANIFEST),
                orderedDefaultCases=self.identities, expectedCases=[name], completedCases=[name],
                executions=[dict(id=name, status='PASSED')], runtimeAssemblies=self.modules,
                outputFiles=[dict(path=f'{name}/original-output.json', bytes=data.stat().st_size,
                                  sha256=P.digest(data))]))
        engine_mvid = next(x['mvid'] for x in self.modules if x['file'] == 'Riftbound.Engine.dll')
        processes = [dict(processId=pid, normalExitCode=0, sha256=self.built['Riftbound.Engine.dll'], mvid=engine_mvid)
                     for pid in [10, 20]]
        self.write('payment-restart/payment-reconnect-receipt.json', dict(
            status='DECLARED_PAYMENT_RECONNECT_STAGES_PASSED', fixedCommit=self.commit,
            orderedCases=['restricted-mana-lux'], checkout=dict(actualSha=self.commit, expectedSha=self.commit),
            fixedRuntimeSha256={'/api/Riftbound.Engine.dll': self.built['Riftbound.Engine.dll'],
                                '/driver/Riftbound.Engine.dll': self.built['Riftbound.Engine.dll']},
            cases=[dict(case='restricted-mana-lux', status='PASSED', actualApiRestart=True,
                        sqlSelectorExecuted=True, processes=processes)]))
        for kind in ['ogs', 'sp6']:
            folder = 'payment-restart/restricted-mana-lux/' + kind
            room = 'fixture-' + kind
            self.write(folder + '/network-fixture.json', dict(room=room, controlledQaPosition=True,
                                                              ordinaryOpeningCertified=False))
            self.write(f'payment-restart/restricted-mana-lux/sql/sql/{kind}/sql-original-replay.json',
                       dict(status='RESTRICTED_MANA_SQL_PASS'))
            for mode in ['play', 'recover', 'finish']:
                traces = [dict(method='Joined', recipient=player, message=dict(playerId=player, roomId=room,
                          type='RECONNECT', payload=dict(playerId=player, reconnectToken='joined-' + player)))
                          for player in ['actor', 'peer']]
                self.write(folder + '/network-' + mode + '.json', dict(status='REAL_SIGNALR_RESTRICTED_MANA_PASS',
                           room=room, snapshots={'actor': {}, 'peer': {}}, traces=traces))
            for stage in ['before-restart', 'after-restart', 'final']:
                self.write(folder + '/sql-' + stage + '.json', dict(status='RESTRICTED_MANA_SQL_ORIGINAL_REPLAY_PASS',
                           stage=stage, room=room, fullOriginalReplay=True, stateHash='unchanged'))
            self.write(folder + '/registry-forgery-refusals.json', dict(room=room, originalBirthReplayRestored=True,
                       outcomes=[dict(originalSqlRestored=True, sevenTableDigestRestored=True) for _ in range(6)]))
        for mode in ['play', 'recover', 'finish']:
            rows = []
            for kind in ['ogs', 'sp6']:
                relative = kind + '/network-' + mode + '.json'
                path = self.root / 'payment-restart/restricted-mana-lux' / relative
                rows.append(dict(path=relative, room='fixture-' + kind, players=['actor', 'peer'],
                                 successfulJoinedPlayers=2, sha256=P.digest(path)))
            self.write('payment-restart/restricted-mana-lux/network-auth-' + mode + '.json',
                       dict(status='ACTUAL_RECONNECT_JOINED_OBSERVED', traces=rows))
        self.receipt = dict(status='REAL_POSTGRES_SLICE_AND_PROCESS_RESTART_PASSED', commit=self.commit,
                            runId='123', runAttempt='1', manifestSha256=P.digest(P.MANIFEST),
                            cases=self.selected, partitionCount=len(self.identities),
                            stages={name: dict(exitCode=0) for name in ['build', 'payment-restart', *P.CASES]},
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
        self.assertEqual(('123', '1'), (result['runId'], result['runAttempt']))
        self.assertEqual(P.digest(self.root / 'preflight-receipt.json'), result['preflightReceiptSha256'])
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
        restart = self.root / 'payment-restart/payment-reconnect-receipt.json'
        record = P.read(restart)
        record['cases'][0]['processes'][1]['processId'] = record['cases'][0]['processes'][0]['processId']
        restart.write_text(json.dumps(record))
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'Two real API processes'):
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
        checkpoint = self.root / 'payment-restart/restricted-mana-lux/ogs/sql-after-restart.json'
        changed = P.read(checkpoint)
        changed['stateHash'] = 'changed'
        checkpoint.write_text(json.dumps(changed))
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'SQL gameplay'):
            self.verify()

    def test_required_case_identity_cannot_silently_change(self):
        manifest = copy.deepcopy(self.manifest)
        manifest['cases'][self.selected['combat-assignment']]['sourceType'] = 'DifferentAcceptance'
        with self.assertRaisesRegex(ValueError, 'identity'):
            P.selected_cases(manifest)

    def test_wrong_missing_or_cross_case_modules_cannot_pass_by_rehashing(self):
        case = self.root / 'sql/recycled-card-boon/partition-receipt.json'
        original = P.read(case)
        for changed in ([dict(file='wrong.dll', sha256='0' * 64,
                              mvid='00000000-0000-0000-0000-000000000000')],
                        original['runtimeAssemblies'][:-1]):
            record = copy.deepcopy(original)
            record['runtimeAssemblies'] = changed
            case.write_text(json.dumps(record))
            self.save_receipt()
            with self.subTest(changed=changed), self.assertRaisesRegex(ValueError, 'module'):
                self.verify()
        record = copy.deepcopy(original)
        record['runtimeAssemblies'][0]['sha256'] = '5' * 64
        case.write_text(json.dumps(record))
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'recorded build'):
            self.verify()
        record = copy.deepcopy(original)
        record['runtimeAssemblies'][0]['mvid'] = '10000000-0000-0000-0000-000000000000'
        case.write_text(json.dumps(record))
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'same module'):
            self.verify()
        case.write_text(json.dumps(original))
        restart = self.root / 'payment-restart/payment-reconnect-receipt.json'
        proof = P.read(restart)
        proof['cases'][0]['processes'][0]['sha256'] = '5' * 64
        restart.write_text(json.dumps(proof))
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'Two real API processes'):
            self.verify()

    def test_joined_trace_and_tamper_restoration_cannot_be_faked_by_rehashing(self):
        folder = self.root / 'payment-restart/restricted-mana-lux'
        path = folder / 'ogs/network-recover.json'
        report = P.read(path)
        report['traces'].pop()
        path.write_text(json.dumps(report))
        auth_path = folder / 'network-auth-recover.json'
        auth = P.read(auth_path)
        next(row for row in auth['traces'] if row['path'] == 'ogs/network-recover.json')['sha256'] = P.digest(path)
        auth_path.write_text(json.dumps(auth))
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'Both original players'):
            self.verify()
        report['traces'].append(dict(method='Joined', recipient='peer', message=dict(playerId='peer',
            roomId='fixture-ogs', type='RECONNECT', payload=dict(playerId='peer', reconnectToken='joined-peer'))))
        path.write_text(json.dumps(report))
        next(row for row in auth['traces'] if row['path'] == 'ogs/network-recover.json')['sha256'] = P.digest(path)
        auth_path.write_text(json.dumps(auth))
        forged_path = folder / 'sp6/registry-forgery-refusals.json'
        forged = P.read(forged_path)
        forged['outcomes'][0]['sevenTableDigestRestored'] = False
        forged_path.write_text(json.dumps(forged))
        self.save_receipt()
        with self.assertRaisesRegex(ValueError, 'tamper checks'):
            self.verify()


if __name__ == '__main__':
    unittest.main()
