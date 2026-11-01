import copy
from contextlib import redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch


SCRIPT = Path(__file__).parents[1] / 'l2-queue.py'
spec = importlib.util.spec_from_file_location('l2_queue', SCRIPT)
Q = importlib.util.module_from_spec(spec)
spec.loader.exec_module(Q)


class L2QueueTests(unittest.TestCase):
    def setUp(self):
        self.repo = 'owner/project'
        self.repository = {'full_name': self.repo}
        self.workflow = 42
        self.run = dict(id=123, run_attempt=1, workflow_id=self.workflow, path=Q.WORKFLOW,
                        status='in_progress', event='pull_request', head_sha='a' * 40,
                        head_branch='a/fixture', repository=self.repository,
                        head_repository=self.repository, pull_requests=[{'number': 7}])
        self.pr = dict(number=7, state='open', head=dict(ref='a/fixture', sha='b' * 40, repo=self.repository),
                       base=dict(ref='main', repo=self.repository))
        self.calls = []

    def fetch(self, path, method='GET'):
        self.calls.append((path, method))
        if path.endswith('/pulls/7'):
            return copy.deepcopy(self.pr)
        if path.endswith('/branches/main'):
            return {'commit': {'sha': 'b' * 40}}
        if path.endswith('/actions/runs/123'):
            return copy.deepcopy(self.run)
        if path.endswith('/actions/runs/123/cancel'):
            self.assertEqual('POST', method)
            return None
        self.fail('Unexpected API call: ' + path)

    def assess(self, run=None):
        return Q.assess(self.run if run is None else run, self.repo, self.workflow, self.fetch)

    def test_only_superseded_or_closed_pr_is_obsolete(self):
        self.assertTrue(self.assess()['obsolete'])
        self.pr['head']['sha'] = self.run['head_sha']
        self.assertFalse(self.assess()['obsolete'])
        self.pr['state'] = 'closed'
        self.assertTrue(self.assess()['obsolete'])

    def test_main_push_compares_the_live_main_head(self):
        run = dict(self.run, event='push', head_branch='main')
        self.assertTrue(self.assess(run)['obsolete'])
        self.assertFalse(self.assess(dict(run, head_sha='b' * 40))['obsolete'])
        self.assertFalse(self.assess(dict(run, head_branch='a/fixture'))['obsolete'])

    def test_manual_other_workflow_finished_and_fork_runs_are_preserved(self):
        for changes in [dict(event='workflow_dispatch'), dict(event='schedule'), dict(workflow_id=99),
                        dict(path='.github/workflows/native-desktop.yml'), dict(status='completed'),
                        dict(repository={'full_name': 'elsewhere/project'}),
                        dict(head_repository={'full_name': 'fork/project'})]:
            with self.subTest(changes=changes):
                self.calls.clear()
                self.assertFalse(self.assess(dict(self.run, **changes))['obsolete'])
                self.assertEqual([], self.calls)

    def test_missing_ambiguous_or_malformed_pr_associations_never_cancel(self):
        for value in [None, [], [{'number': 7}, {'number': 8}], [None], [{'number': True}], [{'number': '7'}]]:
            with self.subTest(associated=value):
                self.assertFalse(self.assess(dict(self.run, pull_requests=value))['obsolete'])
        for changes in [dict(head_sha=''), dict(head_sha=None), dict(head_branch=None), dict(run_attempt=None),
                        dict(id=True), dict(head_repository={'full_name': None})]:
            with self.subTest(changes=changes):
                self.assertFalse(self.assess(dict(self.run, **changes))['obsolete'])

    def test_live_pr_identity_and_head_must_remain_unambiguous(self):
        original = copy.deepcopy(self.pr)
        changes = [dict(number=8), dict(state='unknown'), dict(head=None), dict(base={})]
        changes += [dict(head=dict(original['head'], **change)) for change in
                    [dict(ref='b/other'), dict(sha='invalid'), dict(repo={'full_name': 'fork/project'})]]
        changes += [dict(base=dict(original['base'], **change)) for change in
                    [dict(ref='release'), dict(repo={'full_name': 'elsewhere/project'})]]
        for change in changes:
            with self.subTest(change=change):
                self.pr = dict(original, **change)
                self.assertFalse(self.assess()['obsolete'])

    def test_cancellation_rechecks_live_run_and_pr_before_posting(self):
        result = Q.cancel_obsolete(self.run, self.repo, self.workflow, self.fetch)
        self.assertTrue(result['requested'])
        self.assertEqual([('repos/owner/project/actions/runs/123', 'GET'),
                          ('repos/owner/project/pulls/7', 'GET'),
                          ('repos/owner/project/actions/runs/123/cancel', 'POST')], self.calls)

    def test_completion_rerun_and_force_push_races_preserve_the_run(self):
        for changes in [dict(status='completed'), dict(run_attempt=2), dict(head_sha='c' * 40)]:
            with self.subTest(changes=changes):
                self.calls.clear()
                original = copy.deepcopy(self.run)
                self.run.update(changes)
                self.assertFalse(Q.cancel_obsolete(original, self.repo, self.workflow, self.fetch)['requested'])
                self.assertFalse(any(method == 'POST' for _, method in self.calls))
                self.run = original
        self.pr['head']['sha'] = self.run['head_sha']
        self.assertFalse(Q.cancel_obsolete(self.run, self.repo, self.workflow, self.fetch)['requested'])
        self.assertFalse(any(method == 'POST' for _, method in self.calls))

    def test_api_failure_cannot_post_a_cancellation(self):
        def broken(path, method='GET'):
            self.assertEqual('GET', method)
            raise RuntimeError('offline')
        with self.assertRaisesRegex(RuntimeError, 'offline'):
            Q.cancel_obsolete(self.run, self.repo, self.workflow, broken)

    def test_pagination_retains_all_jobs_and_rejects_incomplete_payloads(self):
        calls = []
        def pages(path):
            calls.append(path)
            return {'jobs': [{'id': i} for i in range(100)] if path.endswith('&page=1') else [{'id': 100}]}
        self.assertEqual(101, len(list(Q.items(pages, 'jobs?filter=latest', 'jobs'))))
        self.assertEqual(2, len(calls))
        self.assertIn('filter=latest&per_page=100&page=2', calls[-1])
        with self.assertRaisesRegex(ValueError, 'collection'):
            list(Q.items(lambda path: {'jobs': None}, 'jobs', 'jobs'))
        with self.assertRaisesRegex(ValueError, 'truncated'):
            list(Q.items(lambda path: {'jobs': [{}] * 100}, 'jobs', 'jobs'))

    def test_read_only_snapshot_counts_jobs_even_when_run_status_is_queued(self):
        def snapshot(path, method='GET'):
            self.assertEqual('GET', method)
            if path.endswith('/actions/workflows/backend-l2.yml'):
                return dict(id=self.workflow, path=Q.WORKFLOW)
            if '/actions/runs?status=' in path:
                return {'workflow_runs': [dict(self.run, status='queued')] if 'status=queued&' in path else []}
            if '/jobs?' in path:
                return {'jobs': [dict(status='in_progress')] * 8 + [dict(status='queued'), dict(status='completed')]}
            return self.fetch(path, method)
        report, candidates, workflow_id = Q.inspect(self.repo, snapshot)
        self.assertEqual({'in_progress': 8, 'queued': 1}, report['activeJobs'])
        self.assertEqual([123], [run['id'] for run in candidates])
        self.assertEqual(self.workflow, workflow_id)
        self.assertEqual([], report['cancellations'])
        self.assertFalse(any(method == 'POST' for _, method in self.calls))

    def test_cli_rejects_malformed_repository_before_querying_github(self):
        result = subprocess.run([sys.executable, str(SCRIPT), '--repo', 'owner/project/extra'],
                                capture_output=True, text=True)
        self.assertEqual(2, result.returncode)
        self.assertIn('OWNER/REPOSITORY', result.stderr)

    def test_cli_is_read_only_until_cancel_flag_is_explicit(self):
        for enabled in [False, True]:
            with self.subTest(cancel_flag=enabled):
                report = dict(runs=[], cancellations=[])
                output = io.StringIO()
                argv = [str(SCRIPT), '--repo', self.repo] + (['--cancel-obsolete'] if enabled else [])
                with patch.object(sys, 'argv', argv), redirect_stdout(output), \
                        patch.object(Q, 'inspect', return_value=(report, [self.run], self.workflow)), \
                        patch.object(Q, 'cancel_obsolete', return_value=dict(id=123, requested=True)) as cancel:
                    self.assertEqual(0, Q.main())
                self.assertEqual(int(enabled), cancel.call_count)
                self.assertEqual(int(enabled), len(json.loads(output.getvalue())['cancellations']))


if __name__ == '__main__':
    unittest.main()
