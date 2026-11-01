#!/usr/bin/env python3
"""Inspect repository job pressure and optionally cancel proven obsolete automatic L2 runs."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import json
import re
import subprocess


WORKFLOW = '.github/workflows/backend-l2.yml'
ACTIVE = ('requested', 'queued', 'pending', 'in_progress', 'waiting')
SHA = re.compile(r'[0-9a-f]{40}')


def gh(path, method='GET'):
    result = subprocess.run(['gh', 'api', '--method', method, path],
                            capture_output=True, text=True, check=True, timeout=30)
    return json.loads(result.stdout) if result.stdout.strip() else None


def items(fetch, path, key):
    for page in range(1, 11):
        separator = '&' if '?' in path else '?'
        payload = fetch(path + separator + 'per_page=100&page=' + str(page))
        rows = payload.get(key) if isinstance(payload, dict) else None
        if not isinstance(rows, list) or any(not isinstance(row, dict) for row in rows):
            raise ValueError('Invalid GitHub collection: ' + key)
        yield from rows
        if len(rows) < 100:
            return
    raise ValueError('GitHub collection may be truncated: ' + key)


def same_repo(value, repository):
    name = value.get('full_name') if isinstance(value, dict) else None
    return isinstance(name, str) and name.lower() == repository.lower()


def identity(run):
    return tuple(run.get(key) for key in ('id', 'run_attempt', 'head_sha', 'event', 'workflow_id', 'head_branch'))


def assess(run, repository, workflow_id, fetch):
    """Uncertainty preserves a run; only a live, unambiguous revision comparison can retire it."""
    keep = lambda reason: dict(obsolete=False, reason=reason)
    if run.get('status') not in ACTIVE:
        return keep('Run has finished')
    if any(type(run.get(key)) is not int or run[key] <= 0 for key in ('id', 'run_attempt')):
        return keep('Run identity or attempt is unavailable')
    if (run.get('workflow_id') != workflow_id or run.get('path') != WORKFLOW
            or not same_repo(run.get('repository'), repository)
            or not same_repo(run.get('head_repository'), repository)):
        return keep('Run is outside the verified repository L2 workflow')
    if (not isinstance(run.get('head_sha'), str) or not SHA.fullmatch(run['head_sha'])
            or not isinstance(run.get('head_branch'), str) or not run['head_branch']):
        return keep('Run head or branch is unavailable')
    api = 'repos/' + repository
    if run.get('event') == 'pull_request':
        associated = run.get('pull_requests')
        if not isinstance(associated, list) or len(associated) != 1 or not isinstance(associated[0], dict):
            return keep('Run does not identify exactly one pull request')
        number = associated[0].get('number')
        if type(number) is not int or number <= 0:
            return keep('Pull request number is unavailable')
        pr = fetch(api + '/pulls/' + str(number))
        if not isinstance(pr, dict):
            return keep('Pull request identity is unavailable')
        head, base = pr.get('head'), pr.get('base')
        if (pr.get('number') != number or pr.get('state') not in ('open', 'closed')
                or not isinstance(head, dict) or not isinstance(base, dict)
                or base.get('ref') != 'main' or head.get('ref') != run.get('head_branch')
                or not same_repo(base.get('repo'), repository) or not same_repo(head.get('repo'), repository)
                or not isinstance(head.get('sha'), str) or not SHA.fullmatch(head['sha'])):
            return keep('Pull request identity or live head is unavailable')
        if pr['state'] == 'closed':
            return dict(obsolete=True, reason='Pull request #' + str(number) + ' is closed')
        if head['sha'] != run['head_sha']:
            return dict(obsolete=True, reason='Pull request #' + str(number) + ' head changed')
        return keep('Current pull request head')
    if run.get('event') == 'push' and run.get('head_branch') == 'main':
        branch = fetch(api + '/branches/main')
        commit = branch.get('commit') if isinstance(branch, dict) else None
        head = commit.get('sha') if isinstance(commit, dict) else None
        if not isinstance(head, str) or not SHA.fullmatch(head):
            return keep('Live main head is unavailable')
        if head != run['head_sha']:
            return dict(obsolete=True, reason='Main head changed')
        return keep('Current main head')
    return keep('Preserve manual runs and other events')


def inspect(repository, fetch=gh):
    api = 'repos/' + repository
    workflow = fetch(api + '/actions/workflows/backend-l2.yml')
    if (not isinstance(workflow, dict) or workflow.get('path') != WORKFLOW
            or type(workflow.get('id')) is not int or workflow['id'] <= 0):
        raise ValueError('Cannot identify Backend L2 workflow')
    runs = {}
    for status in ACTIVE:
        for run in items(fetch, api + '/actions/runs?status=' + status, 'workflow_runs'):
            if type(run.get('id')) is not int or run['id'] <= 0:
                raise ValueError('Run ID is unavailable')
            runs[run['id']] = run
    rows, candidates, job_counts = [], [], Counter()
    for run in sorted(runs.values(), key=lambda value: value['id']):
        if run.get('status') not in ACTIVE:
            continue
        counts = Counter(job.get('status', 'unknown') for job in items(
            fetch, api + '/actions/runs/' + str(run['id']) + '/jobs?filter=latest', 'jobs'))
        counts.pop('completed', None)
        job_counts.update(counts)
        verdict = assess(run, repository, workflow['id'], fetch)
        rows.append(dict(id=run['id'], workflow=run.get('name'), event=run.get('event'),
                         branch=run.get('head_branch'), headSha=run.get('head_sha'),
                         status=run.get('status'), jobs=dict(counts), **verdict))
        if verdict['obsolete']:
            candidates.append(run)
    report = dict(repository=repository, observedAt=datetime.now(timezone.utc).isoformat(),
                  scope='Current job snapshot for this repository',
                  activeJobs=dict(job_counts), runs=rows, cancellations=[])
    return report, candidates, workflow['id']


def cancel_obsolete(run, repository, workflow_id, fetch=gh):
    api = 'repos/' + repository + '/actions/runs/' + str(run['id'])
    fresh = fetch(api)
    if not isinstance(fresh, dict) or identity(fresh) != identity(run):
        return dict(id=run['id'], requested=False, reason='Run identity or attempt changed')
    verdict = assess(fresh, repository, workflow_id, fetch)
    if not verdict['obsolete']:
        return dict(id=run['id'], requested=False, reason=verdict['reason'])
    fetch(api + '/cancel', method='POST')
    return dict(id=run['id'], requested=True, reason=verdict['reason'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', required=True, help='GitHub OWNER/REPOSITORY')
    parser.add_argument('--cancel-obsolete', action='store_true',
                        help='Recheck and cancel only superseded or closed automatic L2 runs')
    args = parser.parse_args()
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', args.repo):
        parser.error('--repo must be OWNER/REPOSITORY')
    report, candidates, workflow_id = inspect(args.repo)
    if args.cancel_obsolete:
        for run in candidates:
            try:
                report['cancellations'].append(cancel_obsolete(run, args.repo, workflow_id))
            except (OSError, ValueError, subprocess.SubprocessError) as error:
                report['cancellations'].append(dict(id=run['id'], requested=False, error=str(error)))
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return int(any('error' in result for result in report['cancellations']))


if __name__ == '__main__':
    raise SystemExit(main())
