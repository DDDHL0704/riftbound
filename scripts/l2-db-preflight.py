#!/usr/bin/env python3
"""Run a bounded real PostgreSQL slice and an actual API restart in Actions."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / 'tests/Riftbound.PostgresAcceptance/SqlAcceptanceCases.json'
CASES = {
    'recycled-card-boon': 'RecycledCardBoonSqlAcceptance',
    'combat-assignment': 'CombatAssignmentSqlAcceptance',
    'room-seat-journal': 'RoomSeatJournalSqlAcceptance',
    'draven-battle-death': 'DravenBattleDeathSqlAcceptance',
}
MODULES = {'Riftbound.PostgresAcceptance.dll', 'Riftbound.Engine.dll', 'Riftbound.Contracts.dll',
           'Riftbound.CardCatalog.dll', 'Riftbound.Persistence.dll'}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text())


def file_inventory(folder):
    result = {}
    for path in sorted(folder.rglob('*')):
        if path.is_symlink():
            raise ValueError('Preflight evidence must not contain symlinks.')
        if path.is_file() and path.relative_to(folder).as_posix() != 'preflight-receipt.json':
            result[path.relative_to(folder).as_posix()] = dict(bytes=path.stat().st_size, sha256=digest(path))
    return result


def selected_cases(manifest):
    entries = manifest['cases']
    if (manifest.get('schemaVersion') != 1
            or manifest.get('kind') != 'SQL_EXECUTION_IDENTITIES_NOT_RULE_CERTIFICATION'
            or len({entry['id'] for entry in entries}) != len(entries)):
        raise ValueError('Invalid original SQL case manifest.')
    selected = {}
    for identity, source_type in CASES.items():
        matches = [(i, entry) for i, entry in enumerate(entries) if entry['id'] == identity]
        if len(matches) != 1 or matches[0][1]['sourceType'] != source_type:
            raise ValueError('A required original SQL identity is missing or changed: ' + identity)
        selected[identity] = matches[0][0]
    return selected


def valid_sha(value):
    return isinstance(value, str) and re.fullmatch(r'[0-9a-f]{64}', value) is not None and value != '0' * 64


def runtime_modules(items, built):
    if not isinstance(items, list) or len(items) != len(MODULES):
        raise ValueError('The exact SQL runtime module set is required.')
    modules = {}
    for item in items:
        name, sha, mvid = item.get('file'), item.get('sha256'), item.get('mvid')
        if name not in MODULES or name in modules or not valid_sha(sha) or sha != built.get(name):
            raise ValueError('SQL runtime module differs from the recorded build.')
        try:
            parsed = uuid.UUID(mvid)
        except (ValueError, TypeError, AttributeError):
            raise ValueError('SQL runtime module MVID is missing or invalid.') from None
        if parsed.int == 0:
            raise ValueError('SQL runtime module MVID must identify a real module.')
        modules[name] = (sha, str(parsed))
    if set(modules) != MODULES:
        raise ValueError('The exact SQL runtime module set is required.')
    return modules


def verify_joined_trace(case_dir, row, fixture):
    path = case_dir / row['path']
    if (not path.resolve().is_relative_to(case_dir.resolve()) or not path.is_file()
            or digest(path) != row.get('sha256')):
        raise ValueError('Original joined trace differs from its receipt.')
    report = read(path)
    players = set(report.get('snapshots', {}))
    if (len(players) != 2 or players != {fixture['actor'], fixture['peer']}
            or players != set(row.get('players', [])) or report.get('room') != row.get('room')
            or report.get('room') != fixture['room']):
        raise ValueError('The joined trace must retain two original player identities.')
    joined = set()
    for trace in report.get('traces', []):
        if trace.get('method') != 'Joined':
            continue
        message = trace.get('message', {})
        payload = message.get('payload', {})
        player = payload.get('playerId')
        if (player not in players or player in joined or message.get('playerId') != player
                or message.get('roomId') != report['room'] or message.get('type') != 'RECONNECT'
                or not isinstance(payload.get('reconnectToken'), str)
                or not payload['reconnectToken'].strip()
                or trace.get('recipient', player) != player):
            raise ValueError('The actual joined trace has an invalid authenticated player.')
        joined.add(player)
    if joined != players or row.get('successfulJoinedPlayers') != 2:
        raise ValueError('Both original players must actually reconnect.')


def verify(folder, commit, quality, manifest_path=MANIFEST):
    receipt = read(folder / 'preflight-receipt.json')
    checkout = read(folder / 'checkout.json')
    keys = ['status', 'actualSha', 'expectedSha', 'eventSha', 'treeSha', 'runId', 'runAttempt']
    if (checkout.get('status') != 'FIXED_CANDIDATE_CHECKOUT_VERIFIED'
            or any(checkout.get(k) != quality.get(k) for k in keys)
            or any(checkout.get(k) != commit for k in ['actualSha', 'expectedSha', 'eventSha'])
            or not checkout.get('runId') or not checkout.get('runAttempt')):
        raise ValueError('Database preflight checkout or workflow attempt differs from L2.')
    manifest = read(manifest_path)
    selected = selected_cases(manifest)
    identities = [entry['id'] for entry in manifest['cases']]
    if (receipt.get('status') != 'REAL_POSTGRES_SLICE_AND_PROCESS_RESTART_PASSED'
            or receipt.get('commit') != commit or receipt.get('runId') != checkout['runId']
            or receipt.get('runAttempt') != checkout['runAttempt']
            or receipt.get('manifestSha256') != digest(manifest_path)
            or receipt.get('cases') != selected or receipt.get('partitionCount') != len(identities)
            or receipt.get('fullL3Certified') is not False
            or receipt.get('files') != file_inventory(folder)):
        raise ValueError('Database preflight evidence or fixed identity is incomplete.')
    stages = receipt.get('stages', {})
    if set(stages) != {'build', 'payment-restart', *CASES} or any(x.get('exitCode') != 0 for x in stages.values()):
        raise ValueError('Every real database preflight stage must succeed.')
    built = read(folder / 'runtime-build.json')
    if set(built) != MODULES or any(not valid_sha(sha) for sha in built.values()):
        raise ValueError('The exact built SQL module hashes are required.')
    original_modules = None
    for identity, index in selected.items():
        case_dir = folder / 'sql' / identity
        sql = read(case_dir / 'partition-receipt.json')
        if (sql.get('status') != 'REAL_POSTGRES_PARTITION_COMPLETE' or sql.get('fixedCommit') != commit
                or sql.get('partitionIndex') != index or sql.get('partitionCount') != len(identities)
                or sql.get('manifestSha256') != digest(manifest_path)
                or sql.get('orderedDefaultCases') != identities
                or sql.get('expectedCases') != [identity] or sql.get('completedCases') != [identity]
                or len(sql.get('executions', [])) != 1
                or sql['executions'][0].get('id') != identity
                or sql['executions'][0].get('status') != 'PASSED' or not sql.get('outputFiles')):
            raise ValueError('Original SQL partition receipt is incomplete: ' + identity)
        modules = runtime_modules(sql.get('runtimeAssemblies'), built)
        if original_modules is not None and modules != original_modules:
            raise ValueError('SQL identities did not load the same module hashes and MVIDs.')
        original_modules = modules
        for item in sql['outputFiles']:
            path = case_dir / item['path']
            if (not path.resolve().is_relative_to(case_dir.resolve()) or not path.is_file()
                    or path.stat().st_size != item['bytes'] or digest(path) != item['sha256']):
                raise ValueError('Original SQL output differs from its receipt: ' + identity)
    restart = folder / 'payment-restart'
    proof = read(restart / 'payment-reconnect-receipt.json')
    cases = proof.get('cases', [])
    if (proof.get('status') != 'DECLARED_PAYMENT_RECONNECT_STAGES_PASSED'
            or proof.get('fixedCommit') != commit or proof.get('orderedCases') != ['restricted-mana-lux']
            or proof.get('checkout', {}).get('actualSha') != commit
            or proof.get('checkout', {}).get('expectedSha') != commit
            or len(cases) != 1 or cases[0].get('case') != 'restricted-mana-lux'
            or cases[0].get('status') != 'PASSED' or cases[0].get('actualApiRestart') is not True
            or cases[0].get('sqlSelectorExecuted') is not True):
        raise ValueError('The actual restricted-mana SQL and restart case must pass.')
    processes = cases[0].get('processes', [])
    if (len(processes) != 2 or processes[0].get('processId') == processes[1].get('processId')
            or any(not p.get('processId') or p.get('normalExitCode') not in (0, -2, 130)
                   or p.get('sha256') != built['Riftbound.Engine.dll']
                   or p.get('mvid', '').lower() != original_modules['Riftbound.Engine.dll'][1]
                   for p in processes)):
        raise ValueError('Two real API processes must load the fixed SQL Engine module.')
    engine_hashes = [value for path, value in proof.get('fixedRuntimeSha256', {}).items()
                     if Path(path).name == 'Riftbound.Engine.dll']
    if len(engine_hashes) < 2 or any(value != built['Riftbound.Engine.dll'] for value in engine_hashes):
        raise ValueError('API and restricted-mana driver must use the fixed Engine module.')
    case_dir = restart / 'restricted-mana-lux'
    fixtures = {kind: read(case_dir / kind / 'network-fixture.json') for kind in ['ogs', 'sp6']}
    for fixture in fixtures.values():
        if (fixture.get('controlledQaPosition') is not True or fixture.get('ordinaryOpeningCertified') is not False
                or not all(isinstance(fixture.get(key), str) and fixture[key].strip()
                           for key in ['room', 'actor', 'peer']) or fixture['actor'] == fixture['peer']):
            raise ValueError('The restricted-mana fixture scope and players must remain explicit.')
    if len({fixture['room'] for fixture in fixtures.values()}) != len(fixtures):
        raise ValueError('The controlled fixtures must use distinct rooms.')
    for mode in ['play', 'recover', 'finish']:
        auth = read(case_dir / ('network-auth-' + mode + '.json'))
        if auth.get('status') != 'ACTUAL_RECONNECT_JOINED_OBSERVED':
            raise ValueError('Both original players must successfully join each game phase.')
        rows = auth.get('traces', [])
        traces = {row['path']: row for row in rows}
        if len(traces) != len(rows):
            raise ValueError('The joined trace receipt contains duplicate paths.')
        for kind in ['ogs', 'sp6']:
            path = kind + '/network-' + mode + '.json'
            if path not in traces:
                raise ValueError('Both controlled rooms must have a fresh joined trace.')
            verify_joined_trace(case_dir, traces[path], fixtures[kind])
    for kind in ['ogs', 'sp6']:
        case = case_dir / kind
        fixture = fixtures[kind]
        original_sql = read(case_dir / 'sql/sql' / kind / 'sql-original-replay.json')
        if (original_sql.get('status') != 'RESTRICTED_MANA_SQL_PASS'
                or original_sql.get('fullOriginalReplay') is not True
                or original_sql.get('birth') != ['ROOM_SEAT', 'ROOM_SEAT', 'DEV_SEED_SCENARIO:restricted-mana-lux-' + kind]
                or original_sql.get('commands', 0) <= 3 or original_sql.get('events', 0) <= 0
                or original_sql.get('actualColdRestores', 0) <= 0 or not valid_sha(original_sql.get('sevenTableDigest'))):
            raise ValueError('The original restricted-mana SQL replay is required.')
        for mode in ['play', 'recover', 'finish']:
            network = read(case / ('network-' + mode + '.json'))
            if (network.get('status') != 'REAL_SIGNALR_RESTRICTED_MANA_PASS'
                    or network.get('room') != fixture['room'] or network.get('mode') != mode):
                raise ValueError('Actual restricted-mana gameplay and reconnect are required.')
        checkpoints = [read(case / ('sql-' + stage + '.json')) for stage in ['before-restart', 'after-restart', 'final']]
        if (any(row.get('status') != 'RESTRICTED_MANA_SQL_ORIGINAL_REPLAY_PASS'
                or row.get('stage') != stage or row.get('room') != fixture['room']
                or row.get('fullOriginalReplay') is not True or not valid_sha(row.get('stateHash'))
                or not valid_sha(row.get('sevenTableDigest')) or row.get('commands', 0) <= 3
                or row.get('events', 0) <= 0
                for row, stage in zip(checkpoints, ['before-restart', 'after-restart', 'final']))
                or checkpoints[0].get('stateHash') != checkpoints[1].get('stateHash')
                or checkpoints[2].get('commands', 0) <= checkpoints[1].get('commands', 0)):
            raise ValueError('Original SQL gameplay must survive the real API restart and finish.')
        forged = read(case / 'registry-forgery-refusals.json')
        if (forged.get('room') != fixture['room'] or forged.get('originalBirthReplayRestored') is not True
                or len(forged.get('outcomes', [])) != 6
                or any(row.get('originalSqlRestored') is not True or row.get('sevenTableDigestRestored') is not True
                       for row in forged['outcomes'])):
            raise ValueError('Restricted-mana tamper checks must restore the original SQL history.')
    return dict(status=receipt['status'], commit=commit, runId=checkout['runId'],
                runAttempt=checkout['runAttempt'], cases=list(CASES),
                preflightReceiptSha256=digest(folder / 'preflight-receipt.json'),
                seconds=receipt['seconds'], targetSeconds=600,
                restartScope='CONTROLLED_RESTRICTED_MANA_NOT_ORDINARY_OPENING', fullL3Certified=False)


def run(commit, output):
    if os.environ.get('GITHUB_ACTIONS') != 'true':
        raise ValueError('The real PostgreSQL preflight runs only in Actions.')
    checkout = read(output / 'checkout.json')
    manifest = read(MANIFEST)
    selected = selected_cases(manifest)
    env = dict(os.environ, RIFTBOUND_FIXED_COMMIT=commit, SourceRevisionId=commit)
    started = time.monotonic()
    stages = {}

    def execute(name, command, extra_env=None):
        start = time.monotonic()
        with (output / (name + '.log')).open('xb') as log:
            result = subprocess.run(command, cwd=ROOT, env=extra_env or env,
                                    stdout=log, stderr=subprocess.STDOUT, check=False)
        stages[name] = dict(exitCode=result.returncode, seconds=round(time.monotonic() - start, 3))

    execute('payment-restart', ['python3', 'scripts/run-payment-reconnect-ci.py', '--expected', commit,
                                '--case', 'restricted-mana-lux', '--output', str(output / 'payment-restart')])
    execute('build', ['dotnet', 'build', 'tests/Riftbound.PostgresAcceptance', '-m:1', '--nologo', '-v:q'])
    if stages['build']['exitCode'] == 0:
        try:
            binary_dir = ROOT / 'tests/Riftbound.PostgresAcceptance/bin/Debug/net10.0'
            built = {name: digest(binary_dir / name) for name in sorted(MODULES)}
            with (output / 'runtime-build.json').open('x') as stream:
                json.dump(built, stream, indent=2)
                stream.write('\n')
        except OSError as error:
            stages['build'].update(exitCode=-1, error=str(error))
    if stages['build']['exitCode'] == 0:
        for identity, index in selected.items():
            case_env = dict(env, RIFTBOUND_POSTGRES_EVIDENCE=str(output / 'sql' / identity))
            execute(identity, ['dotnet', 'run', '--no-build', '--project', 'tests/Riftbound.PostgresAcceptance',
                               '--', '--ci-partition', str(index), str(len(manifest['cases']))], case_env)
    receipt = dict(status='REAL_POSTGRES_SLICE_AND_PROCESS_RESTART_PASSED', commit=commit,
                   runId=checkout.get('runId'), runAttempt=checkout.get('runAttempt'),
                   manifestSha256=digest(MANIFEST), cases=selected, partitionCount=len(manifest['cases']),
                   stages=stages, seconds=round(time.monotonic() - started, 3),
                   fullL3Certified=False, files=file_inventory(output))
    with (output / 'preflight-receipt.json').open('x') as stream:
        json.dump(receipt, stream, indent=2)
        stream.write('\n')
    try:
        print(json.dumps(verify(output, commit, checkout)))
    except (ValueError, OSError, KeyError, TypeError, json.JSONDecodeError) as error:
        receipt.update(status='REAL_POSTGRES_SLICE_OR_PROCESS_RESTART_FAILED', error=str(error))
        (output / 'preflight-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
        print('Database preflight failed or is incomplete: ' + str(error))
        return 1
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['run', 'verify'])
    parser.add_argument('--commit', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--quality', type=Path)
    args = parser.parse_args()
    if args.mode == 'run':
        return run(args.commit, args.output.resolve())
    if not args.quality:
        parser.error('verify requires the L2 quality checkout')
    print(json.dumps(verify(args.output.resolve(), args.commit, read(args.quality))))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
