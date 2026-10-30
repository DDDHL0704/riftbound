#!/usr/bin/env python3
"""Run a fixed-checkout CI command with the existing L2 resource sampler."""
import argparse
import importlib.util
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('observed_l2', ROOT / 'scripts/l2-distributed.py')
L2 = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(L2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--label', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ['--'] else args.command
    if not command:
        parser.error('A command after -- is required.')
    actual = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    if args.commit != actual:
        raise ValueError('Resource observation must use the fixed checked-out commit.')
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    # Resource observations do not infer case coverage or replace acceptance
    # receipts. Keep the original command, environment, and exit code.
    return L2.run_visible(command, output / 'command.log', output / 'execution-runtime.json',
                          dict(commit=actual, kind='CI_COMMAND', label=args.label))


if __name__ == '__main__':
    raise SystemExit(main())
