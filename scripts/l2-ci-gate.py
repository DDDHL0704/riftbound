#!/usr/bin/env python3
"""Require actual fixed-commit focus/check TRX results for daily merging."""
import argparse
import importlib.util
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('rule_tests', ROOT / 'scripts/test-rules.py')
RULES = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RULES)


def verify_tests(folder, profile, commit):
    folder = Path(folder)
    checkout = json.loads((folder / 'checkout.json').read_text())
    if (checkout.get('status') != 'FIXED_CANDIDATE_CHECKOUT_VERIFIED'
            or checkout.get('actualSha') != commit or checkout.get('expectedSha') != commit):
        raise ValueError('L2 artifact checkout differs from the required commit.')
    tests = folder / 'tests'
    summary = json.loads((tests / 'summary.json').read_text())
    plan = json.loads((tests / 'plan.json').read_text())
    if (summary.get('status') != 'PASSED' or summary.get('profile') != profile
            or plan.get('profile') != profile or not plan.get('shards')
            or not re.fullmatch(r'[0-9a-f]{64}', plan.get('testAssemblySha256', ''))):
        raise ValueError('A successful nonempty actual L2 plan and summary are required.')
    names, methods, total = set(), set(), 0
    for shard in plan['shards']:
        name, expected = shard['name'], set(shard['methods'])
        if not re.fullmatch(r'[a-z0-9-]+', name) or name in names or not expected or methods & expected:
            raise ValueError('L2 shard plan is empty, duplicated or invalid.')
        names.add(name)
        methods.update(expected)
        rows, counters = RULES.report(tests / name / 'results.trx')
        RULES.verify(rows, counters, expected)
        total += counters['total']
    actual_summaries = summary.get('shards', [])
    if (len(actual_summaries) != len(names) or {s['shard'] for s in actual_summaries} != names
            or summary.get('total') != total or total <= 0
            or any(s.get('passed') != s.get('total') or s.get('failed') != 0
                   or s.get('notExecuted') != 0 for s in actual_summaries)):
        raise ValueError('L2 summary differs from the actual passing TRX results.')
    return dict(profile=profile, methods=len(methods), cases=total,
                testAssemblySha256=plan['testAssemblySha256'])


def verify(commit, jobs, inputs):
    if not re.fullmatch(r'[0-9a-f]{40}', commit):
        raise ValueError('An exact L2 commit is required.')
    if set(jobs) != {'quality', 'targeted', 'rule_suite'} or any(v != 'success' for v in jobs.values()):
        raise ValueError('Every L2 child must execute and succeed: ' + repr(jobs))
    results = [verify_tests(Path(inputs) / name, profile, commit)
               for name, profile in [('targeted', 'focus'), ('rule_suite', 'check')]]
    if len({r['testAssemblySha256'] for r in results}) != 1:
        raise ValueError('L2 children ran different test assemblies.')
    return dict(status='L2_REQUIRED_PASSED', commit=commit, jobs=jobs, results=results,
                l3SqlNetworkRestartOrFullMatrixCertified=False)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--commit', required=True)
    for name in ('quality', 'targeted', 'rule-suite'):
        p.add_argument('--' + name, required=True)
    p.add_argument('--inputs', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    result = verify(a.commit, dict(quality=a.quality, targeted=a.targeted, rule_suite=a.rule_suite), a.inputs)
    a.output.parent.mkdir(parents=True, exist_ok=True)
    with a.output.open('x') as stream:
        json.dump(result, stream, indent=2)
        stream.write('\n')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
