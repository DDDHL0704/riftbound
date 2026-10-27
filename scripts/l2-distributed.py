#!/usr/bin/env python3
"""One fixed L2 build, duration-balanced execution, and derived coverage views."""
import argparse
from collections import Counter, defaultdict
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import tarfile
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('distributed_rules', ROOT / 'scripts/test-rules.py')
RULES = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RULES)
ASSEMBLY = 'Riftbound.ConformanceTests.dll'
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
    if not 1 <= count <= 8 or len(methods) < count:
        raise ValueError('Require 1..8 nonempty L2 shards.')
    if any(not isinstance(v, (int, float)) or not math.isfinite(v) or v < 0 for v in weights.values()):
        raise ValueError('Invalid duration history; it never controls membership.')
    bins = [dict(name=f'shard-{i + 1}', methods=[], estimatedSeconds=0.0) for i in range(count)]
    for method in sorted(methods, key=lambda name: (-weights.get(name, 1), name)):
        shard = min(bins, key=lambda item: (item['estimatedSeconds'], len(item['methods']), item['name']))
        shard['methods'].append(method)
        shard['estimatedSeconds'] += weights.get(method, 1)
    for shard in bins:
        shard['methods'].sort()
    return bins


def make_plan(text, inventory, weights, count=6):
    discovered = RULES.discover(text)
    selected = RULES.l2_rule_methods(discovered)
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
    result = dict(schemaVersion=1, profile='l2-distributed', methods=sorted(selected),
                  cases=dict(sorted(cases.items())), deferredL3Methods=sorted(RULES.l3_acceptance_methods()),
                  shards=partition(selected, weights, count),
                  targeted=dict(mode='full-fallback', reason='No audited dependency closure; validate the complete L2 input.',
                                methods=sorted(selected), execution='DERIVED_FROM_ORIGINAL_SHARD_TRX'))
    validate_plan(result, text)
    return result


def validate_plan(plan, text):
    expected = RULES.l2_rule_methods(RULES.discover(text))
    names = [shard['name'] for shard in plan['shards']]
    assigned = Counter(method for shard in plan['shards'] for method in shard['methods'])
    if (plan.get('schemaVersion') != 1 or plan.get('profile') != 'l2-distributed'
            or set(plan['methods']) != expected or len(plan['methods']) != len(expected)
            or set(assigned) != expected or any(n != 1 for n in assigned.values())
            or not names or len(set(names)) != len(names)
            or any(not re.fullmatch(r'shard-[1-8]', name) for name in names)
            or any(not shard['methods'] for shard in plan['shards'])
            or set(plan['cases']) != expected
            or set(plan['deferredL3Methods']) != RULES.l3_acceptance_methods()):
        raise ValueError('Distributed plan must partition the exact required L2 methods once.')
    if any(not rows or any(RULES.method(row) != method for row in rows) for method, rows in plan['cases'].items()):
        raise ValueError('Invalid exact parameter-row inventory.')
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
    expected = make_plan((prepared / 'discovery.log').read_text(), inventory, {}, len(plan['shards']))
    if plan['cases'] != expected['cases']:
        raise ValueError('Plan changed the freshly enumerated parameter rows.')
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
        expected = Counter(row for method in shard['methods'] for row in plan['cases'][method])
        if actual != expected or receipt.get('counters') != counters:
            raise ValueError('Missing, repeated, foreign parameter rows or false shard counters.')
        all_cases.update(actual)
        seconds += receipt['seconds']
        case_map.extend(dict(name=row.get('testName'), method=RULES.method(row.get('testName')),
                             shard=shard['name'], rowIndex=index, trxSha256=receipt['trxSha256'])
                        for index, row in enumerate(rows))
    expected_all = Counter(row for rows in plan['cases'].values() for row in rows)
    if all_cases != expected_all:
        raise ValueError('Actual case union differs from complete fixed L2 inventory.')
    return plan, sorted(case_map, key=lambda row: (row['name'], row['shard'])), seconds


def derived_view(prepared, inputs, commit):
    plan, mapping, _ = checked_results(prepared, inputs, commit)
    methods = set(plan['targeted']['methods'])
    return dict(status='TARGETED_COVERAGE_VERIFIED', commit=commit,
                execution='DERIVED_FROM_ORIGINAL_SHARD_TRX', planSha256=digest(prepared / 'plan.json'),
                testAssemblySha256=plan['testAssemblySha256'], selection=plan['targeted'],
                cases=[row for row in mapping if row['method'] in methods])


def aggregate(prepared, inputs, targeted, quality, commit, jobs):
    if set(jobs) != {'quality', 'prepare', 'rule_suite', 'targeted'} or any(v != 'success' for v in jobs.values()):
        raise ValueError('Every fixed-input L2 job must execute and succeed.')
    plan, mapping, seconds = checked_results(prepared, inputs, commit)
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
    return dict(status='L2_REQUIRED_PASSED', commit=commit, mode='ONE_EXECUTION_DERIVED_TARGETED', jobs=jobs,
                methods=len(plan['methods']), cases=len(mapping), shards=len(plan['shards']),
                testAssemblySha256=plan['testAssemblySha256'], planSha256=digest(prepared / 'plan.json'),
                rawCaseMapping=mapping, shardExecutionSeconds=seconds,
                l3SqlNetworkRestartOrFullMatrixCertified=False)


def run_logged(command, output):
    with Path(output).open('x') as log:
        subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)


def prepare(commit, output, count, github_output):
    began = time.monotonic()
    checkout = read(output / 'checkout.json')
    if checkout.get('actualSha') != commit or checkout.get('eventSha') != commit:
        raise ValueError('Verify the exact checkout before building.')
    sdk = subprocess.check_output(['dotnet', '--version'], cwd=ROOT, text=True).strip()
    run_logged(['dotnet', 'build', str(RULES.PROJECT), '-m:1', '-v:q', '--nologo'], output / 'build.log')
    bundle = RULES.PROJECT / 'bin/Debug/net10.0'
    run_logged(['dotnet', 'vstest', str(bundle / ASSEMBLY), '--TestAdapterPath:' + str(bundle), '--ListTests'], output / 'discovery.log')
    text = (output / 'discovery.log').read_text()
    selected = RULES.l2_rule_methods(RULES.discover(text))
    names = defaultdict(list)
    for name in discovered_cases(text):
        if RULES.method(name) in selected:
            names[RULES.method(name)].append(name)
    bare = sorted(method for method, rows in names.items() if rows == [method])
    write(output / 'unexpanded-methods.json', bare)
    inventory_project = ROOT / 'scripts/ci/L2CaseInventory'
    run_logged(['dotnet', 'build', str(inventory_project), '-m:1', '-v:q', '--nologo'], output / 'inventory-build.log')
    helper = inventory_project / 'bin/Debug/net10.0/L2CaseInventory.dll'
    run_logged(['dotnet', str(helper), str(bundle / ASSEMBLY), str(output / 'unexpanded-methods.json'),
                str(output / 'case-inventory.json')], output / 'inventory.log')
    for suffix in ['.dll', '.deps.json', '.runtimeconfig.json']:
        shutil.copyfile(helper.with_name('L2CaseInventory' + suffix), bundle / ('L2CaseInventory' + suffix))
    files = bundle_files(bundle)
    write(output / 'bundle-manifest.json', files)
    weights = RULES.load_weights(ROOT / 'scripts/l2-method-timings.json')
    plan = make_plan(text, read(output / 'case-inventory.json'), weights, count)
    plan.update(commit=commit, sdkVersion=sdk, runId=checkout['runId'], runAttempt=checkout['runAttempt'],
                testAssemblySha256=files[ASSEMBLY], bundleManifestSha256=digest(output / 'bundle-manifest.json'),
                caseInventorySha256=digest(output / 'case-inventory.json'), discoverySha256=digest(output / 'discovery.log'),
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
    settings = ET.Element('RunSettings')
    config = ET.SubElement(settings, 'RunConfiguration')
    ET.SubElement(config, 'TestCaseFilter').text = '|'.join('FullyQualifiedName=' + method for method in shard['methods'])
    ET.SubElement(config, 'TreatNoTestsAsError').text = 'true'
    ET.ElementTree(settings).write(output / 'execution.runsettings', encoding='utf-8', xml_declaration=True)
    command = ['dotnet', 'vstest', str(bundle / ASSEMBLY), '--Settings:' + str(output / 'execution.runsettings'),
               '--TestAdapterPath:' + str(bundle), '--ResultsDirectory:' + str(output), '--logger:trx;LogFileName=results.trx']
    with (output / 'test.log').open('x') as log:
        code = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT).returncode
    receipt = dict(status='FAILED', exitCode=code, commit=commit, shard=name, sdkVersion=sdk,
                   planSha256=digest(prepared / 'plan.json'), bundleManifestSha256=plan['bundleManifestSha256'],
                   testAssemblySha256=plan['testAssemblySha256'], moduleAuditSha256=digest(output / 'module-audit.json'),
                   seconds=round(time.monotonic() - began, 3))
    try:
        if code or bundle_files(bundle) != files:
            raise ValueError('Test process failed or changed the fixed bundle.')
        rows, counters = RULES.report(output / 'results.trx')
        RULES.verify(rows, counters, set(shard['methods']))
        expected = Counter(row for method in shard['methods'] for row in plan['cases'][method])
        if Counter(row.get('testName') for row in rows) != expected:
            raise ValueError('Actual parameter rows differ from the fresh fixed inventory.')
        receipt.update(status='PASSED', counters=counters, trxSha256=digest(output / 'results.trx'))
    finally:
        write(output / 'receipt.json', receipt)
    print(json.dumps(receipt))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['prepare', 'shard', 'targeted', 'aggregate'])
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
        prepare(args.commit, args.output.resolve(), args.shards, args.github_output)
    elif args.mode == 'shard':
        execute_shard(args.prepared.resolve(), args.output.resolve(), args.commit, args.shard)
    elif args.mode == 'targeted':
        write(args.output, derived_view(args.prepared, args.inputs, args.commit))
    else:
        jobs = {name: os.environ.get('RIFTBOUND_' + name.upper(), '') for name in ['quality', 'prepare', 'rule_suite', 'targeted']}
        write(args.output, aggregate(args.prepared, args.inputs, args.targeted, args.quality, args.commit, jobs))


if __name__ == '__main__':
    main()
