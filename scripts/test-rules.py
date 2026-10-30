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
         'OfficialDeckMidgamePaysRumbleReducedGraveyardMechanicalPaymentAndScoreVictoryActionLogReplaysToFinalStateHash',
         'OfficialDeckMidgameResolvesPiltoverAcademyHeldNextSpellEchoAndScoreVictoryActionLogReplaysToFinalStateHash']
# This controlled prefix shares the full-game class, but is not a full-game
# acceptance. Include it when present without requiring it on older inputs.
L2_BOUNDED_GAMES = {FULL_CLASS + 'ControlledConquestFixturesChangeEnemyControlAndReplayConsumerPrefix'}
L3_POLICY = ROOT / 'scripts/l3-rule-acceptance-methods.json'
TESTS_ROOT = ROOT / 'tests/Riftbound.ConformanceTests'
CARD_TOKEN = re.compile(
    r'(?<![A-Za-z0-9])(?:RAD|VEN)(?:[·-](?:[A-Za-z]*\d+[A-Za-z*]*(?:/\d+)?(?:·[A-Za-z0-9]+)?|[A-Za-z]+\d*(?:/\d+)?))(?![A-Za-z0-9])')
TYPE_DECLARATION = re.compile(r'\b(?:class|record|struct|interface)\s+([A-Za-z_]\w*)')
SOURCE_READ = re.compile(r'(?:SourceFileParts\s*\.\s*ReadAll(?:Text|Lines|Bytes)|File\s*\.\s*ReadAll(?:Text|Lines|Bytes))')
SOURCE_ASSERTION = re.compile(r'Assert\s*\.\s*(?:Contains|DoesNotContain|Matches|Equal|NotEqual)')
GENERIC_SOURCE_PARTS = frozenset({'cs', 'tests', 'test', 'src', 'engine', 'contracts', 'cardcatalog', 'persistence', 'api'})
EXPLICIT_GUARD_PARTS = ('privacy', 'hidden', 'sourceidentity', 'provenance', 'catalogbaseline', 'officialopening')


class AffectedBranchOutOfDate(ValueError):
    """The requested base is newer than the checked out branch."""


def _test_class_names(text):
    names = _declared_type_names(text)
    return {name for name in names
            if name.endswith(('Tests', 'Test', 'Consumers', 'Guard', 'Privacy', 'Provenance'))}


def _declared_type_names(text):
    return set(TYPE_DECLARATION.findall(text))


def _guard_class_names(test_files):
    """Find mandatory guards and tests that assert source-level architecture."""
    names = set()
    for path, text in test_files:
        lower = Path(path).name.casefold()
        explicit = any(part in lower for part in EXPLICIT_GUARD_PARTS)
        source_assertion = bool(SOURCE_READ.search(text) and SOURCE_ASSERTION.search(text)
                               and ('SourceFileParts' in text or '.cs' in text))
        if explicit or source_assertion:
            names.update(_test_class_names(text))
    return {name for name in names if name != 'SourceFileParts'}


def _source_identifiers(path, text):
    """Build conservative type/file tokens for tests that depend on a source file."""
    stem = path.rsplit('/', 1)[-1]
    if stem.endswith('.cs'):
        stem = stem[:-3]
    parts = re.split(r'[._-]', stem)
    identifiers = {part for part in parts if len(part) >= 4 and part.casefold() not in GENERIC_SOURCE_PARTS}
    identifiers.update(_declared_type_names(text))
    if stem:
        identifiers.add(stem)
    return identifiers


def select_affected_test_classes(changed_paths, changed_text, test_files, source_texts=None):
    """Select test classes for a conservative pre-push affected-test run."""
    source_texts = source_texts or {}
    changed = set(changed_paths)
    selected = set()
    tokens = set(CARD_TOKEN.findall(changed_text))
    source_tokens = set()
    for path in changed:
        if path.startswith('tests/Riftbound.ConformanceTests/') and path.endswith('.cs'):
            source = next((text for candidate, text in test_files if candidate == path), '')
            test_classes = _test_class_names(source)
            selected.update(test_classes)
            # Nested Driver/Store/Printing helpers are not test classes. A
            # standalone helper file instead affects the tests that use it.
            if not test_classes:
                source_tokens.update(_source_identifiers(path, source))
        elif path.startswith('src/') or path.endswith('.cs'):
            source_tokens.update(_source_identifiers(path, source_texts.get(path, '')))
    for path, text in test_files:
        if path in changed:
            selected.update(_test_class_names(text))
            continue
        if tokens and any(token in text for token in tokens):
            selected.update(_test_class_names(text))
        if source_tokens and any(token in text for token in source_tokens):
            selected.update(_test_class_names(text))
        if any(path_token in text for path_token in changed if path_token != 'docs/BACKLOG.md'):
            selected.update(_test_class_names(text))
    selected.update(_guard_class_names(test_files))
    if not selected:
        raise ValueError('No affected tests or mandatory guard classes were found.')
    return selected, tokens


def _git_text(repo, *args):
    try:
        return subprocess.check_output(['git', *args], cwd=repo, stderr=subprocess.PIPE, text=True)
    except subprocess.CalledProcessError as error:
        detail = error.stderr.strip() if isinstance(error.stderr, str) else ''
        raise ValueError(f'git {" ".join(args)} failed' + (f': {detail}' if detail else '')) from error


def affected_selection(base='origin/main', repo=ROOT, head='HEAD'):
    """Return affected classes, filter and fixed Git inputs for ``head``."""
    head_sha = _git_text(repo, 'rev-parse', '--verify', head + '^{commit}').strip()
    base_sha = _git_text(repo, 'rev-parse', '--verify', base + '^{commit}').strip()
    try:
        subprocess.run(['git', 'merge-base', '--is-ancestor', base_sha, head_sha], cwd=repo,
                       stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True, text=True)
    except subprocess.CalledProcessError as error:
        raise AffectedBranchOutOfDate(
            f'HEAD {head_sha[:12]} is behind or diverged from {base} {base_sha[:12]}; '
            'sync origin/main before running affected tests.') from error
    merge_base = _git_text(repo, 'merge-base', base_sha, head_sha).strip()
    changed_paths = [path for path in _git_text(
        repo, 'diff', '--name-only', '--diff-filter=ACMR', merge_base + '...' + head_sha).splitlines() if path]
    changed_text = _git_text(repo, 'diff', '--no-ext-diff', '--unified=0', merge_base + '...' + head_sha)
    source_texts = {}
    for path in changed_paths:
        if path.startswith('src/') or path.endswith('.cs'):
            try:
                source_texts[path] = _git_text(repo, 'show', f'{head_sha}:{path}')
            except ValueError:
                source_texts[path] = ''
    tests_root = Path(repo) / 'tests/Riftbound.ConformanceTests'
    test_files = [(path.relative_to(repo).as_posix(), path.read_text(encoding='utf-8', errors='replace'))
                  for path in sorted(tests_root.rglob('*.cs'))]
    classes, tokens = select_affected_test_classes(changed_paths, changed_text, test_files, source_texts)
    filter_expression = '(' + '|'.join('FullyQualifiedName~' + name for name in sorted(classes)) + ')'
    return dict(base=base, baseSha=base_sha, headSha=head_sha, mergeBase=merge_base,
                changedPaths=changed_paths, cardTokens=sorted(tokens), classes=sorted(classes),
                filter=filter_expression)


def l3_acceptance_methods(path=L3_POLICY):
    policy = json.loads(Path(path).read_text())
    rows = policy.get('methods', [])
    names = [row['method'] for row in rows]
    if (policy.get('schemaVersion') != 1 or not rows or len(set(names)) != len(names)
            or any(not re.fullmatch(r'Riftbound\.ConformanceTests\.[A-Za-z0-9_]+\.[A-Za-z0-9_]+', name)
                   for name in names)
            or any(not row.get('source') or not row.get('reason') for row in rows)):
        raise ValueError('Invalid explicit L3 acceptance-method policy.')
    return set(names)


def l2_rule_methods(methods):
    deferred = l3_acceptance_methods()
    if not deferred <= methods:
        raise ValueError('A declared L3 method was renamed or removed; reconcile the retained test identity.')
    smoke = {name for name in methods if name.startswith(FULL_CLASS) and name.rsplit('.', 1)[-1] in SMOKE}
    if len(smoke) != len(SMOKE):
        raise ValueError('A required smoke test was renamed or removed; update the checked plan.')
    bounded = L2_BOUNDED_GAMES & methods
    if bounded & deferred:
        raise ValueError('A required bounded L2 method is also declared as deferred L3.')
    return {name for name in methods if not name.startswith(FULL_CLASS)} - deferred | smoke | bounded


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
    keys = ('total', 'passed', 'failed', 'notExecuted')
    if (counters is None or any(counters.get(key) is None for key in keys)
            or not rows or int(counters.get('total')) != len(rows)):
        raise ValueError(f'Missing, empty or inconsistent TRX: {path}')
    return rows, {key: int(counters.get(key)) for key in keys}


def elapsed(value):
    hours, minutes, seconds = value.split(':')
    return int(hours) * 3600 + int(minutes) * 60 + float(seconds)


def load_weights(path):
    weights = defaultdict(float)
    if path:
        if path.suffix == '.json':
            data = json.loads(path.read_text())
            return data['weights'] | data.get('caseWeights', {})
        rows, _ = report(path)
        for row in rows:
            weights[method(row.get('testName'))] += elapsed(row.get('duration', '00:00:00'))
    return weights


def plan(methods, profile, jobs, tokens, weights, include_games=False, l2_only=False):
    deferred = l3_acceptance_methods() if profile == 'l2' or l2_only else set()
    if deferred and not deferred <= methods:
        raise ValueError('A declared L3 method was renamed or removed.')
    exclude = ''.join('&FullyQualifiedName!=' + name for name in sorted(deferred))
    if profile == 'focus':
        selected = {name for name in methods if (include_games or not name.startswith(FULL_CLASS))
                    and name not in deferred and any(token.casefold() in name.casefold() for token in tokens)}
        if not selected:
            raise ValueError('No tests match; a zero-test success is forbidden.')
        expression = '(' + '|'.join('FullyQualifiedName~' + token for token in tokens) + ')'
        return [{'name': 'focus', 'methods': selected,
                 'filter': (expression if include_games else expression + '&FullyQualifiedName!~' + FULL_CLASS) + exclude}]
    ordinary = {name for name in methods if not name.startswith(FULL_CLASS)}
    if profile in {'check', 'l2'}:
        smoke = {name for name in methods if name.startswith(FULL_CLASS) and name.rsplit('.', 1)[-1] in SMOKE}
        if len(smoke) != len(SMOKE):
            raise ValueError('A required smoke test was renamed or removed; update the checked plan.')
        selected = l2_rule_methods(methods) if profile == 'l2' else ordinary | smoke
        included_games = {name for name in selected if name.startswith(FULL_CLASS)}
        expression = '(FullyQualifiedName!~' + FULL_CLASS + '|' + '|'.join('FullyQualifiedName=' + n for n in sorted(included_games)) + ')'
        return [{'name': profile, 'methods': selected, 'filter': expression + exclude}]
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
    # Ordinary rules provide broad failure feedback. Keep the longest game in
    # the first wave as well, without changing membership or the job limit.
    return sorted(shards, key=lambda s: (s['name'] != 'rules', -s['estimatedSeconds']))


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


def run_affected(args):
    selection = affected_selection(args.base)
    if args.print_filter:
        print(selection['filter'])
        return 0
    print(f"Affected base: {selection['baseSha']}  head: {selection['headSha']}", flush=True)
    print(f"Changed paths: {len(selection['changedPaths'])}; selected classes: {len(selection['classes'])}", flush=True)
    if selection['cardTokens']:
        print('Changed card tokens: ' + ', '.join(selection['cardTokens']), flush=True)
    print('Affected filter: ' + selection['filter'], flush=True)
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise ValueError('dotnet SDK not found on PATH.')
    output = (args.output or ROOT / 'var/qa/affected-tests' / datetime.now().strftime('%Y%m%d-%H%M%S-%f')).resolve()
    output.mkdir(parents=True, exist_ok=True)
    if (output / 'results.trx').exists():
        raise ValueError('Output already contains a run; use a new directory to avoid stale results.')
    env = dict(os.environ)
    build = [dotnet, 'build', str(PROJECT), '-c', args.configuration, '-m:1', '-v:q', '--nologo']
    with (output / 'build.log').open('w') as log:
        subprocess.run(build, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
    command = [dotnet, 'test', str(PROJECT), '-c', args.configuration, '--no-build', '--no-restore', '-m:1', '-v:q',
               '--filter', selection['filter'], '--results-directory', str(output),
               '--logger', 'trx;LogFileName=results.trx']
    print('Running affected tests with --jobs 1 and -m:1', flush=True)
    subprocess.run(command, cwd=ROOT, env=env, check=True)
    rows, counters = report(output / 'results.trx')
    verify(rows, counters, {method(row.get('testName')) for row in rows})
    (output / 'selection.json').write_text(json.dumps(selection, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'status': 'PASSED', 'selectedClasses': len(selection['classes']),
                      'total': counters['total'], 'filter': selection['filter']}), flush=True)
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('profile', choices=['affected', 'focus', 'check', 'l2', 'full'], nargs='?', default='check')
    parser.add_argument('domain', nargs='?', choices=sorted(DOMAINS))
    parser.add_argument('--match', action='append', default=[], help='Additional test-name token for focus; repeatable')
    parser.add_argument('--include-games', action='store_true', help='Explicitly allow matched full-game tests in focus')
    parser.add_argument('--l2-only', action='store_true', help='Focus excludes only explicitly declared full-opening L3 acceptance methods')
    parser.add_argument('--configuration', choices=['Debug', 'Release'], default='Release',
                        help='Use the same build configuration for build, discovery and execution (default: Release)')
    parser.add_argument('--jobs', type=int, default=min(4, os.cpu_count() or 1)
                        if os.environ.get('GITHUB_ACTIONS') == 'true' else 1)
    parser.add_argument('--timings', type=Path, help='Prior TRX or timing JSON, used only to balance full shards')
    parser.add_argument('--expect-trx', type=Path, help='Full benchmark only: require exact case parity with this baseline')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--plan-only', action='store_true', help='Build and discover, then print plan without running tests')
    parser.add_argument('--base', default='origin/main', help='Affected mode base ref (default: origin/main)')
    parser.add_argument('--print-filter', action='store_true', help='Affected mode: print the computed filter and do not execute')
    args = parser.parse_args()
    if args.profile == 'affected':
        if args.domain or args.match or args.include_games or args.l2_only or args.plan_only or args.expect_trx:
            parser.error('affected accepts --base, --print-filter, --configuration, --output and --jobs only.')
        # Affected mode is deliberately serial even on CI; --jobs is accepted
        # for a uniform invocation but cannot increase the safety gate.
        args.jobs = 1
        return run_affected(args)
    if args.print_filter or args.base != 'origin/main':
        parser.error('--base and --print-filter apply only to affected.')
    tokens = DOMAINS.get(args.domain, []) + args.match
    if not 1 <= args.jobs <= 16 or args.profile == 'focus' and not tokens:
        parser.error('Use 1..16 jobs; focus requires a domain or --match.')
    if args.profile != 'focus' and tokens:
        parser.error('Domains and --match apply only to focus; refusing to ignore a requested filter.')
    if args.include_games and args.profile != 'focus':
        parser.error('--include-games applies only to focus.')
    if args.l2_only and (args.profile != 'focus' or args.include_games):
        parser.error('--l2-only applies only to focus without full-game inclusion.')
    if any(not re.fullmatch(r'[A-Za-z0-9_.]+', token) for token in tokens):
        parser.error('Filter tokens allow letters, numbers, underscores and dots only.')
    if args.expect_trx and args.profile != 'full':
        parser.error('--expect-trx requires full.')
    output = (args.output or ROOT / 'var/qa/rules-tests' / datetime.now().strftime('%Y%m%d-%H%M%S-%f')).resolve()
    output.mkdir(parents=True, exist_ok=True)
    if (output / 'plan.json').exists():
        parser.error('Output already contains a run; use a new directory to avoid stale evidence.')
    # PATH may select a machine-wide resource limiter; never bypass it with a home SDK.
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise ValueError('dotnet SDK not found on PATH.')
    env = dict(os.environ)
    start = time.monotonic()
    print(f'Incremental build; evidence: {output}', flush=True)
    with (output / 'build.log').open('w') as log:
        # Keep project builds in one MSBuild node; test-process sharding remains independent.
        subprocess.run([dotnet, 'build', str(PROJECT), '-c', args.configuration, '-m:1', '-v:q', '--nologo'], cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
    discovery = subprocess.run([dotnet, 'test', str(PROJECT), '-c', args.configuration, '--no-build', '--no-restore', '-m:1', '--list-tests', '-v:q'],
                               cwd=ROOT, env=env, capture_output=True, text=True, check=True)
    (output / 'discovery.log').write_text(discovery.stdout + discovery.stderr)
    methods = discover(discovery.stdout)
    timings = args.timings
    if timings is None:
        # Timing history affects balancing only, never membership or pass reuse.
        candidates = sorted((PROJECT / 'TestResults').glob('*full*.trx'), key=lambda p: p.stat().st_mtime, reverse=True)
        timings = candidates[0] if candidates else ROOT / 'scripts/rule-test-timings.json'
    shards = plan(methods, args.profile, args.jobs, tokens, load_weights(timings), args.include_games, args.l2_only)
    document = {'profile': args.profile, 'configuration': args.configuration, 'discoveredMethods': len(methods), 'jobs': args.jobs,
                'includeGames': args.include_games,
                'l2Only': args.l2_only or args.profile == 'l2',
                'deferredL3Methods': sorted(l3_acceptance_methods()) if args.l2_only or args.profile == 'l2' else [],
                'testAssemblySha256': hashlib.sha256((PROJECT / f'bin/{args.configuration}/net10.0/Riftbound.ConformanceTests.dll').read_bytes()).hexdigest(),
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
                command = [dotnet, 'test', str(PROJECT), '-c', args.configuration, '--no-build', '--no-restore', '-m:1', '-v:q', '--filter', shard['filter'],
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
