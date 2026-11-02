import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from zipfile import ZipFile


spec = importlib.util.spec_from_file_location(
    'main_l2_reuse', Path(__file__).parents[1] / 'main-l2-reuse.py')
reuse = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reuse)


class MainL2ReuseTests(unittest.TestCase):
    def setUp(self):
        self.head = 'a' * 40
        self.run_head = 'b' * 40
        self.checkout_head = 'f' * 40
        self.tree = 'c' * 40
        self.repository = 'DDDHL0704/riftbound'
        self.branch = 'a/l2-ven-opening-batch'
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
                'number': 330,
                'merged_at': '2026-10-31T04:11:49Z',
                'merge_commit_sha': self.head,
                'base': {'ref': 'main'},
                'head': {'ref': self.branch, 'sha': self.run_head},
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
                'head_branch': self.branch,
                'path': '.github/workflows/backend-l2.yml',
                'repository': {'id': 123, 'full_name': self.repository},
                'head_repository': {'id': 123, 'full_name': self.repository},
                'pull_requests': [{'number': 330}],
                'html_url': 'https://example.invalid/runs/41165082786',
                'updated_at': '2026-10-31T04:10:32Z',
            }]}
        if '/artifacts?name=l2-quality' in url:
            return {'total_count': 1, 'artifacts': [{
                'id': 42, 'name': 'l2-quality', 'expired': False, 'size_in_bytes': 300,
                'workflow_run': {'id': 41165082786, 'head_sha': self.run_head,
                                 'head_branch': self.branch, 'repository_id': 123,
                                 'head_repository_id': 123},
            }]}
        if url.endswith('/commits/' + self.checkout_head):
            return {'commit': {'tree': {'sha': self.tree}}}
        self.fail('unexpected API URL: ' + url)

    def artifact(self, repository, artifact_id, token):
        self.assertEqual((self.repository, 42, 'token'), (repository, artifact_id, token))
        return {
            'status': 'FIXED_CANDIDATE_CHECKOUT_VERIFIED',
            'expectedSha': self.checkout_head, 'actualSha': self.checkout_head,
            'eventSha': self.checkout_head, 'treeSha': self.tree,
            'runId': '41165082786', 'runAttempt': '1', 'job': 'prepare',
            'ref': 'refs/pull/330/merge',
            'workflowRef': self.repository + '/.github/workflows/backend-l2.yml@refs/pull/330/merge',
        }

    def test_matching_passed_pr_l2_and_tree_is_reused(self):
        result = reuse.decide(
            'push', self.event, self.head, repository='DDDHL0704/riftbound',
            token='token', fetch=self.fetch,
            tree_reader=lambda root, commit: self.tree,
            artifact_reader=self.artifact,
        )
        self.assertTrue(result['reused'])
        self.assertEqual('MAIN_L2_REUSE_VERIFIED', result['status'])
        self.assertEqual(330, result['pullRequest'])
        self.assertEqual(41165082786, result['sourceRunId'])
        self.assertEqual(self.tree, result['sourceTreeSha'])
        self.assertEqual(self.checkout_head, result['sourceCheckoutSha'])
        self.assertEqual(42, result['sourceQualityArtifactId'])

    def test_tree_mismatch_falls_back_to_full_suite(self):
        result = reuse.decide(
            'push', self.event, self.head, repository='DDDHL0704/riftbound',
            token='token', fetch=self.fetch,
            tree_reader=lambda root, commit: 'e' * 40,
            artifact_reader=self.artifact,
        )
        self.assertFalse(result['reused'])
        self.assertEqual('MAIN_L2_REUSE_NOT_USED', result['status'])
        self.assertIn('tree differs', result['reason'])

    def test_merged_pr_empty_associations_keep_exact_head_identification(self):
        def cleared(url, token):
            value = self.fetch(url, token)
            if '/actions/runs?' in url:
                value['workflow_runs'][0]['pull_requests'] = []
            return value
        result = reuse.decide('push', self.event, self.head, repository=self.repository,
                              token='token', fetch=cleared, tree_reader=lambda root, commit: self.tree,
                              artifact_reader=self.artifact)
        self.assertTrue(result['reused'])

    def test_pr_head_tree_cannot_substitute_for_the_tested_merge_checkout(self):
        queries = []
        def fetch(url, token):
            queries.append(url)
            if url.endswith('/commits/' + self.run_head):
                return {'commit': {'tree': {'sha': self.tree}}}
            if url.endswith('/commits/' + self.checkout_head):
                return {'commit': {'tree': {'sha': 'e' * 40}}}
            return self.fetch(url, token)
        def artifact(*args):
            value = self.artifact(*args)
            value['treeSha'] = 'e' * 40
            return value
        result = reuse.decide('push', self.event, self.head, repository=self.repository,
                              token='token', fetch=fetch, tree_reader=lambda root, commit: self.tree,
                              artifact_reader=artifact)
        self.assertFalse(result['reused'])
        self.assertIn('tree differs', result['reason'])
        self.assertFalse(any(url.endswith('/commits/' + self.run_head) for url in queries))

    def test_cleared_associations_do_not_match_other_heads_branches_or_workflows(self):
        for key, wrong in [('head_sha', 'e' * 40), ('head_branch', 'other'),
                           ('path', '.github/workflows/other.yml'),
                           ('head_repository', {'id': 99, 'full_name': 'other/fork'}),
                           ('pull_requests', [{'number': 999}])]:
            def changed(url, token):
                value = self.fetch(url, token)
                if '/actions/runs?' in url:
                    value['workflow_runs'][0]['pull_requests'] = []
                    value['workflow_runs'][0][key] = wrong
                return value
            with self.subTest(key=key):
                result = reuse.decide('push', self.event, self.head, repository=self.repository,
                                      token='token', fetch=changed, tree_reader=lambda root, commit: self.tree,
                                      artifact_reader=self.artifact)
                self.assertFalse(result['reused'])

    def test_checkout_receipt_must_bind_run_attempt_pr_and_commit(self):
        for key, wrong in [('runId', '1'), ('runAttempt', '2'), ('ref', 'refs/pull/999/merge'),
                           ('workflowRef', 'other/repo/.github/workflows/backend-l2.yml@refs/pull/330/merge'),
                           ('expectedSha', self.run_head), ('treeSha', 'e' * 40)]:
            def changed(*args):
                value = self.artifact(*args)
                value[key] = wrong
                return value
            with self.subTest(key=key):
                result = reuse.decide('push', self.event, self.head, repository=self.repository,
                                      token='token', fetch=self.fetch, tree_reader=lambda root, commit: self.tree,
                                      artifact_reader=changed)
                self.assertFalse(result['reused'])

    def test_quality_artifact_identity_and_expiration_are_checked(self):
        for key, wrong in [('expired', True), ('size_in_bytes', 1024 * 1024 + 1),
                           ('workflow_run', {'id': 1}), ('id', True)]:
            def changed(url, token):
                value = self.fetch(url, token)
                if '/artifacts?name=l2-quality' in url:
                    value['artifacts'][0][key] = wrong
                return value
            with self.subTest(key=key):
                result = reuse.decide('push', self.event, self.head, repository=self.repository,
                                      token='token', fetch=changed, tree_reader=lambda root, commit: self.tree,
                                      artifact_reader=self.artifact)
                self.assertFalse(result['reused'])

    def test_checkout_archive_reads_only_one_bounded_receipt(self):
        def archive(names):
            buffer = io.BytesIO()
            with ZipFile(buffer, 'w') as output:
                for name, value in names:
                    output.writestr(name, json.dumps(value))
            return buffer.getvalue()
        receipt = self.artifact(self.repository, 42, 'token')
        self.assertEqual(receipt, reuse.decode_checkout_archive(archive([('checkout.json', receipt)])))
        for names in [[('other.json', receipt)], [('checkout.json', [])],
                      [('checkout.json', {'tooLarge': 'x' * 65536})]]:
            with self.subTest(names=[name for name, _ in names]), self.assertRaises(reuse.ReuseUnavailable):
                reuse.decode_checkout_archive(archive(names))

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
                'schemaVersion': 2,
                'status': 'MAIN_L2_REUSE_VERIFIED', 'reused': True,
                'headSha': self.head, 'treeSha': self.tree,
                'sourceTreeSha': self.tree, 'sourceWorkflow': 'Backend L2',
                'sourceEvent': 'pull_request', 'sourceConclusion': 'success',
                'pullRequest': 330, 'sourceRunId': 41165082786,
                'sourceRunAttempt': 1,
                'sourceRunHeadSha': self.run_head,
                'sourceCheckoutSha': self.checkout_head,
                'sourceQualityArtifactId': 42,
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
