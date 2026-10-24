#!/usr/bin/env python3
"""Restart an actual PostgreSQL-backed API and restore both original SignalR players."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('candidate', ROOT / 'scripts/ci-candidate.py')
CANDIDATE = importlib.util.module_from_spec(SPEC)
previous_bytecode_setting = sys.dont_write_bytecode
try:
    sys.dont_write_bytecode = True
    SPEC.loader.exec_module(CANDIDATE)
finally:
    sys.dont_write_bytecode = previous_bytecode_setting


def stop_api(process):
    if process.poll() is not None:
        raise RuntimeError('The dedicated API exited before shutdown.')
    process.send_signal(signal.SIGINT)
    try:
        code = process.wait(timeout=20)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=10)
        raise RuntimeError('API required forced cleanup; restart acceptance is incomplete.')
    if code not in (0, -signal.SIGINT, 128 + signal.SIGINT):
        raise RuntimeError('API shutdown failed: ' + str(code))
    return code


def restart_sequence(launch, driver, capture, shutdown):
    phases, snapshots = [], []
    for phase in ('restart-prepare', 'restart-recover'):
        process = launch(phase)
        failure = None
        try:
            if phases and phases[0]['processId'] == process.pid:
                raise ValueError('Recovery must use a new actual API process.')
            driver(phase)
            snapshots.append(capture(process.pid))
        except BaseException as error:
            failure = error
            raise
        finally:
            try:
                code = shutdown(process)
            except BaseException as cleanup:
                if failure is None:
                    raise
                failure.riftbound_cleanup_errors = [repr(cleanup)]
        phases.append(dict(phase=phase, processId=process.pid, normalExitCode=code))
    if snapshots[0] != snapshots[1]:
        raise ValueError('Process restart or reconnect changed the original SQL command/event/state history.')
    return phases


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--expected', required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    if os.environ.get('GITHUB_ACTIONS') != 'true':
        p.error('Real process restart acceptance runs in Actions.')
    proof = dict(status='RUNNING', checkout=CANDIDATE.verify(ROOT, a.expected), fullGameReplayCertified=False)
    output = a.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    env = dict(os.environ, SourceRevisionId=a.expected, ASPNETCORE_ENVIRONMENT='Production',
               ASPNETCORE_URLS='http://127.0.0.1:15090', RIFTBOUND_SERVER_URL='http://127.0.0.1:15090')
    env.pop('DOTNET_STARTUP_HOOKS', None)
    if not env.get('ConnectionStrings__Riftbound'):
        p.error('A dedicated PostgreSQL service is required.')
    dotnet, psql = shutil.which('dotnet'), shutil.which('psql')
    if not dotnet or not psql:
        raise RuntimeError('The Actions .NET runtime and PostgreSQL client are required.')
    sql = [psql, '--no-psqlrc', '--host=127.0.0.1', '--port=5432', '--username=postgres',
           '--dbname=riftbound_acceptance', '--set=ON_ERROR_STOP=1']

    def checked(command, name):
        with (output / name).open('xb') as stream:
            subprocess.run(command, cwd=ROOT, env=env, stdout=stream, stderr=subprocess.STDOUT, check=True)

    try:
        for migration in sorted((ROOT / 'src/Riftbound.Persistence/Sql').glob('*.sql')):
            checked(sql + ['--file=' + str(migration)], 'schema-' + migration.stem + '.log')
        for project, name in [('src/Riftbound.Api', 'api'), ('tests/Riftbound.NativeNetworkSmoke', 'driver')]:
            checked([dotnet, 'build', project, '-m:1', '--nologo', '-v:q'], 'build-' + name + '.log')
        api = ROOT / 'src/Riftbound.Api/bin/Debug/net10.0/Riftbound.Api.dll'
        driver = ROOT / 'tests/Riftbound.NativeNetworkSmoke/bin/Debug/net8.0/Riftbound.NativeNetworkSmoke.dll'
        checked([dotnet, str(driver), 'restart-json-self-test'], 'snapshot-json-contract.log')
        runtime = {str(path): hashlib.sha256(path.read_bytes()).hexdigest()
                   for path in [api, driver, api.parent / 'Riftbound.Engine.dll']}
        proof['runtimeSha256'] = runtime

        def launch(phase):
            with (output / ('api-' + phase + '.log')).open('xb') as log:
                process = subprocess.Popen([dotnet, str(api)], cwd=api.parent, env=env,
                                           stdout=log, stderr=subprocess.STDOUT)
            try:
                deadline = time.monotonic() + 60
                while time.monotonic() < deadline:
                    if process.poll() is not None:
                        raise RuntimeError('Dedicated API failed during startup.')
                    try:
                        with urllib.request.urlopen(env['ASPNETCORE_URLS'] + '/health', timeout=1) as response:
                            if response.status == 200:
                                return process
                    except (urllib.error.URLError, TimeoutError):
                        pass
                    time.sleep(0.2)
                raise RuntimeError('Dedicated API did not become healthy.')
            except BaseException:
                if process.poll() is None:
                    stop_api(process)
                raise

        def run_driver(phase):
            checked([dotnet, str(driver), phase, str(output)], 'driver-' + phase + '.log')
            receipt = json.loads((output / (phase + '.json')).read_text())
            if receipt['status'] != 'PASSED' or receipt['actualJoinedPlayers'] != 2:
                raise ValueError('Both actual SignalR players must complete this phase.')

        def capture(pid):
            room = json.loads((output / 'restart-input.json').read_text())['room']
            if not re.fullmatch(r'restart-[a-f0-9]{32}', room):
                raise ValueError('Unexpected dedicated acceptance room.')
            maps = Path('/proc/' + str(pid) + '/maps').read_text()
            if str(api.parent / 'Riftbound.Engine.dll') not in maps:
                raise ValueError('Actual API process did not load the fixed Engine path.')
            (output / ('process-' + str(pid) + '-maps.txt')).write_text(maps)
            result = {}
            for table in ('command_log', 'game_events', 'snapshots', 'state_snapshots', 'action_prompts'):
                query = "select coalesce(jsonb_agg(to_jsonb(t) order by id), '[]'::jsonb) from " + table + " t where match_id='" + room + "'"
                rows = json.loads(subprocess.check_output(sql + ['--tuples-only', '--no-align', '--command', query], env=env))
                if not rows:
                    raise ValueError('Actual original SQL history is empty: ' + table)
                result[table] = rows
            (output / ('sql-process-' + str(pid) + '.json')).write_text(json.dumps(result, indent=2) + '\n')
            return result

        proof['processes'] = restart_sequence(launch, run_driver, capture, stop_api)
        if any(hashlib.sha256(Path(path).read_bytes()).hexdigest() != sha for path, sha in runtime.items()):
            raise ValueError('Runtime changed during process restart acceptance.')
        CANDIDATE.verify(ROOT, a.expected)
        proof.update(status='ACTUAL_SQL_SIGNALR_PROCESS_RESTART_PASSED', originalSqlHistoryEqual=True)
    except BaseException as error:
        proof.update(status='PROCESS_RESTART_FAILURE_OR_INCOMPLETE', error=repr(error),
                     cleanupErrors=getattr(error, 'riftbound_cleanup_errors', []))
        raise
    finally:
        (output / 'process-restart-receipt.json').write_text(json.dumps(proof, indent=2) + '\n')


if __name__ == '__main__':
    main()
