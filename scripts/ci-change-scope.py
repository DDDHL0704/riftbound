#!/usr/bin/env python3
"""Fail-closed CI scope selection for a fixed Git input (stdlib only)."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
# Fixture data and DEPLOYMENT.md are read by tests and remain executable inputs.
DOCUMENTATION = frozenset({
    'AGENTS.md', 'README.md', 'GLOSSARY.md',
    'docs/BACKLOG.md', 'docs/CARD_BACKLOG.md',
    'docs/agents/issue-tracker.md', 'docs/agents/domain.md',
    'docs/agents/triage-labels.md',
})
SHA = re.compile(r'[0-9a-fA-F]{40}')
RAW = re.compile(r':([0-7]{6}) ([0-7]{6}) [0-9a-f]+ [0-9a-f]+ ([A-Z])')


def parse_changes(raw):
    fields = raw.decode('utf-8').split('\0')
    if not fields or fields[-1] != '' or (len(fields)-1) % 2:
        raise ValueError('Unrecognized NUL-delimited Git diff')
    changes = []
    for i in range(0, len(fields)-1, 2):
        match = RAW.fullmatch(fields[i])
        if not match or not fields[i+1]:
            raise ValueError('Unrecognized raw Git change')
        old, new, status = match.groups()
        changes.append(dict(path=fields[i+1], oldMode=old, newMode=new, status=status))
    return changes


def classify(changes):
    if not changes:
        return True, 'Empty or unknown change set; require full verification'
    for change in changes:
        if (change['path'] not in DOCUMENTATION
                or change['oldMode'] not in {'000000', '100644'}
                or change['newMode'] not in {'000000', '100644'}
                or change['status'] not in {'A', 'D', 'M'}):
            return True, 'Executable input, unknown path or non-regular documentation'
    return False, 'Only explicitly reviewed documentation paths changed'


def tier_details(changes):
    """First conservative tier map. No self-declared L1 or unchecked card exemption."""
    full, reason = classify(changes)
    if not full:
        return dict(tier='DOCS', full=False, profile='none', sql=False, network=False, packaging=False,
                    matches=[], reason=reason)
    fallback = dict(tier='L3', full=True, profile='full', sql=True, network=True, packaging=True,
                    matches=[], reason='Unknown, broad or unsafe input requires complete verification')
    if not changes or any(c['oldMode'] not in {'000000', '100644'} or c['newMode'] != '100644'
                          or c['status'] not in {'A', 'M'} for c in changes):
        return fallback
    code = [c for c in changes if c['path'] not in DOCUMENTATION]
    # New independent test classes have no runtime impact. Existing tests or game
    # matrices may remove coverage and are never eligible for this L1 shortcut.
    test = re.compile(r'tests/Riftbound\.ConformanceTests/([A-Za-z_][A-Za-z0-9_]*Tests)\.cs')
    if code and all(c['status'] == 'A' and test.fullmatch(c['path']) and 'FullGame' not in c['path'] for c in code):
        return dict(tier='L1', full=False, profile='focus', sql=False, network=False, packaging=False,
                    matches=[test.fullmatch(c['path'])[1] for c in code],
                    reason='Only new independent conformance test classes; runtime unchanged')
    runtime = ('src/Riftbound.Engine/', 'src/Riftbound.Contracts/', 'src/Riftbound.Persistence/', 'src/Riftbound.Api/')
    if code and any(c['path'].startswith(runtime) for c in code) and all(
        c['path'].startswith(runtime) and c['path'].endswith('.cs')
        or test.fullmatch(c['path']) and 'FullGame' not in c['path'] for c in code):
        return dict(tier='L2', full=False, profile='check', sql=True, network=True, packaging=False,
                    matches=[], reason='Known backend mechanism paths: integration check plus all SQL/network gates')
    return fallback


def git(repo, *args):
    return subprocess.check_output(['git', *args], cwd=repo, stderr=subprocess.PIPE)


def decide(event_name, event, head, repo=ROOT, allow_fetch=False):
    result = dict(full=True, reason='Unknown event; require full verification',
                  event=event_name, headSha=head, baseSha=None, changes=[])
    try:
        if not isinstance(event, dict):
            raise ValueError('Invalid event envelope')
        if not SHA.fullmatch(head or '') or git(repo, 'rev-parse', 'HEAD').decode().strip() != head:
            raise ValueError('Checkout does not match the fixed head')
        if event_name in {'workflow_dispatch', 'schedule'}:
            result['reason'] = 'Manual integration gate always runs full'
            return result
        if event_name == 'push':
            base = event.get('before', '')
        elif event_name == 'pull_request':
            base = event.get('pull_request', {}).get('base', {}).get('sha', '')
        else:
            return result
        if not SHA.fullmatch(base or '') or base == '0'*40:
            raise ValueError('No usable base commit')
        result['baseSha'] = base
        try:
            git(repo, 'cat-file', '-e', base+'^{commit}')
        except subprocess.CalledProcessError:
            if not allow_fetch:
                raise
            subprocess.run(['git', 'fetch', '--no-tags', '--depth=1', 'origin', base],
                           cwd=repo, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                           check=True, timeout=30)
        # A force-push or incomplete ancestry is an uncertain input, not a docs exemption.
        git(repo, 'merge-base', '--is-ancestor', base, head)
        changes = parse_changes(git(repo, 'diff', '--raw', '--no-renames', '-z', base, head))
        result.update(changes=changes)
        result.update(tier_details(changes))
    except (OSError, ValueError, TypeError, AttributeError, subprocess.SubprocessError) as error:
        result['full'] = True
        result['reason'] = 'Cannot safely determine change scope: '+type(error).__name__
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--event', type=Path, required=True)
    parser.add_argument('--event-name', default=os.environ.get('GITHUB_EVENT_NAME', ''))
    parser.add_argument('--head', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--github-output', type=Path)
    parser.add_argument('--require-docs', action='store_true',
                        help='Fail unless fresh Git inspection proves a documentation-only change.')
    args = parser.parse_args()
    try:
        event = json.loads(args.event.read_text())
        if not isinstance(event, dict):
            raise ValueError('Invalid event envelope')
        result = decide(args.event_name, event, args.head, allow_fetch=True)
    except (OSError, ValueError):
        result = dict(full=True, reason='Unreadable event; require full verification',
                      headSha=args.head, baseSha=None, changes=[])
    if 'tier' not in result:
        result.update(tier='L3', profile='full', sql=True, network=True, packaging=True, matches=[])
    result['selectorSha256'] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    result['ruleAndPackagingExecution'] = 'COMPLETE_REQUIRED' if result['full'] else ('NOT_RUN_DOCUMENTATION_ONLY' if result['tier'] == 'DOCS' else 'SCOPED_ONLY_FULL_NOT_RUN')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2)+'\n')
    if args.github_output:
        with args.github_output.open('a') as stream:
            for key in ['full', 'sql', 'network', 'packaging']:
                stream.write(key+'='+str(result[key]).lower()+'\n')
            stream.write('tier='+result['tier']+'\n')
    print(json.dumps(result))
    if args.require_docs and result['tier'] != 'DOCS':
        parser.exit(1, 'Documentation exemption rejected: ' + result['reason'] + '\n')


if __name__ == '__main__':
    main()
