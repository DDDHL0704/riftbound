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
DOCUMENTATION = frozenset({
    'AGENTS.md', 'README.md', 'GLOSSARY.md',
    'docs/START_HERE.md', 'docs/PROJECT_GOVERNANCE.md',
    'docs/PROJECT_STATUS.md', 'docs/development-audit-status.md',
    'docs/REPOSITORY_AUDIT_2026-10-12.md',
    'docs/MOONFALL_AUDIT_2026-10-12.md',
    'docs/SIPHON_MINIMUM_MIGHT_AUDIT_2026-10-12.md',
    'docs/ORB_OF_REGRET_AUDIT_2026-10-12.md',
    'docs/AHRI_COMBAT_MIGHT_AUDIT_2026-10-12.md',
    'docs/NINE_TAILED_FOX_AUDIT_2026-10-12.md',
    'docs/LEONA_STUNNED_MIGHT_AUDIT_2026-10-12.md',
    'docs/POWERFUL_KEYWORD_LAYER_AUDIT_2026-10-12.md',
    'docs/RULE_TEST_WORKFLOW.md', 'docs/RULE_TEST_SPEED_2026-10-09.md',
    'docs/RULE_TEST_SPEED_2026-10-12.md',
    'docs/rules-authority-and-audit.md', 'docs/rules-evidence-index.md',
    'docs/COMPREHENSIVE_OFFICIAL_AUDIT_2026-10-11.md',
    'docs/agents/issue-tracker.md', 'docs/agents/domain.md',
    'docs/agents/triage-labels.md',
    'docs/CURRENT_COMPLETION_AUDIT.md', 'docs/CURRENT_A_MASTER_CHECKPOINT.md',
    'docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md',
    'docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md',
    'docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md',
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
        if event_name == 'workflow_dispatch':
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
        result['full'], result['reason'] = classify(changes)
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
    args = parser.parse_args()
    try:
        event = json.loads(args.event.read_text())
        if not isinstance(event, dict):
            raise ValueError('Invalid event envelope')
        result = decide(args.event_name, event, args.head, allow_fetch=True)
    except (OSError, ValueError):
        result = dict(full=True, reason='Unreadable event; require full verification',
                      headSha=args.head, baseSha=None, changes=[])
    result['selectorSha256'] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    result['ruleAndPackagingExecution'] = 'REQUIRED' if result['full'] else 'NOT_RUN_DOCUMENTATION_ONLY'
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2)+'\n')
    if args.github_output:
        with args.github_output.open('a') as stream:
            stream.write('full='+str(result['full']).lower()+'\n')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
