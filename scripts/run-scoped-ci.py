#!/usr/bin/env python3
"""Execute only a reproducible fixed-input scoped decision; never label it full."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('ci_scope', ROOT/'scripts/ci-change-scope.py')
scope = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scope)


def command_for(decision):
    expected = scope.tier_details(decision['changes'])
    for key in ('tier', 'full', 'profile', 'sql', 'network', 'packaging', 'matches'):
        if decision.get(key) != expected[key]:
            raise ValueError('Scope declaration differs from conservative classification: '+key)
    if expected['tier'] not in {'L1', 'L2'}:
        raise ValueError('Only explicit L1/L2 decisions may run scoped CI')
    args = [sys.executable, str(ROOT/'scripts/test-rules.py'), expected['profile']]
    for name in expected['matches']:
        args.extend(['--match', name])
    return args


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--decision', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    recorded = json.loads(a.decision.read_text())
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    actual = scope.decide(os.environ['GITHUB_EVENT_NAME'], event, os.environ['GITHUB_SHA'], ROOT, allow_fetch=True)
    if actual != {k:recorded[k] for k in actual}:
        raise ValueError('Actual Git inputs do not reproduce the recorded scope')
    command = command_for(actual)+['--output', str(a.output/'tests')]
    if a.output.exists():
        raise ValueError('Fresh scoped output required')
    subprocess.run(command, cwd=ROOT, check=True)
    summary = json.loads((a.output/'tests/summary.json').read_text())
    if summary['status'] != 'PASSED' or summary['profile'] != actual['profile']:
        raise ValueError('No passing result for the selected profile')
    (a.output/'scope-result.json').write_text(json.dumps(dict(status='SCOPED_RULE_TESTS_PASSED_FULL_NOT_RUN',
        commit=actual['headSha'], tier=actual['tier'], decision=actual, result=summary,
        sqlNetworkResults='REQUIRED_SEPARATE_JOBS' if actual['sql'] else 'NOT_REQUIRED_RUNTIME_UNCHANGED'), indent=2)+'\n')


if __name__ == '__main__':
    main()
