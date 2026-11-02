#!/usr/bin/env python3
"""Admit one idle main L2 per hour and yield automatic main runs to PRs."""
import argparse
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import re
import subprocess
from urllib.parse import urlencode


WORKFLOW = '.github/workflows/backend-l2.yml'
AUTOMATIC_TITLE = 'Scheduled main L2'
ACTIVE = ('requested', 'queued', 'pending', 'in_progress', 'waiting')
SHA = re.compile(r'[0-9a-f]{40}')


def gh(path, method='GET', body=None):
    command = ['gh', 'api', '--method', method, path]
    if body is not None:
        command += ['--input', '-']
    result = subprocess.run(command, input=json.dumps(body) if body is not None else None,
                            capture_output=True, text=True, check=True, timeout=30)
    return json.loads(result.stdout) if result.stdout.strip() else None


def timestamp(value):
    if not isinstance(value, str):
        raise ValueError('Run creation time is unavailable')
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    if parsed.tzinfo is None:
        raise ValueError('Run creation time has no time zone')
    return parsed


def same_run(run, repository, workflow_id):
    repo = run.get('repository') if isinstance(run, dict) else None
    return (isinstance(run, dict) and isinstance(repo, dict)
            and isinstance(repo.get('full_name'), str)
            and repo.get('full_name', '').lower() == repository.lower()
            and run.get('workflow_id') == workflow_id and run.get('path') == WORKFLOW
            and type(run.get('id')) is int and run['id'] > 0
            and type(run.get('run_attempt')) is int and run['run_attempt'] > 0
            and isinstance(run.get('head_sha'), str) and SHA.fullmatch(run['head_sha']))


def automatic_main(run):
    return (run.get('head_branch') == 'main'
            and (run.get('event') == 'push' or
                 (run.get('event') == 'workflow_dispatch'
                  and run.get('display_title') == AUTOMATIC_TITLE)))


def identity(run):
    return tuple(run.get(key) for key in ('id', 'run_attempt', 'head_sha', 'workflow_id',
                                        'path', 'event', 'head_branch', 'display_title'))


def runs(fetch, api, **filters):
    for page in range(1, 11):
        query = urlencode(dict(filters, per_page=100, page=page))
        payload = fetch(api + '/runs?' + query)
        rows = payload.get('workflow_runs') if isinstance(payload, dict) else None
        if not isinstance(rows, list) or any(not isinstance(row, dict) for row in rows):
            raise ValueError('Cannot enumerate complete L2 runs')
        yield from rows
        if len(rows) < 100:
            return
    raise ValueError('L2 run listing is truncated')


def decide(repository, event_name, event, *, apply=False, fetch=gh, now=None):
    """Unknown API state never starts expensive work; manual and PR runs are preserved."""
    now = now or datetime.now(timezone.utc)
    report = dict(status='MAIN_L2_DEFERRED', repository=repository,
                  observedAt=now.isoformat(), apply=apply, cancellations=[])
    api = 'repos/' + repository
    workflow_api = api + '/actions/workflows/backend-l2.yml'
    workflow = fetch(workflow_api)
    if (not isinstance(workflow, dict) or workflow.get('path') != WORKFLOW
            or type(workflow.get('id')) is not int or workflow['id'] <= 0):
        raise ValueError('Cannot identify Backend L2 workflow')
    workflow_id = workflow['id']
    trigger = None
    if event_name == 'workflow_run':
        requested = event.get('workflow_run') if isinstance(event, dict) else None
        if (not isinstance(requested, dict) or type(requested.get('id')) is not int
                or event.get('action') not in ('requested', 'in_progress')):
            raise ValueError('Invalid workflow request')
        trigger = fetch(api + '/actions/runs/' + str(requested['id']))
        if not same_run(trigger, repository, workflow_id) or trigger.get('event') != 'pull_request':
            raise ValueError('Request is not a PR Backend L2')
        if trigger.get('status') not in ACTIVE:
            return dict(report, reason='Requesting PR verification has already finished')
        if event['action'] == 'in_progress' and trigger['run_attempt'] == 1:
            return dict(report, reason='Initial attempt was handled by the requested event')
    elif event_name not in ('schedule', 'workflow_dispatch'):
        raise ValueError('Unsupported scheduler event')

    active = {}
    for status in ACTIVE:
        for run in runs(fetch, workflow_api, status=status):
            if not same_run(run, repository, workflow_id):
                raise ValueError('Active run identity is unavailable')
            if run.get('status') in ACTIVE:
                active[run['id']] = run
    prs = [run for run in active.values() if run.get('event') == 'pull_request']
    if trigger is not None or prs:
        for run in active.values():
            if not automatic_main(run):
                continue
            fresh = fetch(api + '/actions/runs/' + str(run['id']))
            if (not same_run(fresh, repository, workflow_id) or identity(fresh) != identity(run)
                    or fresh.get('status') not in ACTIVE or not automatic_main(fresh)):
                continue
            # A completed PR must not cancel work using an obsolete event/list snapshot.
            requesters = [trigger] if trigger is not None else prs
            priority_active = False
            for requester in requesters:
                current = fetch(api + '/actions/runs/' + str(requester['id']))
                if (not same_run(current, repository, workflow_id)
                        or identity(current) != identity(requester) or current.get('status') not in ACTIVE):
                    continue
                priority_active = True
                break
            if not priority_active:
                break
            if apply:
                fetch(api + '/actions/runs/' + str(run['id']) + '/cancel', method='POST')
            report['cancellations'].append(dict(id=run['id'], requested=apply))
        return dict(report, reason='PR L2 takes priority', activePullRequests=len(prs))
    if any(run.get('head_branch') == 'main' for run in active.values()):
        return dict(report, reason='A main L2 is already active')

    cutoff = now - timedelta(hours=1)
    recent = list(runs(fetch, workflow_api, branch='main', created='>=' + cutoff.isoformat()))
    for run in recent:
        if not same_run(run, repository, workflow_id):
            raise ValueError('Recent main run identity is unavailable')
        if automatic_main(run) and timestamp(run.get('created_at')) > cutoff:
            return dict(report, reason='An automatic main L2 started within the last hour')
    branch = fetch(api + '/branches/main')
    head = branch.get('commit', {}).get('sha') if isinstance(branch, dict) else None
    if not isinstance(head, str) or not SHA.fullmatch(head):
        raise ValueError('Cannot identify the latest main commit')
    for run in runs(fetch, workflow_api, branch='main', head_sha=head, status='success'):
        if not same_run(run, repository, workflow_id):
            raise ValueError('Successful main run identity is unavailable')
        if (run.get('head_branch') == 'main' and run['head_sha'] == head
                and run.get('event') == 'workflow_dispatch'
                and run.get('status') == 'completed' and run.get('conclusion') == 'success'):
            return dict(report, reason='This main commit already passed a full dispatched L2', headSha=head)
    # Recheck PR admission just before dispatch; workflow_run also cancels a main
    # run if a PR arrives after this final snapshot.
    for status in ACTIVE:
        if list(runs(fetch, workflow_api, event='pull_request', status=status)):
            return dict(report, reason='A PR L2 arrived during admission')
    if apply:
        fetch(workflow_api + '/dispatches', method='POST',
              body=dict(ref='main', inputs=dict(scheduled_main=True)))
    return dict(report, status='MAIN_L2_DISPATCHED' if apply else 'MAIN_L2_WOULD_DISPATCH',
                reason='PR verification is idle and main is due', headSha=head)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', default=os.environ.get('GITHUB_REPOSITORY'))
    parser.add_argument('--event', type=Path, required=True)
    parser.add_argument('--event-name', default=os.environ.get('GITHUB_EVENT_NAME'), required=False)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', args.repo or ''):
        parser.error('--repo must be OWNER/REPOSITORY')
    try:
        report = decide(args.repo, args.event_name, json.loads(args.event.read_text()), apply=args.apply)
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        report = dict(status='MAIN_L2_DEFERRED', reason=str(error), apply=args.apply)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
