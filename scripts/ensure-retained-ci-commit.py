#!/usr/bin/env python3
"""Make historical audit inputs available; governance still authenticates every pin."""
import json
from pathlib import Path
import re
import subprocess
import sys


def fetch(root, objects, shallow=False):
    command = ['git', 'fetch', '--no-tags', '--no-write-fetch-head',
               '--filter=blob:none', '--stdin']
    if shallow:
        command.append('--depth=1')
    command.append('origin')
    for attempt in range(3):
        try:
            subprocess.run(command, cwd=root, input='\n'.join(objects)+'\n', text=True,
                           check=True, timeout=120)
            return
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            print(f'Retained input fetch attempt {attempt+1}/3 failed', file=sys.stderr)
            if attempt == 2:
                raise


def referenced_paths(value):
    if isinstance(value, dict):
        if isinstance(value.get('path'), str):
            yield value['path']
        for child in value.values():
            yield from referenced_paths(child)
    elif isinstance(value, list):
        for child in value:
            yield from referenced_paths(child)


def ensure(root):
    baseline = root/'docs/audit/legacy-source-baseline.json'
    if not baseline.exists():
        return
    if baseline.is_symlink():
        raise ValueError('Retained baseline must be a regular configuration')
    commit = json.loads(baseline.read_text())['commit']
    if not isinstance(commit, str) or not re.fullmatch(r'[0-9a-f]{40}', commit):
        raise ValueError('Retained baseline requires a full commit SHA')
    if subprocess.run(['git', 'cat-file', '-e', commit+'^{commit}'], cwd=root,
                      stderr=subprocess.DEVNULL).returncode == 0:
        return

    # Only the ephemeral CI checkout is configured; historical evidence is immutable.
    subprocess.run(['git', 'config', 'remote.origin.promisor', 'true'], cwd=root, check=True)
    subprocess.run(['git', 'config', 'remote.origin.partialclonefilter', 'blob:none'],
                   cwd=root, check=True)
    fetch(root, [commit], shallow=True)
    subprocess.run(['git', 'cat-file', '-e', commit+'^{commit}'], cwd=root, check=True)
    entries = subprocess.check_output(['git', 'ls-tree', '-rz', commit], cwd=root)
    blobs = {}
    for entry in entries.split(b'\0'):
        if entry:
            metadata, path = entry.split(b'\t', 1)
            _, kind, oid = metadata.decode().split()
            if kind == 'blob':
                blobs[path.decode()] = oid
    manifest_path = 'docs/audit/current-manifest.json'
    fetch(root, [blobs[manifest_path]])
    manifest = json.loads(subprocess.check_output(
        ['git', 'show', commit+':'+manifest_path], cwd=root))
    # Batch the referenced blobs to avoid one lazy network fetch for each audit path.
    # Absent paths still fail in the unchanged downstream governance validation.
    objects = sorted({blobs[path] for path in referenced_paths(manifest) if path in blobs})
    if objects:
        fetch(root, objects)
    print(f'Retained audit commit {commit}: requested {len(objects)} referenced blobs')


if __name__ == '__main__':
    ensure(Path(__file__).resolve().parents[1])
