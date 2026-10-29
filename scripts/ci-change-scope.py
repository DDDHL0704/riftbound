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
# docs/legacy and DEPLOYMENT.md are read by tests and remain executable inputs.
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


def require_full(result, reason):
    result.update(tier='L3', full=True, profile='full', sql=True, network=True,
                  packaging=True, matches=[], reason=reason)
    return result


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


def scope_context(event_name, event, head):
    return dict(event=event_name, headSha=head,
                eventSha256=hashlib.sha256(json.dumps(event, sort_keys=True).encode()).hexdigest(),
                selectorSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                runId=os.environ.get('GITHUB_RUN_ID'), runAttempt=os.environ.get('GITHUB_RUN_ATTEMPT'))


def recheck(result, previous_path):
    """Only two matching Git inspections can exempt docs, before choosing jobs."""
    try:
        raw = previous_path.read_bytes()
        previous = json.loads(raw)
        result['priorScopeSha256'] = hashlib.sha256(raw).hexdigest()
        keys = ['event', 'eventSha256', 'headSha', 'baseSha', 'changes', 'selectorSha256',
                'runId', 'runAttempt', 'tier', 'full', 'profile', 'sql', 'network', 'packaging', 'matches']
        if (not isinstance(previous, dict) or any(k not in previous or k not in result
                                                 or previous[k] != result[k] for k in keys)):
            raise ValueError('Scope inspections disagree')
        return result
    except (OSError, ValueError, TypeError):
        return require_full(result, 'Scope recheck unavailable or inconsistent; require full verification')


def verify_docs_receipt(directory, checkout_path, event_name, event, head):
    """Verify the same-run exemption without repeating Git ancestry after jobs ran."""
    previous_path = directory / 'scope-candidate.json'
    previous = json.loads(previous_path.read_text())
    final = json.loads((directory / 'scope.json').read_text())
    prepared = json.loads((directory / 'checkout.json').read_text())
    checkout = json.loads(checkout_path.read_text())
    context = scope_context(event_name, event, head)
    if event_name == 'pull_request':
        base = event.get('pull_request', {}).get('base', {}).get('sha')
    elif event_name == 'push':
        base = event.get('before')
    else:
        raise ValueError('This event cannot exempt documentation')
    if (not SHA.fullmatch(base or '') or base == '0'*40
            or not SHA.fullmatch(head or '') or not str(context['runId']).isdigit()
            or not str(context['runAttempt']).isdigit()):
        raise ValueError('Missing fixed event or workflow identity')
    for decision in [previous, final]:
        if (not isinstance(decision, dict) or decision.get('baseSha') != base
                or any(decision.get(k) != v for k, v in context.items())
                or decision.get('ruleAndPackagingExecution') != 'NOT_RUN_DOCUMENTATION_ONLY'):
            raise ValueError('Documentation scope belongs to a different input')
        expected = tier_details(decision['changes'])
        if expected['tier'] != 'DOCS' or any(decision.get(k) != v for k, v in expected.items()):
            raise ValueError('Scope does not prove reviewed documentation only')
    if (previous['changes'] != final['changes']
            or final.get('priorScopeSha256') != hashlib.sha256(previous_path.read_bytes()).hexdigest()):
        raise ValueError('Documentation inspections are not linked')
    for receipt in [prepared, checkout]:
        if (receipt.get('status') != 'FIXED_CANDIDATE_CHECKOUT_VERIFIED'
                or any(receipt.get(k) != head for k in ['actualSha', 'expectedSha', 'eventSha'])
                or any(receipt.get(k) != context[k] for k in ['runId', 'runAttempt'])
                or not SHA.fullmatch(receipt.get('treeSha', ''))):
            raise ValueError('Checkout does not match the documentation input')
    if any(prepared.get(k) != checkout.get(k) for k in ['treeSha', 'sourceIndexSha256']):
        raise ValueError('Documentation checkout trees disagree')
    return dict(status='DOCUMENTATION_EXEMPTION_VERIFIED', **context, baseSha=base,
                ruleAndPackagingExecution='NOT_RUN_DOCUMENTATION_ONLY')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--event', type=Path, required=True)
    parser.add_argument('--event-name', default=os.environ.get('GITHUB_EVENT_NAME', ''))
    parser.add_argument('--head', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--github-output', type=Path)
    parser.add_argument('--docs-pr-only', action='store_true',
                        help='Main may replace pending code runs; only PRs may exempt documentation.')
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--require-docs', action='store_true',
                        help='Fail unless fresh Git inspection proves a documentation-only change.')
    mode.add_argument('--recheck', type=Path,
                      help='Reinspect Git before selecting jobs; uncertainty requires the full suite.')
    mode.add_argument('--verify-docs-receipt', type=Path,
                      help='Validate the preparation artifact without late Git scope selection.')
    parser.add_argument('--checkout', type=Path)
    args = parser.parse_args()
    try:
        event = json.loads(args.event.read_text())
        if not isinstance(event, dict):
            raise ValueError('Invalid event envelope')
        if args.verify_docs_receipt:
            result = verify_docs_receipt(args.verify_docs_receipt, args.checkout,
                                         args.event_name, event, args.head)
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(json.dumps(result, indent=2)+'\n')
            print(json.dumps(result))
            return
        result = decide(args.event_name, event, args.head, allow_fetch=True)
    except (OSError, ValueError, TypeError, AttributeError, KeyError) as error:
        if args.verify_docs_receipt:
            parser.exit(1, 'Documentation receipt rejected: '+str(error)+'\n')
        event = None
        result = dict(full=True, reason='Unreadable event; require full verification',
                      headSha=args.head, baseSha=None, changes=[])
    if 'tier' not in result:
        require_full(result, result['reason'])
    if args.docs_pr_only and args.event_name != 'pull_request':
        require_full(result, 'Main always requires complete verification, including replaced pending code runs')
    result.update(scope_context(args.event_name, event, args.head))
    if args.recheck:
        result = recheck(result, args.recheck)
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
