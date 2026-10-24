#!/usr/bin/env python3
"""Run changed conformance classes plus adjacent domains, without external services."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
DOMAINS = ['payment', 'combat', 'triggers', 'recovery', 'recast']


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
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    classes = changed_classes(ROOT, a.base, a.commit)
    command = [sys.executable, str(ROOT / 'scripts/test-rules.py'), 'focus', *DOMAINS]
    for name in classes:
        command.extend(['--match', name])
    subprocess.run(command + ['--jobs', '2', '--output', str(a.output)], cwd=ROOT, check=True)
    (a.output / 'selection.json').write_text(json.dumps(dict(commit=a.commit, base=a.base,
        domains=DOMAINS, changedClasses=classes, fullMatrixExecuted=False), indent=2) + '\n')


if __name__ == '__main__':
    main()
