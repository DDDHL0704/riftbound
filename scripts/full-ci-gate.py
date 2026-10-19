#!/usr/bin/env python3
"""Keep the existing required rules check dependent on every full CI job."""
import argparse
import json
from pathlib import Path
import re


def full_gate(commit, results):
    if not re.fullmatch(r'[0-9a-f]{40}', commit):
        raise ValueError('A fixed full commit SHA is required.')
    required = {'scope', 'rule_suite', 'postgres'}
    if set(results) != required or any(results[name] != 'success' for name in required):
        raise ValueError('Full CI requires successful scope, complete rule suite, and real PostgreSQL/network jobs: ' + repr(results))
    return {'status': 'FIXED_COMMIT_FULL_CHILD_JOBS_ALL_SUCCEEDED', 'commit': commit, 'jobs': results}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--scope', required=True)
    parser.add_argument('--rule-suite', required=True)
    parser.add_argument('--postgres', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = full_gate(args.commit, {'scope': args.scope, 'rule_suite': args.rule_suite, 'postgres': args.postgres})
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result), flush=True)


if __name__ == '__main__':
    main()
