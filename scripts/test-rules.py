#!/usr/bin/env python3
"""Fast rule feedback and complete, coverage-checked process sharding (stdlib only)."""
import argparse
from collections import Counter, defaultdict
from datetime import datetime
import json
import hashlib
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'tests/Riftbound.ConformanceTests'
PREFIX = 'Riftbound.ConformanceTests.'
FULL_CLASS = PREFIX + 'FullGameEndToEndTests.'
NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
DOMAINS = {
    'recast': ['Recast', 'EffectPlay', 'UnitRevival', 'RevealedHand', 'NaturalUnitConquest', 'SourceUnitPlayed', 'ConquestLifecycle', 'OfficialRepeat', 'OfficialSpellCompletion', 'OfficialWardCost'],
    'payment': ['Payment', 'Cost', 'Rune', 'Gold', 'ResourceAbility', 'Ward'],
    'triggers': ['Trigger', 'Conquest', 'Insight', 'SpellCompletion', 'DeathAndDuel', 'Hold'],
    'combat': ['Battle', 'Combat', 'Move', 'Duel', 'Target', 'Protection'],
    'recovery': ['Recovery', 'Continuation', 'Replay', 'GameHubJoin'],
}
SMOKE = ['OfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts',
         'OfficialDeckMidgamePaysRumbleReducedGraveyardMechanicalPaymentAndScoreVictoryActionLogReplaysToFinalStateHash']


def method(name):
    return name.split('(', 1)[0]


def discover(text):
    rows = [line.strip() for line in text.splitlines() if line.startswith('    ') and line.strip()]
    if not rows or any(not row.startswith(PREFIX) for row in rows):
        raise ValueError('Cannot safely parse test discovery; refusing an incomplete plan.')
    return set(map(method, rows))


def report(path):
    tree = ET.parse(path)
    rows = tree.findall('.//t:UnitTestResult', NS)
    counters = tree.find('.//t:ResultSummary/t:Counters', NS)
    if counters is None or not rows or int(counters.get('total', 0)) != len(rows):
        raise ValueError(f'Missing, empty or inconsistent TRX: {path}')
    return rows, {key: int(counters.get(key, 0)) for key in ('total', 'passed', 'failed', 'notExecuted')}


def elapsed(value):
    hours, minutes, seconds = value.split(':')
    return int(hours) * 3600 + int(minutes) * 60 + float(seconds)


def load_weights(path):
    weights = defaultdict(float)
    if path:
        if path.suffix == '.json':
            return json.loads(path.read_text())['weights']
        rows, _ = report(path)
        for row in rows:
            weights[method(row.get('testName'))] += elapsed(row.get('duration', '00:00:00'))
    return weights


def plan(methods, profile, jobs, tokens, weights):
    if profile == 'focus':
        selected = {name for name in methods if not name.startswith(FULL_CLASS)
                    and any(token.casefold() in name.casefold() for token in tokens)}
        if not selected:
            raise ValueError('No tests match; a zero-test success is forbidden.')
        expression = '(' + '|'.join('FullyQualifiedName~' + token for token in tokens) + ')'
        return [{'name': 'focus', 'methods': selected,
                 'filter': expression + '&FullyQualifiedName!~' + FULL_CLASS}]
    ordinary = {name for name in methods if not name.startswith(FULL_CLASS)}
    if profile == 'check':
        smoke = {name for name in methods if name.startswith(FULL_CLASS) and name.rsplit('.', 1)[-1] in SMOKE}
        if len(smoke) != len(SMOKE):
            raise ValueError('A required smoke test was renamed or removed; update the checked plan.')
        return [{'name': 'check', 'methods': ordinary | smoke, 'filter':
                 'FullyQualifiedName!~' + FULL_CLASS + '|' + '|'.join('FullyQualifiedName=' + n for n in sorted(smoke))}]
    games = sorted(methods - ordinary, key=lambda n: (-weights.get(n, 1), n))
    bins = [[] for _ in range(min(jobs, len(games)))]
    loads = [0.0] * len(bins)
    for name in games:
        index = min(range(len(bins)), key=loads.__getitem__)
        bins[index].append(name)
        loads[index] += weights.get(name, 1)
    shards = [{'name': f'games-{i+1}', 'methods': set(group),
               'filter': '|'.join('FullyQualifiedName=' + n for n in group), 'estimatedSeconds': loads[i]}
              for i, group in enumerate(bins)]
    shards.append({'name': 'rules', 'methods': ordinary, 'filter': 'FullyQualifiedName!~' + FULL_CLASS,
                   'estimatedSeconds': max((weights.get(n, 1) for n in ordinary), default=1)})
    coverage = Counter(n for shard in shards for n in shard['methods'])
    if set(coverage) != methods or any(count != 1 for count in coverage.values()):
        raise ValueError('Full plan must assign every discovered method exactly once.')
    return sorted(shards, key=lambda s: -s['estimatedSeconds'])


def verify(rows, counters, expected):
    actual = set(method(row.get('testName')) for row in rows)
    if actual != expected:
        raise ValueError(f'Method coverage mismatch: missing {sorted(expected-actual)[:5]}, extra {sorted(actual-expected)[:5]}')
    if counters['passed'] != counters['total'] or counters['failed'] or counters['notExecuted'] or any(row.get('outcome') != 'Passed' for row in rows):
        raise ValueError(f'Tests did not all pass: {counters}')


def terminate(process):
    if process.poll() is not None:
        return
    if os.name == 'nt':
        subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True)
    else:
        try:
            os.killpg(process.pid, signal.SIGTERM)
        except ProcessLookupError:
            return
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        if os.name != 'nt':
            os.killpg(process.pid, signal.SIGKILL)
        process.wait()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('profile', choices=['focus', 'check', 'full'], nargs='?', default='check')
    parser.add_argument('domain', nargs='?', choices=sorted(DOMAINS))
    parser.add_argument('--match', action='append', default=[], help='Additional test-name token for focus; repeatable')
    parser.add_argument('--jobs', type=int, default=min(4, os.cpu_count() or 1))
    parser.add_argument('--timings', type=Path, help='Prior TRX or timing JSON, used only to balance full shards')
    parser.add_argument('--expect-trx', type=Path, help='Full benchmark only: require exact case parity with this baseline')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--plan-only', action='store_true', help='Build and discover, then print plan without running tests')
    args = parser.parse_args()
    tokens = DOMAINS.get(args.domain, []) + args.match
    if not 1 <= args.jobs <= 16 or args.profile == 'focus' and not tokens:
        parser.error('Use 1..16 jobs; focus requires a domain or --match.')
    if args.profile != 'focus' and tokens:
        parser.error('Domains and --match apply only to focus; refusing to ignore a requested filter.')
    if any(not re.fullmatch(r'[A-Za-z0-9_.]+', token) for token in tokens):
        parser.error('Filter tokens allow letters, numbers, underscores and dots only.')
    if args.expect_trx and args.profile != 'full':
        parser.error('--expect-trx requires full.')
    output = (args.output or ROOT / 'var/qa/rules-tests' / datetime.now().strftime('%Y%m%d-%H%M%S-%f')).resolve()
    output.mkdir(parents=True, exist_ok=True)
    if (output / 'plan.json').exists():
        parser.error('Output already contains a run; use a new directory to avoid stale evidence.')
    local = Path.home() / '.dotnet' / ('dotnet.exe' if os.name == 'nt' else 'dotnet')
    dotnet = str(local) if local.exists() else shutil.which('dotnet')
    if not dotnet:
        raise ValueError('dotnet SDK not found.')
    env = dict(os.environ)
    env.setdefault('DOTNET_ROOT', str(Path(dotnet).resolve().parent))
    start = time.monotonic()
    print(f'Incremental build; evidence: {output}', flush=True)
    with (output / 'build.log').open('w') as log:
        # Keep project builds in one MSBuild node; test-process sharding remains independent.
        subprocess.run([dotnet, 'build', str(PROJECT), '-m:1', '-v:q', '--nologo'], cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
    discovery = subprocess.run([dotnet, 'test', str(PROJECT), '--no-build', '--no-restore', '--list-tests', '-v:q'],
                               cwd=ROOT, env=env, capture_output=True, text=True, check=True)
    (output / 'discovery.log').write_text(discovery.stdout + discovery.stderr)
    methods = discover(discovery.stdout)
    timings = args.timings
    if timings is None:
        # Timing history affects balancing only, never membership or pass reuse.
        candidates = sorted((PROJECT / 'TestResults').glob('*full*.trx'), key=lambda p: p.stat().st_mtime, reverse=True)
        timings = candidates[0] if candidates else ROOT / 'scripts/rule-test-timings.json'
    shards = plan(methods, args.profile, args.jobs, tokens, load_weights(timings))
    document = {'profile': args.profile, 'discoveredMethods': len(methods), 'jobs': args.jobs,
                'testAssemblySha256': hashlib.sha256((PROJECT / 'bin/Debug/net10.0/Riftbound.ConformanceTests.dll').read_bytes()).hexdigest(),
                'timings': str(timings) if timings else None,
                'shards': [{**s, 'methods': sorted(s['methods'])} for s in shards]}
    (output / 'plan.json').write_text(json.dumps(document, ensure_ascii=False, indent=2) + '\n')
    print(f'{args.profile}: {sum(len(s["methods"]) for s in shards)} methods; {len(shards)} shard(s); {args.jobs} concurrent processes', flush=True)
    if args.plan_only:
        return 0
    queued = list(shards)
    running = []
    completed = []
    all_cases = Counter()
    try:
        while queued or running:
            while queued and len(running) < args.jobs:
                shard = queued.pop(0)
                directory = output / shard['name']
                directory.mkdir()
                log = (directory / 'test.log').open('w')
                command = [dotnet, 'test', str(PROJECT), '--no-build', '--no-restore', '-v:q', '--filter', shard['filter'],
                           '--results-directory', str(directory), '--logger', 'trx;LogFileName=results.trx']
                process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=os.name != 'nt')
                running.append((process, log, shard, directory, time.monotonic()))
            for entry in running[:]:
                process, log, shard, directory, began = entry
                code = process.poll()
                if code is None:
                    continue
                log.close()
                running.remove(entry)
                if code:
                    raise ValueError(f'{shard["name"]} exited {code}; see {directory / "test.log"}')
                rows, counters = report(directory / 'results.trx')
                verify(rows, counters, shard['methods'])
                all_cases.update(row.get('testName') for row in rows)
                result = {'shard': shard['name'], **counters, 'seconds': round(time.monotonic()-began, 2)}
                completed.append(result)
                print(f'{shard["name"]}: {counters["passed"]} passed in {result["seconds"]} s', flush=True)
            if running:
                time.sleep(0.2)
        if args.expect_trx:
            baseline, _ = report(args.expect_trx)
            if all_cases != Counter(row.get('testName') for row in baseline):
                raise ValueError('Exact baseline case parity failed; full benchmark is not valid.')
        summary = {'status': 'PASSED', 'profile': args.profile, 'seconds': round(time.monotonic()-start, 2),
                   'total': sum(all_cases.values()), 'caseParityVerified': bool(args.expect_trx), 'shards': completed}
        (output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
        print(json.dumps(summary), flush=True)
        return 0
    finally:
        for process, log, *_ in running:
            terminate(process)
            log.close()


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, OSError, ET.ParseError, subprocess.CalledProcessError) as error:
        print(f'FAILED: {error}', file=sys.stderr)
        sys.exit(1)
    except KeyboardInterrupt:
        print('Interrupted; owned test processes stopped.', file=sys.stderr)
        sys.exit(130)
