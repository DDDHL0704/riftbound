import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest


spec = importlib.util.spec_from_file_location(
    'main_l2_reuse', Path(__file__).parents[1] / 'main-l2-reuse.py')
reuse = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reuse)


class MainL2ReuseTests(unittest.TestCase):
    def setUp(self):
        self.head = 'a' * 40
        self.run_head = 'b' * 40
        self.tree = 'c' * 40
        self.event = {
            'before': 'd' * 40,
            'after': self.head,
            'ref': 'refs/heads/main',
        }

    def fetch(self, url, token):
        self.assertEqual('token', token)
        if url.endswith('/commits/' + self.head + '/pulls'):
            return [{'number': 330}]
        if url.endswith('/pulls/330'):
            return {
                'merged_at': '2026-10-31T04:11:49Z',
                'merge_commit_sha': self.head,
                'base': {'ref': 'main'},
                'head': {'ref': 'a/l2-ven-opening-batch'},
            }
        if '/actions/runs?' in url:
            return {'workflow_runs': [{
                'id': 41165082786,
                'run_attempt': 1,
                'name': 'Backend L2',
                'event': 'pull_request',
                'status': 'completed',
                'conclusion': 'success',
                'head_sha': self.run_head,
                'pull_requests': [{'number': 330}],
                'html_url': 'https://example.invalid/runs/41165082786',
                'updated_at': '2026-10-31T04:10:32Z',
            }]}
        if url.endswith('/commits/' + self.run_head):
            return {'commit': {'tree': {'sha': self.tree}}}
        self.fail('unexpected API URL: ' + url)

    def test_matching_passed_pr_l2_and_tree_is_reused(self):
        result = reuse.decide(
            'push', self.event, self.head, repository='DDDHL0704/riftbound',
            token='token', fetch=self.fetch,
            tree_reader=lambda root, commit: self.tree,
        )
        self.assertTrue(result['reused'])
        self.assertEqual('MAIN_L2_REUSE_VERIFIED', result['status'])
        self.assertEqual(330, result['pullRequest'])
        self.assertEqual(41165082786, result['sourceRunId'])
        self.assertEqual(self.tree, result['sourceTreeSha'])

    def test_tree_mismatch_falls_back_to_full_suite(self):
        def mismatched_fetch(url, token):
            value = self.fetch(url, token)
            if url.endswith('/commits/' + self.run_head):
                return {'commit': {'tree': 'e' * 40}}
            return value

        result = reuse.decide(
            'push', self.event, self.head, repository='DDDHL0704/riftbound',
            token='token', fetch=mismatched_fetch,
            tree_reader=lambda root, commit: self.tree,
        )
        self.assertFalse(result['reused'])
        self.assertEqual('MAIN_L2_REUSE_NOT_USED', result['status'])
        self.assertIn('tree differs', result['reason'])

    def test_api_failure_and_non_push_are_fail_closed(self):
        result = reuse.decide(
            'push', self.event, self.head, repository='DDDHL0704/riftbound',
            token='token', fetch=lambda url, token: (_ for _ in ()).throw(
                reuse.ReuseUnavailable('offline')),
            tree_reader=lambda root, commit: self.tree,
        )
        self.assertFalse(result['reused'])
        self.assertIn('offline', result['reason'])
        self.assertFalse(reuse.decide('pull_request', {}, self.head)['reused'])
        self.assertFalse(reuse.decide('push', {'before': self.event['before']}, self.head)['reused'])

    def test_only_successful_backend_l2_runs_for_the_associated_pr_are_candidates(self):
        def no_success(url, token):
            value = self.fetch(url, token)
            if '/actions/runs?' in url:
                return {'workflow_runs': [{
                    'name': 'Release closed PR L2', 'event': 'pull_request',
                    'status': 'completed', 'conclusion': 'success',
                    'pull_requests': [{'number': 330}],
                }, {
                    'name': 'Backend L2', 'event': 'pull_request',
                    'status': 'completed', 'conclusion': 'failure',
                    'pull_requests': [{'number': 330}],
                }]}
            return value

        result = reuse.decide(
            'push', self.event, self.head, repository='DDDHL0704/riftbound',
            token='token', fetch=no_success,
            tree_reader=lambda root, commit: self.tree,
        )
        self.assertFalse(result['reused'])
        self.assertIn('no passed', result['reason'])

    def test_receipt_verification_binds_tree_and_workflow_attempt(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            receipt = root / 'decision.json'
            checkout = root / 'checkout.json'
            checkout.write_text(json.dumps({
                'status': 'FIXED_CANDIDATE_CHECKOUT_VERIFIED',
                'actualSha': self.head, 'expectedSha': self.head,
                'eventSha': self.head, 'treeSha': self.tree,
                'runId': '123', 'runAttempt': '2',
            }))
            receipt.write_text(json.dumps({
                'status': 'MAIN_L2_REUSE_VERIFIED', 'reused': True,
                'headSha': self.head, 'treeSha': self.tree,
                'sourceTreeSha': self.tree, 'sourceWorkflow': 'Backend L2',
                'sourceEvent': 'pull_request', 'sourceConclusion': 'success',
                'pullRequest': 330, 'sourceRunId': 41165082786,
                'sourceRunAttempt': 1,
                'sourceRunHeadSha': self.run_head,
                'runId': '123', 'runAttempt': '2',
            }))
            result = reuse.verify_receipt(
                receipt, checkout, self.head, 'push', self.event, '123', '2')
            self.assertEqual('MAIN_L2_REUSE_RECEIPT_VERIFIED', result['status'])
            receipt.write_text(receipt.read_text().replace(self.tree, 'e' * 40, 1))
            with self.assertRaises(ValueError):
                reuse.verify_receipt(receipt, checkout, self.head, 'push', self.event, '123', '2')

    def test_cli_malformed_event_writes_a_full_suite_fallback(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            event = root / 'event.json'
            output = root / 'decision.json'
            event.write_text('{not-json')
            completed = subprocess.run([
                'python3', str(Path(reuse.__file__)), '--event', str(event),
                '--event-name', 'push', '--head', self.head,
                '--output', str(output), '--token', 'unused',
            ], check=True, capture_output=True, text=True)
            self.assertIn('"reused": false', completed.stdout)
            self.assertFalse(json.loads(output.read_text())['reused'])


if __name__ == '__main__':
    unittest.main()
