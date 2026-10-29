#!/usr/bin/env python3
"""One fixed build, duration-balanced rule execution, and exact case coverage."""
import argparse
from collections import Counter, defaultdict
import hashlib
import importlib.util
import json
import os
import platform
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tarfile
import time

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('distributed_rules', ROOT / 'scripts/test-rules.py')
RULES = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RULES)
SELECT_SPEC = importlib.util.spec_from_file_location('l2_case_selection', ROOT / 'scripts/l2_case_selection.py')
SELECT = importlib.util.module_from_spec(SELECT_SPEC)
SELECT_SPEC.loader.exec_module(SELECT)
ASSEMBLY = 'Riftbound.ConformanceTests.dll'
CONFIGURATION = 'Release'
HEX = re.compile(r'[0-9a-f]{64}')
MODULES = {'Riftbound.ConformanceTests', 'Riftbound.Engine', 'Riftbound.Contracts',
           'Riftbound.CardCatalog', 'Riftbound.Persistence', 'Riftbound.Api'}
MVID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text())


def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('x') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


def discovered_cases(text):
    RULES.discover(text)  # Retain fail-closed discovery parsing.
    return [line.strip() for line in text.splitlines() if line.startswith('    ') and line.strip()]


def partition(methods, weights, count):
    return SELECT.partition([dict(kind='method', method=m) for m in methods],
                            {m: [m] for m in methods}, weights, count)


def source_scope_methods(scope, text):
    discovered = RULES.discover(text)
    records = scope.get('methods', [])
    deferred = RULES.l3_acceptance_methods()
    names = [row['method'] for row in records]
    selected = {row['method'] for row in records if row['includeInSourceGuardBatch']}
    if (scope.get('schemaVersion') != 1 or scope.get('discoverySha256') != hashlib.sha256(text.encode()).hexdigest()
            or scope.get('discoveredMethods') != len(discovered) or scope.get('declaredTestMethods') != len(discovered)
            or scope.get('unmatchedDeclaredMethods') != [] or scope.get('unmatchedDiscoveredMethods') != []
            or len(names) != len(set(names)) or not set(names) <= discovered
            or scope.get('sourceRelatedMethodCount') != len(records) or scope.get('sourceGuardBatchCount') != len(selected)
            or scope.get('semanticTestsExecuted') is not False or scope.get('existingLanesChanged') is not False
            or scope.get('unknownMethodsRemainInExistingRuleSuite') is not True
            or any(row['method'] in deferred and row['existingLane'] != 'L3' for row in records)):
        raise ValueError('Source-scope inventory differs from the complete fresh declaration/discovery input.')
    required = RULES.l2_rule_methods(discovered)
    return sorted(selected & required), sorted(selected - required)


def selected_methods(methods, profile):
    if profile == 'l2':
        return RULES.l2_rule_methods(methods)
    if profile == 'full':
        return methods
    raise ValueError('Unknown distributed rule profile.')


def make_plan(text, inventory, source_scope, weights, count=6, profile='l2'):
    discovered = RULES.discover(text)
    selected = selected_methods(discovered, profile)
    cases = defaultdict(list)
    for name in discovered_cases(text):
        if RULES.method(name) in selected:
            cases[RULES.method(name)].append(name)
    bare = {method for method, rows in cases.items() if rows == [method]}
    if set(inventory['cases']) != bare:
        raise ValueError('Fresh data inventory must cover exactly the unexpanded discovery methods.')
    for method in bare:
        cases[method] = inventory['cases'][method]
    if any(not rows or any(RULES.method(row) != method for row in rows) for method, rows in cases.items()):
        raise ValueError('Empty or foreign parameter rows in fresh inventory.')
    source_l2, source_nightly = source_scope_methods(source_scope, text)
    units = SELECT.selectors(discovered_cases(text), cases, weights)
    result = dict(schemaVersion=3, profile=profile + '-distributed', methods=sorted(selected),
                  cases=dict(sorted(cases.items())),
                  deferredL3Methods=sorted(RULES.l3_acceptance_methods()) if profile == 'l2' else [],
                  shards=SELECT.partition(units, cases, weights, count),
                  sourceGuardL2Methods=source_l2, sourceGuardNightlyMethods=source_nightly,
                  targeted=dict(mode='full-fallback', reason='No audited dependency closure; validate the complete profile input.',
                                methods=sorted(selected), execution='DERIVED_FROM_ORIGINAL_SHARD_TRX'))
    validate_plan(result, text)
    return result


def validate_plan(plan, text):
    profile = {'l2-distributed': 'l2', 'full-distributed': 'full'}.get(plan.get('profile'))
    expected = selected_methods(RULES.discover(text), profile)
    deferred = RULES.l3_acceptance_methods() if profile == 'l2' else set()
    names = [shard['name'] for shard in plan['shards']]
    if (plan.get('schemaVersion') != 3
            or set(plan['methods']) != expected or len(plan['methods']) != len(expected)
            or not names or len(set(names)) != len(names)
            or any(not re.fullmatch(r'shard-[1-9]', name) for name in names)
            or any(not shard['methods'] for shard in plan['shards'])
            or set(plan['cases']) != expected
            or set(plan['deferredL3Methods']) != deferred
            or len(plan['deferredL3Methods']) != len(deferred)):
        raise ValueError('Distributed plan must partition the exact required case inventory once.')
    if any(not rows or any(RULES.method(row) != method for row in rows) for method, rows in plan['cases'].items()):
        raise ValueError('Invalid exact parameter-row inventory.')
    discovered = defaultdict(list)
    for name in discovered_cases(text):
        method = RULES.method(name)
        if method in expected:
            discovered[method].append(name)
    for method, names in discovered.items():
        # Runtime inventory is authoritative only for unexpanded discovery.
        if names != [method] and Counter(names) != Counter(plan['cases'][method]):
            raise ValueError('Fresh discovery parameter rows differ from the planned rows.')
    SELECT.validate(plan['shards'], plan['cases'], discovered_cases(text))
    target = plan['targeted']
    if (target.get('mode') != 'full-fallback' or set(target['methods']) != expected
            or len(target['methods']) != len(expected)
            or target.get('execution') != 'DERIVED_FROM_ORIGINAL_SHARD_TRX'):
        raise ValueError('Unknown impact must fall back to complete derived coverage.')


def bundle_files(folder):
    return {str(path.relative_to(folder)): digest(path) for path in sorted(Path(folder).rglob('*')) if path.is_file()}


def check_prepared(prepared, commit):
    plan = read(prepared / 'plan.json')
    validate_plan(plan, (prepared / 'discovery.log').read_text())
    checkout = read(prepared / 'checkout.json')
    if (not re.fullmatch(r'[0-9a-f]{40}', commit) or plan['commit'] != commit
            or checkout.get('status') != 'FIXED_CANDIDATE_CHECKOUT_VERIFIED'
            or checkout.get('actualSha') != commit or checkout.get('expectedSha') != commit
            or checkout.get('eventSha') != commit
            or not str(plan['runId']).isdigit() or not str(plan['runAttempt']).isdigit()
            or checkout.get('runId') != plan['runId'] or checkout.get('runAttempt') != plan['runAttempt']):
        raise ValueError('Prepared plan or checkout differs from the exact candidate.')
    files = read(prepared / 'bundle-manifest.json')
    if (not files or ASSEMBLY not in files or any(not HEX.fullmatch(value) for value in files.values())
            or plan['testAssemblySha256'] != files[ASSEMBLY]
            or digest(prepared / 'bundle-manifest.json') != plan['bundleManifestSha256']
            or digest(prepared / 'case-inventory.json') != plan['caseInventorySha256']
            or digest(prepared / 'discovery.log') != plan['discoverySha256']):
        raise ValueError('Prepared assembly, case inventory or discovery identity differs.')
    inventory = read(prepared / 'case-inventory.json')
    probe = read(prepared / 'row-filter-probe/receipt.json')
    if digest(prepared / 'row-filter-probe/receipt.json') != plan.get('rowProbeSha256'):
        raise ValueError('Linux row-selection probe artifact identity changed.')
    validate_probe(probe, commit, plan['sdkVersion'], plan['runId'], plan['runAttempt'])
    source_scope_path = prepared / 'source-scope/source-guard-methods.json'
    source_scope = read(source_scope_path)
    if source_scope.get('sourceHead') != commit or digest(source_scope_path) != plan.get('sourceScopeSha256'):
        raise ValueError('Source-scope candidate or artifact identity differs from the prepared build.')
    profile = 'full' if plan['profile'] == 'full-distributed' else 'l2'
    expected = make_plan((prepared / 'discovery.log').read_text(), inventory, source_scope, {}, len(plan['shards']), profile)
    if plan['cases'] != expected['cases']:
        raise ValueError('Plan changed the freshly enumerated parameter rows.')
    if any(plan[key] != expected[key] for key in ['sourceGuardL2Methods', 'sourceGuardNightlyMethods']):
        raise ValueError('Source guard view changed the classified methods or their original execution lanes.')
    modules = inventory.get('modules', [])
    if len(modules) != len(MODULES) or {module.get('name') for module in modules} != MODULES:
        raise ValueError('Require each of the exact six built modules once.')
    for module in modules:
        if (not HEX.fullmatch(module.get('sha256', '')) or not MVID.fullmatch(module.get('mvid', ''))
                or module['name'] + '.dll' not in files
                or module['sha256'] != files[module['name'] + '.dll']
                or not re.fullmatch(r'\d+\.\d+\.\d+\+' + commit, module.get('informationalVersion', ''))):
            raise ValueError('Built module identity differs from the fixed source revision.')
    return plan


def checked_results(prepared, inputs, commit):
    plan = check_prepared(prepared, commit)
    expected_names = {shard['name'] for shard in plan['shards']}
    folders = {path.name for path in inputs.iterdir() if path.is_dir()}
    if folders != expected_names:
        raise ValueError('Missing or extra execution shard artifacts.')
    all_cases, case_map, seconds = Counter(), [], 0.0
    for shard in plan['shards']:
        folder = inputs / shard['name']
        receipt, checkout = read(folder / 'receipt.json'), read(folder / 'checkout.json')
        if (receipt.get('status') != 'PASSED' or receipt.get('exitCode') != 0
                or receipt.get('commit') != commit or receipt.get('shard') != shard['name']
                or receipt.get('planSha256') != digest(prepared / 'plan.json')
                or receipt.get('bundleManifestSha256') != plan['bundleManifestSha256']
                or receipt.get('testAssemblySha256') != plan['testAssemblySha256']
                or receipt.get('sdkVersion') != plan['sdkVersion']
                or checkout.get('status') != 'FIXED_CANDIDATE_CHECKOUT_VERIFIED'
                or checkout.get('actualSha') != commit or checkout.get('expectedSha') != commit
                or checkout.get('eventSha') != commit
                or checkout.get('runId') != plan['runId'] or checkout.get('runAttempt') != plan['runAttempt']
                or checkout.get('treeSha') != read(prepared / 'checkout.json')['treeSha']
                or receipt.get('trxSha256') != digest(folder / 'results.trx')):
            raise ValueError('Shard execution, checkout, SDK or built-assembly identity differs.')
        rows, counters = RULES.report(folder / 'results.trx')
        if (digest(folder / 'module-audit.json') != receipt.get('moduleAuditSha256')
                or read(folder / 'module-audit.json')['modules'] != read(prepared / 'case-inventory.json')['modules']):
            raise ValueError('Shard loaded-module audit differs from the built bundle.')
        RULES.verify(rows, counters, set(shard['methods']))
        actual = Counter(row.get('testName') for row in rows)
        SELECT.verify_settings(folder / 'execution.runsettings', shard)
        if receipt.get('executionSettingsSha256') != digest(folder / 'execution.runsettings'):
            raise ValueError('Execution settings identity differs from the planned selectors.')
        runtime_path = folder / 'execution-runtime.json'
        runtime = read(runtime_path)
        if (receipt.get('runtimeReceiptSha256') != digest(runtime_path)
                or runtime.get('commit') != commit or runtime.get('sdkVersion') != plan['sdkVersion']
                or runtime.get('testAssemblySha256') != plan['testAssemblySha256']
                or runtime.get('exitCode') != 0 or not runtime.get('samples')):
            raise ValueError('Runtime resource receipt differs from this execution.')
        if (runtime.get('samplesSha256') != digest(runtime_path.with_suffix('.jsonl'))
                or [json.loads(line) for line in runtime_path.with_suffix('.jsonl').read_text().splitlines()] != runtime['samples']):
            raise ValueError('Runtime resource samples differ from the execution receipt.')
        expected = Counter(SELECT.case_names(shard, plan['cases']))
        if actual != expected or receipt.get('counters') != counters:
            raise ValueError('Missing, repeated, foreign parameter rows or false shard counters.')
        all_cases.update(actual)
        seconds += receipt['seconds']
        case_map.extend(dict(name=row.get('testName'), method=RULES.method(row.get('testName')),
                             shard=shard['name'], rowIndex=index, trxSha256=receipt['trxSha256'])
                        for index, row in enumerate(rows))
    expected_all = Counter(row for rows in plan['cases'].values() for row in rows)
    if all_cases != expected_all:
        raise ValueError('Actual case union differs from the complete fixed profile inventory.')
    return plan, sorted(case_map, key=lambda row: (row['name'], row['shard'])), seconds


def derived_view(prepared, inputs, commit):
    plan, mapping, _ = checked_results(prepared, inputs, commit)
    if plan['profile'] != 'l2-distributed':
        raise ValueError('Targeted L2 coverage requires the L2 profile.')
    methods = set(plan['targeted']['methods'])
    return dict(status='TARGETED_COVERAGE_VERIFIED', commit=commit,
                execution='DERIVED_FROM_ORIGINAL_SHARD_TRX', planSha256=digest(prepared / 'plan.json'),
                testAssemblySha256=plan['testAssemblySha256'], selection=plan['targeted'],
                cases=[row for row in mapping if row['method'] in methods])


def aggregate(prepared, inputs, targeted, quality, commit, jobs):
    if set(jobs) != {'quality', 'prepare', 'rule_suite', 'targeted'} or any(v != 'success' for v in jobs.values()):
        raise ValueError('Every fixed-input L2 job must execute and succeed.')
    plan, mapping, seconds = checked_results(prepared, inputs, commit)
    if plan['profile'] != 'l2-distributed':
        raise ValueError('The L2 gate requires the L2 profile.')
    q = read(quality / 'checkout.json')
    if (q.get('status') != 'FIXED_CANDIDATE_CHECKOUT_VERIFIED' or q.get('actualSha') != commit
            or q.get('expectedSha') != commit or q.get('eventSha') != commit
            or q.get('runId') != plan['runId'] or q.get('runAttempt') != plan['runAttempt']
            or q.get('treeSha') != read(prepared / 'checkout.json')['treeSha']):
        raise ValueError('Quality checkout differs from the prepared candidate.')
    if read(targeted / 'view.json') != derived_view(prepared, inputs, commit):
        raise ValueError('Targeted coverage is missing or differs from its original TRX mapping.')
    target_checkout = read(targeted / 'checkout.json')
    if any(target_checkout.get(key) != q.get(key) for key in
           ['status', 'actualSha', 'expectedSha', 'eventSha', 'treeSha', 'runId', 'runAttempt']):
        raise ValueError('Targeted view checkout differs from the required execution.')
    source_methods = set(plan['sourceGuardL2Methods'])
    return dict(status='L2_REQUIRED_PASSED', commit=commit, mode='ONE_EXECUTION_DERIVED_TARGETED', jobs=jobs,
                methods=len(plan['methods']), cases=len(mapping), shards=len(plan['shards']),
                testAssemblySha256=plan['testAssemblySha256'], planSha256=digest(prepared / 'plan.json'),
                rawCaseMapping=mapping, shardExecutionSeconds=seconds,
                sourceCoverageExecution='DERIVED_FROM_SAME_ORIGINAL_TRX',
                sourceGuardMethods=len(plan['sourceGuardL2Methods']),
                sourceGuardCaseMapping=[row for row in mapping if row['method'] in source_methods],
                sourceScopeSha256=plan['sourceScopeSha256'],
                splitMethods=len({unit['method'] for shard in plan['shards'] for unit in shard['selectors'] if unit['kind'] == 'case'}),
                rowSelectors=sum(unit['kind'] == 'case' for shard in plan['shards'] for unit in shard['selectors']),
                l3SqlNetworkRestartOrFullMatrixCertified=False)


def aggregate_full(prepared, inputs, commit, jobs):
    if set(jobs) != {'prepare', 'rule_shards'} or any(v != 'success' for v in jobs.values()):
        raise ValueError('Every fixed full-rule job must execute and succeed.')
    plan, mapping, seconds = checked_results(prepared, inputs, commit)
    if plan['profile'] != 'full-distributed' or plan['deferredL3Methods']:
        raise ValueError('Full rule coverage requires the full profile without exemptions.')
    return dict(status='FULL_RULE_SUITE_PASSED', commit=commit, jobs=jobs,
                methods=len(plan['methods']), cases=len(mapping), shards=len(plan['shards']),
                testAssemblySha256=plan['testAssemblySha256'], planSha256=digest(prepared / 'plan.json'),
                summedShardSeconds=round(seconds, 3), rawCaseMapping=mapping)


def run_logged(command, output):
    with Path(output).open('x') as log:
        subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)


def processor_identity(cpuinfo=Path('/proc/cpuinfo'), topology=Path('/sys/devices/system/cpu'), affinity=None):
    identity = dict(architecture=platform.machine())
    try:
        identity['modelNames'] = sorted({line.split(':', 1)[1].strip() for line in cpuinfo.read_text().splitlines()
                                        if ':' in line and line.split(':', 1)[0].strip() in {'model name', 'Hardware'}})
    except OSError as error:
        identity['modelReadError'] = str(error)
    try:
        cpus = sorted(affinity if affinity is not None else os.sched_getaffinity(0)
                      if hasattr(os, 'sched_getaffinity') else range(os.cpu_count() or 1))
        if not cpus:
            raise ValueError('No CPUs in the observed affinity.')
        cores = []
        for cpu in cpus:
            folder = topology / f'cpu{cpu}' / 'topology'
            socket = int((folder / 'physical_package_id').read_text())
            core = int((folder / 'core_id').read_text())
            if min(socket, core) < 0:
                raise ValueError('Physical CPU topology is unavailable.')
            cores.append(dict(cpu=cpu, socket=socket, core=core))
        identity.update(affinityLogicalCpuCount=len(cpus), affinityCpuTopology=cores,
                        affinityPhysicalCoreCount=len({(row['socket'], row['core']) for row in cores}))
    except (OSError, ValueError) as error:
        identity['topologyReadError'] = str(error)
    return identity


def process_sample(pid):
    sample = dict(atUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), processId=pid,
                  hostProcessorCount=os.cpu_count(), processes=[])
    try:
        raw = subprocess.check_output(['ps', '-eo', 'pid=,ppid=,pcpu=,rss=,comm='], text=True)
        rows = [line.split(None, 4) for line in raw.splitlines()]
        selected = {pid}
        while True:
            children = {int(row[0]) for row in rows if len(row) == 5 and int(row[1]) in selected}
            if children <= selected:
                break
            selected.update(children)
        sample['processes'] = [dict(pid=int(row[0]), ppid=int(row[1]), cpuPercentLifetime=row[2],
                                    rssKiB=int(row[3]), name=row[4])
                               for row in rows if len(row) == 5 and int(row[0]) in selected]
        if hasattr(os, 'sched_getaffinity'):
            sample['affinityCpuCount'] = len(os.sched_getaffinity(0))
        sample['cgroup'] = {name: (Path('/sys/fs/cgroup') / name).read_text().strip()
                            for name in ['cpu.max', 'cpu.stat', 'memory.max', 'memory.current']
                            if (Path('/sys/fs/cgroup') / name).is_file()}
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        sample['resourceReadError'] = type(error).__name__ + ': ' + str(error)
    return sample


def run_visible(command, log_path, runtime_path, context, interval=60, env=None):
    # A vCPU count alone cannot distinguish SMT siblings or different CPU models.
    # This observation never changes the fixed selectors or worker count.
    context = dict(context, processorIdentity=processor_identity())
    began = time.monotonic()
    samples, offset = [], 0
    sample_path = Path(runtime_path).with_suffix('.jsonl')
    def publish(process, final=False):
        nonlocal offset
        with Path(log_path).open('rb') as stream:
            stream.seek(offset)
            for data in iter(lambda: stream.read(65536), b''):
                sys.stdout.buffer.write(data)
            offset = stream.tell()
        sys.stdout.buffer.flush()
        sample = process_sample(process.pid)
        sample['elapsedSeconds'] = round(time.monotonic() - began, 3)
        sample['exitCode'] = process.poll()
        samples.append(sample)
        sample_stream.write(json.dumps(sample) + '\n')
        sample_stream.flush()
        print(json.dumps(dict(event='L2_EXECUTION_FINISHED' if final else 'L2_EXECUTION_RUNNING',
            kind=context.get('kind', 'L2_SHARD'), shard=context.get('shard'), label=context.get('label'),
            commit=context['commit'], **sample)), flush=True)
    with sample_path.open('x') as sample_stream, Path(log_path).open('xb') as log:
        process = subprocess.Popen(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, env=env)
        publish(process)
        while True:
            try:
                code = process.wait(timeout=interval)
                break
            except subprocess.TimeoutExpired:
                publish(process)
        publish(process, final=True)
    write(runtime_path, dict(**context, platform=platform.system(), architecture=platform.machine(),
                             exitCode=code, samples=samples, samplesSha256=digest(sample_path),
                             observation='ps lifetime CPU percent; RSS KiB; no case-completion inference'))
    return code


def validate_probe(probe, commit, sdk, run_id, attempt):
    if (probe.get('status') != 'LINUX_ROW_SELECTION_PROBE_PASSED' or probe.get('commit') != commit
            or probe.get('sdk') != sdk or sdk != '10.0.403' or probe.get('platform') != 'Linux'
            or probe.get('runId') != run_id or probe.get('runAttempt') != attempt
            or probe.get('riftboundGameAssemblyLoaded') is not False
            or probe.get('negativeControlsPassed') is not True or probe.get('bodyUnionExact') is not True):
        raise ValueError('Linux row-selection probe does not bind the prepared candidate and SDK.')
    labels = {'slice-1': (0, 3), 'slice-2': (0, 2), 'slice-3': (0, 2), 'escaped-values': (0, 2),
              'case-fold-collision': (0, 2), 'case-fold-whole-fallback': (0, 2),
              'unexpanded-whole-fallback': (0, 2), 'unexpanded-row-refused': (1, 0)}
    observations = probe.get('observations', [])
    if (len(observations) != len(labels) or {row.get('label') for row in observations} != set(labels)
            or sorted(probe.get('controls', [])) != sorted(['missing-row', 'repeated-row', 'whole-method-row-overlap',
                                                         'ambiguous-row', 'unexpanded-row'])):
        raise ValueError('Linux row-selection probe is missing actual positive or negative controls.')
    for row in observations:
        code, count = labels[row['label']]
        if (row.get('exitCode') != code or row.get('expectedExitCode') != code
                or len(row.get('actualNames', [])) != count or len(row.get('bodyInvocations', [])) != count
                or len(set(row.get('bodyInvocations', []))) != count
                or not HEX.fullmatch(row.get('trxSha256', ''))):
            raise ValueError('Linux row-selection probe lost or repeated actual rows/bodies.')
    calls = Counter(call for row in observations if row['label'].startswith('slice-') for call in row['bodyInvocations'])
    if calls != Counter(['Expanded:' + color for color in ['red', 'green', 'blue', 'orange', 'purple', 'yellow']] + ['Fact:single']):
        raise ValueError('Linux row-selection probe split-body union is not exact once.')


def prepare(commit, output, count, github_output, profile='l2'):
    began = time.monotonic()
    checkout = read(output / 'checkout.json')
    if checkout.get('actualSha') != commit or checkout.get('eventSha') != commit:
        raise ValueError('Verify the exact checkout before building.')
    sdk = subprocess.check_output(['dotnet', '--version'], cwd=ROOT, text=True).strip()
    validate_probe(read(output / 'row-filter-probe/receipt.json'), commit, sdk, checkout['runId'], checkout['runAttempt'])
    run_logged(['dotnet', 'build', str(RULES.PROJECT), '-c', CONFIGURATION, '-m:1', '-v:q', '--nologo'], output / 'build.log')
    bundle = RULES.PROJECT / f'bin/{CONFIGURATION}/net10.0'
    run_logged(['dotnet', 'vstest', str(bundle / ASSEMBLY), '--TestAdapterPath:' + str(bundle), '--ListTests'], output / 'discovery.log')
    text = (output / 'discovery.log').read_text()
    selected = selected_methods(RULES.discover(text), profile)
    names = defaultdict(list)
    for name in discovered_cases(text):
        if RULES.method(name) in selected:
            names[RULES.method(name)].append(name)
    bare = sorted(method for method, rows in names.items() if rows == [method])
    write(output / 'unexpanded-methods.json', bare)
    inventory_project = ROOT / 'scripts/ci/L2CaseInventory'
    run_logged(['dotnet', 'build', str(inventory_project), '-c', CONFIGURATION, '-m:1', '-v:q', '--nologo'], output / 'inventory-build.log')
    helper = inventory_project / f'bin/{CONFIGURATION}/net10.0/L2CaseInventory.dll'
    run_logged(['dotnet', str(helper), str(bundle / ASSEMBLY), str(output / 'unexpanded-methods.json'),
                str(output / 'case-inventory.json')], output / 'inventory.log')
    scanner_project = ROOT / 'tools/source-guard-scope/scanner/Scanner.csproj'
    run_logged(['dotnet', 'build', str(scanner_project), '-c', CONFIGURATION, '-m:1', '-v:q', '--nologo'], output / 'source-scanner-build.log')
    scanner = scanner_project.parent / f'bin/{CONFIGURATION}/net10.0/Scanner.dll'
    run_logged([sys.executable, str(ROOT / 'tools/source-guard-scope/generate_inventory.py'), '--repo', str(ROOT),
                '--source-head', commit, '--discovery', str(output / 'discovery.log'), '--scanner', str(scanner),
                '--output', str(output / 'source-scope')], output / 'source-inventory.log')
    for suffix in ['.dll', '.deps.json', '.runtimeconfig.json']:
        shutil.copyfile(helper.with_name('L2CaseInventory' + suffix), bundle / ('L2CaseInventory' + suffix))
    files = bundle_files(bundle)
    write(output / 'bundle-manifest.json', files)
    weights = RULES.load_weights(ROOT / 'scripts/rule-test-timings.json') if profile == 'full' else {}
    weights.update(RULES.load_weights(ROOT / 'scripts/l2-method-timings.json'))
    plan = make_plan(text, read(output / 'case-inventory.json'), read(output / 'source-scope/source-guard-methods.json'), weights, count, profile)
    plan.update(commit=commit, sdkVersion=sdk, runId=checkout['runId'], runAttempt=checkout['runAttempt'],
                testAssemblySha256=files[ASSEMBLY], bundleManifestSha256=digest(output / 'bundle-manifest.json'),
                caseInventorySha256=digest(output / 'case-inventory.json'), discoverySha256=digest(output / 'discovery.log'),
                sourceScopeSha256=digest(output / 'source-scope/source-guard-methods.json'),
                rowProbeSha256=digest(output / 'row-filter-probe/receipt.json'),
                prepareSeconds=round(time.monotonic() - began, 3))
    write(output / 'plan.json', plan)
    check_prepared(output, commit)
    with tarfile.open(output / 'bundle.tar.gz', 'x:gz') as archive:
        for name in files:
            path = bundle / name
            if path.is_symlink():
                raise ValueError('Built bundle must contain regular files only.')
            archive.add(path, arcname=name, recursive=False)
    if github_output:
        with Path(github_output).open('a') as stream:
            stream.write('sdk=' + sdk + '\n')
    print(json.dumps(dict(commit=commit, methods=len(selected), cases=sum(map(len, plan['cases'].values())),
                          sdk=sdk, shards=[dict(name=s['name'], seconds=s['estimatedSeconds']) for s in plan['shards']])))


def execute_shard(prepared, output, commit, name):
    began = time.monotonic()
    plan = check_prepared(prepared, commit)
    shard = next((row for row in plan['shards'] if row['name'] == name), None)
    if shard is None:
        raise ValueError('Unknown execution shard.')
    sdk = subprocess.check_output(['dotnet', '--version'], cwd=ROOT, text=True).strip()
    if sdk != plan['sdkVersion']:
        raise ValueError('Execution SDK differs from the build SDK.')
    bundle = prepared / 'bundle'
    bundle.mkdir()
    files = read(prepared / 'bundle-manifest.json')
    with tarfile.open(prepared / 'bundle.tar.gz', 'r:gz') as archive:
        for member in archive.getmembers():
            if not member.isfile() or member.name not in files or not (bundle / member.name).resolve().is_relative_to(bundle.resolve()):
                raise ValueError('Unexpected or unsafe built-bundle member.')
        archive.extractall(bundle, filter='data')
    if bundle_files(bundle) != files:
        raise ValueError('Downloaded built bundle differs from the prepared bytes.')
    output.mkdir(parents=True, exist_ok=True)
    if (output / 'receipt.json').exists() or (output / 'results.trx').exists():
        raise ValueError('Execution output already exists; never reuse a previous run.')
    shutil.copyfile(prepared / 'discovery.log', output / 'discovery.log')
    write(output / 'selection.json', dict(planSha256=digest(prepared / 'plan.json'), **shard))
    run_logged(['dotnet', str(bundle / 'L2CaseInventory.dll'), str(bundle / ASSEMBLY), '--modules-only',
                str(output / 'module-audit.json')], output / 'module-audit.log')
    if read(output / 'module-audit.json')['modules'] != read(prepared / 'case-inventory.json')['modules']:
        raise ValueError('Actual downloaded module metadata differs from the build observation.')
    SELECT.write_settings(output / 'execution.runsettings', shard)
    command = ['dotnet', 'vstest', str(bundle / ASSEMBLY), '--Settings:' + str(output / 'execution.runsettings'),
               '--TestAdapterPath:' + str(bundle), '--ResultsDirectory:' + str(output), '--logger:trx;LogFileName=results.trx']
    code = run_visible(command, output / 'test.log', output / 'execution-runtime.json',
                       dict(commit=commit, sdkVersion=sdk, testAssemblySha256=plan['testAssemblySha256'], shard=name,
                            moduleRuntime=read(output / 'module-audit.json').get('runtime')),
                       # Each CI testhost runs four CPU-bound collections. Server GC
                       # collects their allocation-heavy recovery work in parallel.
                       # This applies only to execution, including the nightly shards;
                       # local processor limits and every exact selector are preserved.
                       env=dict(os.environ, DOTNET_gcServer='1',
                                RIFTBOUND_TEST_COLLECTION_ORDER=str(output / 'selection.json'),
                                RIFTBOUND_TEST_RUNTIME_DIR=str(output / 'testhost-runtime')))
    receipt = dict(status='FAILED', exitCode=code, commit=commit, shard=name, sdkVersion=sdk,
                   planSha256=digest(prepared / 'plan.json'), bundleManifestSha256=plan['bundleManifestSha256'],
                   testAssemblySha256=plan['testAssemblySha256'], moduleAuditSha256=digest(output / 'module-audit.json'),
                   executionSettingsSha256=digest(output / 'execution.runsettings'),
                   runtimeReceiptSha256=digest(output / 'execution-runtime.json'),
                   seconds=round(time.monotonic() - began, 3))
    try:
        if code or bundle_files(bundle) != files:
            raise ValueError('Test process failed or changed the fixed bundle.')
        rows, counters = RULES.report(output / 'results.trx')
        RULES.verify(rows, counters, set(shard['methods']))
        expected = Counter(SELECT.case_names(shard, plan['cases']))
        if Counter(row.get('testName') for row in rows) != expected:
            raise ValueError('Actual parameter rows differ from the fresh fixed inventory.')
        receipt.update(status='PASSED', counters=counters, trxSha256=digest(output / 'results.trx'))
    finally:
        write(output / 'receipt.json', receipt)
    print(json.dumps(receipt))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['prepare', 'shard', 'targeted', 'aggregate', 'aggregate-full'])
    parser.add_argument('--profile', choices=['l2', 'full'], default='l2', help='Preparation membership; full includes every discovered method')
    parser.add_argument('--commit', required=True)
    parser.add_argument('--prepared', type=Path)
    parser.add_argument('--inputs', type=Path)
    parser.add_argument('--targeted', type=Path)
    parser.add_argument('--quality', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--shards', type=int, default=6)
    parser.add_argument('--shard')
    parser.add_argument('--github-output', type=Path)
    args = parser.parse_args()
    if args.mode == 'prepare':
        prepare(args.commit, args.output.resolve(), args.shards, args.github_output, args.profile)
    elif args.mode == 'shard':
        execute_shard(args.prepared.resolve(), args.output.resolve(), args.commit, args.shard)
    elif args.mode == 'targeted':
        write(args.output, derived_view(args.prepared, args.inputs, args.commit))
    elif args.mode == 'aggregate-full':
        jobs = {name: os.environ.get('RIFTBOUND_' + name.upper(), '') for name in ['prepare', 'rule_shards']}
        write(args.output, aggregate_full(args.prepared, args.inputs, args.commit, jobs))
    else:
        jobs = {name: os.environ.get('RIFTBOUND_' + name.upper(), '') for name in ['quality', 'prepare', 'rule_suite', 'targeted']}
        write(args.output, aggregate(args.prepared, args.inputs, args.targeted, args.quality, args.commit, jobs))


if __name__ == '__main__':
    main()
