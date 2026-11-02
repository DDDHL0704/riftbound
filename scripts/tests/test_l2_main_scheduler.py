import copy
from datetime import datetime, timedelta, timezone
import importlib.util
from pathlib import Path
import unittest
from urllib.parse import parse_qs, urlsplit


ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('main_scheduler', ROOT / 'scripts/main-l2-scheduler.py')
scheduler = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scheduler)
NOW = datetime(2026, 11, 2, 15, tzinfo=timezone.utc)
REPO = 'DDDHL0704/riftbound'
HEAD = 'a' * 40


def run(number=10, **changes):
    value = dict(id=number, workflow_id=123, path=scheduler.WORKFLOW, run_attempt=1,
                 repository=dict(full_name=REPO), head_sha=HEAD, head_branch='main',
                 event='workflow_dispatch', display_title=scheduler.AUTOMATIC_TITLE,
                 status='queued', conclusion=None, created_at=NOW.isoformat())
    value.update(changes)
    return value


class Api:
    def __init__(self, active=(), recent=(), passed=()):
        self.active = list(active)
        self.recent = list(recent)
        self.passed = list(passed)
        self.details = {item['id']: copy.deepcopy(item) for item in self.active}
        self.calls = []
        self.arriving = []
        self.fail_status = None

    def __call__(self, path, method='GET', body=None):
        self.calls.append((path, method, body))
        if method != 'GET':
            return None
        target, query = urlsplit(path).path, parse_qs(urlsplit(path).query)
        if target.endswith('/actions/workflows/backend-l2.yml'):
            return dict(id=123, path=scheduler.WORKFLOW)
        if target.endswith('/branches/main'):
            return dict(commit=dict(sha=HEAD))
        if target.endswith('/runs'):
            status = query.get('status', [None])[0]
            if status == self.fail_status and self.fail_status is not None:
                raise ValueError('API unavailable')
            if 'created' in query:
                rows = self.recent
            elif 'head_sha' in query:
                rows = self.passed
            elif query.get('event') == ['pull_request']:
                rows = [item for item in self.arriving if item['status'] == status]
            else:
                rows = [item for item in self.active if item['status'] == status]
            return dict(workflow_runs=copy.deepcopy(rows))
        number = int(target.rsplit('/', 1)[1])
        detail = self.details[number]
        if isinstance(detail, list):
            return copy.deepcopy(detail.pop(0))
        return copy.deepcopy(detail)

    def mutations(self):
        return [(path, method, body) for path, method, body in self.calls if method != 'GET']


class MainL2SchedulerTests(unittest.TestCase):
    def decide(self, api, event_name='schedule', event=None, apply=True):
        return scheduler.decide(REPO, event_name, event or {}, now=NOW, fetch=api, apply=apply)

    def test_idle_hour_dispatches_latest_main_with_explicit_automatic_identity(self):
        api = Api()
        result = self.decide(api)
        self.assertEqual('MAIN_L2_DISPATCHED', result['status'])
        self.assertEqual(HEAD, result['headSha'])
        self.assertEqual([(f'repos/{REPO}/actions/workflows/backend-l2.yml/dispatches', 'POST',
                           dict(ref='main', inputs=dict(scheduled_main=True)))], api.mutations())

    def test_dry_run_never_cancels_or_dispatches(self):
        api = Api()
        self.assertEqual('MAIN_L2_WOULD_DISPATCH', self.decide(api, apply=False)['status'])
        self.assertEqual([], api.mutations())
        api = Api([run(), run(11, event='pull_request', head_branch='c/rules')])
        result = self.decide(api, apply=False)
        self.assertEqual([dict(id=10, requested=False)], result['cancellations'])
        self.assertEqual([], api.mutations())

    def test_every_active_pr_state_blocks_main_and_only_automatic_main_is_cancelled(self):
        for status in scheduler.ACTIVE:
            with self.subTest(status=status):
                api = Api([run(), run(11, event='push'),
                           run(12, display_title='Manual investigation'),
                           run(13, event='pull_request', head_branch='c/rules', status=status)])
                result = self.decide(api)
                self.assertEqual('MAIN_L2_DEFERRED', result['status'])
                self.assertEqual([10, 11], [item['id'] for item in result['cancellations']])
                self.assertEqual([f'repos/{REPO}/actions/runs/{i}/cancel' for i in [10, 11]],
                                 [row[0] for row in api.mutations()])

    def test_a_running_main_or_manual_investigation_is_not_restarted(self):
        for changes in [dict(status='in_progress'), dict(display_title='Manual L2')]:
            api = Api([run(**changes)])
            self.assertIn('already active', self.decide(api)['reason'])
            self.assertEqual([], api.mutations())

    def test_failed_or_cancelled_automatic_runs_still_enforce_the_hourly_limit(self):
        for conclusion in ['success', 'failure', 'cancelled']:
            api = Api(recent=[run(status='completed', conclusion=conclusion,
                                 created_at=(NOW-timedelta(minutes=59)).isoformat())])
            self.assertIn('last hour', self.decide(api)['reason'])
            self.assertEqual([], api.mutations())
        api = Api(recent=[run(status='completed', conclusion='cancelled',
                             created_at=(NOW-timedelta(hours=1)).isoformat())])
        self.assertEqual('MAIN_L2_DISPATCHED', self.decide(api)['status'])

    def test_only_a_full_dispatched_success_for_the_same_main_commit_is_reused(self):
        api = Api(passed=[run(status='completed', conclusion='success')])
        self.assertIn('already passed', self.decide(api)['reason'])
        self.assertEqual([], api.mutations())
        for changes in [dict(head_sha='b'*40), dict(conclusion='failure'),
                        dict(event='pull_request'), dict(event='push')]:
            sample = run(status='completed', conclusion='success')
            sample.update(changes)
            api = Api(passed=[sample])
            self.assertEqual('MAIN_L2_DISPATCHED', self.decide(api)['status'])

    def test_a_pr_arriving_during_admission_blocks_dispatch(self):
        api = Api()
        api.arriving = [run(event='pull_request', status='queued')]
        self.assertIn('arrived', self.decide(api)['reason'])
        self.assertEqual([], api.mutations())

    def test_requested_pr_can_preempt_main_before_it_appears_in_listings(self):
        api = Api([run()])
        api.details[20] = run(20, event='pull_request', head_branch='b/card')
        result = self.decide(api, 'workflow_run', dict(action='requested', workflow_run=dict(id=20)))
        self.assertEqual([dict(id=10, requested=True)], result['cancellations'])
        self.assertEqual(1, len(api.mutations()))

    def test_reruns_preempt_main_without_a_duplicate_job_for_the_initial_attempt(self):
        event = dict(action='in_progress', workflow_run=dict(id=20))
        for attempt in [1, 2]:
            api = Api([run()])
            api.details[20] = run(20, event='pull_request', run_attempt=attempt)
            result = self.decide(api, 'workflow_run', event)
            self.assertEqual(attempt > 1, bool(result['cancellations']))
            self.assertEqual(attempt > 1, bool(api.mutations()))

    def test_changed_main_attempt_or_finished_requesting_pr_is_preserved(self):
        api = Api([run(), run(20, event='pull_request', head_branch='b/card')])
        api.details[10]['run_attempt'] = 2
        self.assertEqual([], self.decide(api)['cancellations'])
        self.assertEqual([], api.mutations())
        api = Api([run()])
        pr = run(20, event='pull_request', head_branch='b/card')
        api.details[20] = [pr, dict(pr, status='completed')]
        result = self.decide(api, 'workflow_run', dict(action='requested', workflow_run=dict(id=20)))
        self.assertEqual([], result['cancellations'])
        self.assertEqual([], api.mutations())
        api = Api([run(), pr])
        api.details[20]['status'] = 'completed'
        self.assertEqual([], self.decide(api)['cancellations'])
        self.assertEqual([], api.mutations())

    def test_a_second_active_pr_still_takes_priority_when_the_first_has_finished(self):
        api = Api([run(), run(20, event='pull_request'), run(21, event='pull_request')])
        api.details[20]['status'] = 'completed'
        self.assertEqual([dict(id=10, requested=True)], self.decide(api)['cancellations'])

    def test_listing_reads_later_pages_and_refuses_truncated_or_malformed_results(self):
        calls = []
        def paginated(path):
            calls.append(path)
            page = int(parse_qs(urlsplit(path).query)['page'][0])
            return dict(workflow_runs=[run(i) for i in range(1, 101)] if page == 1 else [run(101)])
        self.assertEqual(101, len(list(scheduler.runs(paginated, 'repos/x/actions/workflows/1'))))
        self.assertEqual(2, len(calls))
        with self.assertRaisesRegex(ValueError, 'truncated'):
            list(scheduler.runs(lambda _: dict(workflow_runs=[run()]*100), 'repos/x/actions/workflows/1'))
        for payload in [None, {}, dict(workflow_runs=[None])]:
            with self.assertRaises(ValueError):
                list(scheduler.runs(lambda _: payload, 'repos/x/actions/workflows/1'))

    def test_ambiguous_recent_times_never_allow_a_dispatch(self):
        for value in [None, 'not-a-time', '2026-11-02T14:30:00']:
            api = Api(recent=[run(status='completed', created_at=value)])
            with self.assertRaises(ValueError):
                self.decide(api)
            self.assertEqual([], api.mutations())

    def test_unavailable_or_foreign_metadata_never_starts_or_cancels_work(self):
        for changes in [dict(repository=dict(full_name='other/repo')), dict(workflow_id=99),
                        dict(path='.github/workflows/native-desktop.yml'), dict(run_attempt=0)]:
            api = Api([run(**changes)])
            with self.assertRaises(ValueError):
                self.decide(api)
            self.assertEqual([], api.mutations())
        api = Api()
        api.fail_status = 'waiting'
        with self.assertRaises(ValueError):
            self.decide(api)
        self.assertEqual([], api.mutations())

    def test_invalid_or_completed_workflow_requests_cannot_cancel_main(self):
        for changes in [dict(event='workflow_dispatch'), dict(status='completed')]:
            api = Api([run()])
            api.details[20] = run(20, **changes)
            event = dict(action='requested', workflow_run=dict(id=20))
            if changes.get('event') == 'workflow_dispatch':
                with self.assertRaises(ValueError):
                    self.decide(api, 'workflow_run', event)
            else:
                api.details[20]['event'] = 'pull_request'
                self.assertEqual([], self.decide(api, 'workflow_run', event)['cancellations'])
            self.assertEqual([], api.mutations())

    def test_workflow_keeps_pr_gate_and_trusted_scheduler_separate(self):
        backend = (ROOT / '.github/workflows/backend-l2.yml').read_text()
        control = (ROOT / '.github/workflows/backend-l2-main.yml').read_text()
        triggers = backend.split('permissions:', 1)[0]
        self.assertNotIn('push:', triggers)
        self.assertIn('pull_request:', triggers)
        self.assertIn('scheduled_main:', triggers)
        self.assertIn("'Scheduled main L2'", triggers)
        self.assertIn("cron: '17 * * * *'", control)
        self.assertIn('types: [requested, in_progress]', control)
        self.assertIn('github.event.workflow_run.run_attempt > 1', control)
        self.assertIn("github.event.workflow_run.event == 'pull_request'", control)
        self.assertIn('actions: write', control)
        self.assertIn('ref: ${{ github.sha }}', control)
        self.assertNotIn('head_sha', control)
        self.assertNotIn('L2 required', control)
        self.assertIn('      group: main-l2-scheduler', control)
        self.assertNotIn('\nconcurrency:', control)


if __name__ == '__main__':
    unittest.main()
