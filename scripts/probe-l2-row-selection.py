#!/usr/bin/env python3
"""Prove the production selector/filter against dummy bodies on the actual Linux SDK."""
import argparse
from collections import Counter
import copy
import importlib.util
import json
import os
from pathlib import Path
import platform
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('row_probe_driver', ROOT / 'scripts/l2-distributed.py')
D = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(D)
S = D.SELECT
PREFIX = 'WorkflowToolProbe.CaseFilterProbe.'


def probe(commit, output):
    output.mkdir(parents=True)
    checkout = D.read(output.parent / 'checkout.json')
    sdk = subprocess.check_output(['dotnet', '--version'], cwd=ROOT, text=True).strip()
    if (platform.system() != 'Linux' or sdk != '10.0.403' or checkout['actualSha'] != commit
            or checkout['eventSha'] != commit):
        raise ValueError('Probe requires the actual fixed Linux/10.0.403 candidate.')
    project = ROOT / 'scripts/ci/L2RowFilterProbe/L2RowFilterProbe.csproj'
    D.run_logged(['dotnet', 'build', str(project), '-m:1', '-v:q', '--nologo'], output / 'build.log')
    dll = project.parent / 'bin/Debug/net10.0/L2RowFilterProbe.dll'
    original_sha = D.digest(dll)
    D.run_logged(['dotnet', 'vstest', str(dll), '--TestAdapterPath:' + str(dll.parent), '--ListTests'], output / 'discovery.log')
    discovered = [line.strip() for line in (output / 'discovery.log').read_text().splitlines()
                  if line.startswith('    WorkflowToolProbe.')]
    expanded = [name for name in discovered if name.startswith(PREFIX + 'Expanded(')]
    edges = [name for name in discovered if name.startswith(PREFIX + 'Escaped(')]
    collision = [name for name in discovered if name.startswith(PREFIX + 'CaseFoldCollision(')]
    if len(expanded) != 6 or len(edges) != 2 or len(collision) != 2 or PREFIX + 'Unexpanded' not in discovered:
        raise ValueError('Dummy discovery does not exhibit the required expanded and unexpanded shapes.')
    observations, selected_shards, union = [], [], []

    def execute(label, units, allowed, expected_names=None, expected_exit=0):
        folder = output / label
        folder.mkdir()
        shard = dict(selectors=units)
        S.write_settings(folder / 'selection.runsettings', shard)
        env = dict(os.environ, ROW_PROBE_ALLOWED=json.dumps(allowed), ROW_PROBE_JOURNAL=str(folder / 'invoked.jsonl'))
        code = D.run_visible(['dotnet', 'vstest', str(dll), '--TestAdapterPath:' + str(dll.parent),
            '--Settings:' + str(folder / 'selection.runsettings'), '--ResultsDirectory:' + str(folder),
            '--logger:trx;LogFileName=results.trx'], folder / 'test.log', folder / 'runtime.json',
            dict(commit=commit, sdkVersion=sdk, testAssemblySha256=original_sha, kind='DUMMY_FILTER_PROBE', label=label), env=env)
        trx = folder / 'results.trx'
        rows = ET.parse(trx).findall('.//t:UnitTestResult', D.RULES.NS)
        names = [row.get('testName') for row in rows]
        journal = folder / 'invoked.jsonl'
        calls = [json.loads(line) for line in journal.read_text().splitlines()] if journal.exists() else []
        if (code != expected_exit or Counter(calls) != Counter(allowed)
                or any(row.get('outcome') != 'Passed' for row in rows)
                or (expected_names is not None and Counter(names) != Counter(expected_names))):
            raise ValueError('Actual dummy rows/body invocations differ: ' + label)
        if expected_exit == 0:
            parsed, counters = D.RULES.report(trx)
            D.RULES.verify(parsed, counters, {D.RULES.method(name) for name in names})
        observations.append(dict(label=label, expression=S.expression(shard), exitCode=code,
            expectedExitCode=expected_exit, actualNames=names, bodyInvocations=calls, trxSha256=D.digest(trx)))
        return names

    for index, colors in enumerate([['red', 'green'], ['blue', 'orange'], ['purple', 'yellow']], 1):
        wanted = [name for name in expanded if any('"' + color + '"' in name for color in colors)]
        units = [dict(kind='case', method=PREFIX + 'Expanded', case=name) for name in wanted]
        allowed = ['Expanded:' + color for color in colors]
        if index == 1:
            units.append(dict(kind='method', method=PREFIX + 'WholeMethod'))
            wanted.append(PREFIX + 'WholeMethod')
            allowed.append('Fact:single')
        selected_shards.append(dict(name=f'shard-{index}', selectors=units,
                                    methods=sorted({unit['method'] for unit in units})))
        union.extend(execute(f'slice-{index}', units, allowed, wanted))
    cases = {PREFIX + 'Expanded': expanded, PREFIX + 'WholeMethod': [PREFIX + 'WholeMethod']}
    S.validate(selected_shards, cases, discovered)
    if Counter(union) != Counter(expanded + [PREFIX + 'WholeMethod']):
        raise ValueError('Actual mixed row/whole-method union is not exact once.')
    controls = []

    def rejects(label, shards, expected):
        try:
            S.validate(shards, expected, discovered)
        except ValueError:
            controls.append(label)
        else:
            raise ValueError('Negative control incorrectly accepted: ' + label)

    missing = copy.deepcopy(selected_shards)
    missing[-1]['selectors'].pop()
    rejects('missing-row', missing, cases)
    repeated = copy.deepcopy(selected_shards)
    repeated[-1]['selectors'].append(copy.deepcopy(selected_shards[1]['selectors'][0]))
    rejects('repeated-row', repeated, cases)
    overlap = copy.deepcopy(selected_shards)
    overlap[0]['selectors'].append(dict(kind='method', method=PREFIX + 'Expanded'))
    rejects('whole-method-row-overlap', overlap, cases)
    execute('escaped-values', [dict(kind='case', method=PREFIX + 'Escaped', case=name) for name in edges],
            ['Escaped:a|b&(c)=!~\\xyz', 'Escaped:a,b "q" 中文'], edges)
    execute('case-fold-collision', [dict(kind='case', method=PREFIX + 'CaseFoldCollision', case=collision[0])],
            ['Collision:case', 'Collision:CASE'], collision)
    rejects('ambiguous-row', [dict(name='shard-1', methods=[PREFIX + 'CaseFoldCollision'],
            selectors=[dict(kind='case', method=PREFIX + 'CaseFoldCollision', case=collision[0])])],
            {PREFIX + 'CaseFoldCollision': collision})
    execute('case-fold-whole-fallback', [dict(kind='method', method=PREFIX + 'CaseFoldCollision')],
            ['Collision:case', 'Collision:CASE'], collision)
    opaque = execute('unexpanded-whole-fallback', [dict(kind='method', method=PREFIX + 'Unexpanded')], ['Opaque:1', 'Opaque:2'])
    if len(opaque) != 2:
        raise ValueError('Unexpanded whole-method fallback lost a body.')
    rejects('unexpanded-row', [dict(name='shard-1', methods=[PREFIX + 'Unexpanded'],
            selectors=[dict(kind='case', method=PREFIX + 'Unexpanded', case=opaque[0])])], {PREFIX + 'Unexpanded': opaque})
    execute('unexpanded-row-refused', [dict(kind='case', method=PREFIX + 'Unexpanded', case=opaque[0])], [], [], 1)
    if D.digest(dll) != original_sha:
        raise ValueError('Dummy execution changed the built assembly.')
    D.write(output / 'receipt.json', dict(status='LINUX_ROW_SELECTION_PROBE_PASSED', commit=commit,
        sdk=sdk, platform=platform.system(), architecture=platform.machine(),
        runId=checkout['runId'], runAttempt=checkout['runAttempt'], dummyAssemblySha256=original_sha,
        packageVersions={'Microsoft.NET.Test.Sdk': '17.12.0', 'xunit': '2.9.2', 'xunit.runner.visualstudio': '2.8.2'},
        riftboundGameAssemblyLoaded=False, bodyUnionExact=True, negativeControlsPassed=True,
        controls=controls, observations=observations, gameSemanticPassClaim=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    probe(args.commit, args.output.resolve())
