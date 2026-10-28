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
    if set(stages) != {'build', 'restart', *CASES} or any(x.get('exitCode') != 0 for x in stages.values()):
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
    restart = folder / 'restart'
    proof = read(restart / 'process-restart-receipt.json')
    processes = proof.get('processes', [])
    restart_modules = {Path(path).name: sha for path, sha in proof.get('runtimeSha256', {}).items()}
    if (proof.get('status') != 'ACTUAL_SQL_SIGNALR_PROCESS_RESTART_PASSED'
            or proof.get('checkout', {}).get('actualSha') != commit
            or proof.get('checkout', {}).get('expectedSha') != commit
            or proof.get('originalSqlHistoryEqual') is not True
            or set(restart_modules) != {'Riftbound.Api.dll', 'Riftbound.NativeNetworkSmoke.dll', 'Riftbound.Engine.dll'}
            or len(proof.get('runtimeSha256', {})) != 3
            or any(not valid_sha(sha) for sha in restart_modules.values())
            or restart_modules['Riftbound.Engine.dll'] != built['Riftbound.Engine.dll']
            or len(processes) != 2 or processes[0].get('phase') != 'restart-prepare'
            or processes[1].get('phase') != 'restart-recover'
            or not all(p.get('processId') and p.get('normalExitCode') in (0, -2, 130) for p in processes)
            or processes[0]['processId'] == processes[1]['processId']):
        raise ValueError('A genuine two-process SQL and SignalR restart is required.')
    snapshots = []
    for process in processes:
        phase = read(restart / (process['phase'] + '.json'))
        if phase.get('status') != 'PASSED' or phase.get('actualJoinedPlayers') != 2:
            raise ValueError('Both original SignalR players must join each restart phase.')
        snapshots.append(read(restart / ('sql-process-' + str(process['processId']) + '.json')))
    if snapshots[0] != snapshots[1] or not all(snapshots[0].get(t) for t in
            ['command_log', 'game_events', 'snapshots', 'state_snapshots', 'action_prompts']):
        raise ValueError('Actual original SQL history must survive the API restart.')
    return dict(status=receipt['status'], commit=commit, cases=list(CASES),
                seconds=receipt['seconds'], targetSeconds=600, fullL3Certified=False)


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

    execute('restart', ['python3', 'scripts/run-process-restart-ci.py', '--expected', commit,
                        '--output', str(output / 'restart')])
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
