#!/usr/bin/env python3
"""Validate targeted coverage from the same candidate's original distributed TRX."""
import argparse
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
DOMAINS = ['payment', 'combat', 'triggers', 'recovery', 'recast']
SPEC = importlib.util.spec_from_file_location('rules_runner', ROOT / 'scripts/test-rules.py')
RULES = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RULES)


def command_for(classes):
    # Retained historical focus-plan/CLI regression; daily CI no longer executes this plan.
    tokens = sorted({token for domain in DOMAINS for token in RULES.DOMAINS[domain]} | set(classes))
    command = [sys.executable, str(ROOT / 'scripts/test-rules.py'), 'focus', '--l2-only']
    for token in tokens:
        command.extend(['--match', token])
    return command


def changed_classes(root, base, head):
    if not re.fullmatch(r'[0-9a-f]{40}', head):
        raise ValueError('An exact checkout commit is required.')
    if subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip() != head:
        raise ValueError('Targeted checkout differs from the required commit.')
    if not re.fullmatch(r'[0-9a-f]{40}', base or '') or base == '0' * 40:
        return []  # All adjacent domains still execute; check covers every ordinary method.
    try:
        paths = subprocess.check_output(['git', 'diff', '--name-only', '-z', base, head,
            '--', 'tests/Riftbound.ConformanceTests'], cwd=root).decode().split('\0')
    except subprocess.CalledProcessError:
        return []
    classes = set()
    for name in paths:
        path = Path(root) / name
        if not name.endswith('.cs') or not path.is_file():
            continue
        classes.update(c for c in re.findall(r'\bclass\s+([A-Za-z_][A-Za-z0-9_]*)', path.read_text())
                       if c != 'FullGameEndToEndTests')
    return sorted(classes)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--commit', required=True)
    p.add_argument('--base', default='')
    p.add_argument('--prepared', type=Path, required=True)
    p.add_argument('--inputs', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    spec = importlib.util.spec_from_file_location('targeted_original_trx', ROOT / 'scripts/l2-distributed.py')
    distributed = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(distributed)
    distributed.write(a.output / 'view.json', distributed.derived_view(a.prepared, a.inputs, a.commit))


if __name__ == '__main__':
    main()
