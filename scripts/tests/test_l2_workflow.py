import itertools
import os
import re
from pathlib import Path
import textwrap
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]


class DailyWorkflowTests(unittest.TestCase):
    def workflow(self):
        return (ROOT / '.github/workflows/backend-l2.yml').read_text()

    def jobs(self):
        return dict(re.findall(r'^  ([a-z0-9_]+):\n(.*?)(?=^  [a-z0-9_]+:\n|\Z)',
                               self.workflow().split('jobs:\n', 1)[1], re.M | re.S))

    def test_complete_shards_share_preparation_and_the_derived_targeted_gate(self):
        text, jobs = self.workflow(), self.jobs()
        self.assertEqual({'prepare', 'rule_suite', 'l2_required'}, set(jobs))
        self.assertIn('name: L2 required', jobs['l2_required'])
        self.assertIn('needs: [prepare, rule_suite]', jobs['l2_required'])
        self.assertIn('if: ${{ !cancelled() }}', jobs['l2_required'])
        self.assertNotIn('always()', text)
        self.assertIn('scripts/run-l2-targeted.py', jobs['l2_required'])
        self.assertIn('RIFTBOUND_TARGETED: ${{ steps.targeted.outcome }}', jobs['l2_required'])
        self.assertIn('RIFTBOUND_QUALITY: ${{ needs.prepare.outputs.quality }}', jobs['l2_required'])
        self.assertNotIn('dotnet test', jobs['l2_required'])
        self.assertIn('scripts/l2-distributed.py shard', jobs['rule_suite'])
        self.assertIn('fail-fast: false', jobs['rule_suite'])
        self.assertIn('max-parallel: 9', jobs['rule_suite'])
        self.assertIn('shard: [shard-1, shard-2, shard-3, shard-4, shard-5, shard-6, shard-7, shard-8, shard-9]', jobs['rule_suite'])
        self.assertIn('--shards 9', jobs['prepare'])
        for forbidden in ['services:', 'needs.postgres', 'needs.native_network', 'needs.related_network',
                          'continue-on-error', 'Riftbound.NativeNetworkSmoke', 'test_process_restart',
                          'unittest discover -s scripts/tests -v']:
            self.assertNotIn(forbidden, text)
        self.assertIn('Build API and conformance once', jobs['prepare'])
        for pattern in ["-p 'test_l2*.py'", '-p test_ci_change_scope.py', '-p test_rules_runner.py',
                        '-p test_main_l2_reuse.py']:
            self.assertIn(pattern, jobs['prepare'])

    def test_docs_are_rechecked_before_selecting_jobs_and_the_gate_only_verifies_receipts(self):
        jobs = self.jobs()
        self.assertNotRegex(jobs['prepare'], r'(?m)^    if:')
        self.assertIn("if: needs.prepare.outputs.tier != 'DOCS' && needs.prepare.outputs.reused != 'true'", jobs['rule_suite'])
        self.assertIn("if: steps.scope.outputs.tier != 'DOCS' && steps.reuse.outputs.reused != 'true'", jobs['prepare'])
        self.assertEqual(2, jobs['prepare'].count('scripts/ci-change-scope.py --docs-pr-only --allow-main-docs --event'))
        self.assertIn('--recheck var/qa/l2-quality/scope-candidate.json', jobs['prepare'])
        self.assertLess(jobs['prepare'].index('--recheck'), jobs['prepare'].index('actions/setup-dotnet@'))
        self.assertIn('--verify-docs-receipt var/qa/l2-quality', jobs['l2_required'])
        self.assertIn('scripts/main-l2-reuse.py', jobs['prepare'])
        self.assertIn('--verify-receipt var/qa/l2-reuse/decision.json', jobs['l2_required'])
        self.assertIn('--checkout var/qa/l2-gate/checkout.json', jobs['l2_required'])
        self.assertNotIn('--require-docs', jobs['l2_required'])
        self.assertIn("if: needs.prepare.outputs.tier == 'DOCS'", jobs['l2_required'])
        # An uncertain scope emits L3 and therefore enters the normal full-build/six-shard path.
        self.assertNotIn("outputs.tier == 'L2'", self.workflow())
        self.assertNotIn("outputs.tier == 'L1'", self.workflow())
        # L1/L2 scope results also have full=false. Only DOCS may skip the suite.
        self.assertNotIn('outputs.full', self.workflow())

    def test_actual_gate_preconditions_reject_every_unsuccessful_stage(self):
        block = self.jobs()['l2_required'].split("python3 - <<'PYGATE'\n", 1)[1].split('          PYGATE', 1)[0]
        code = compile(textwrap.dedent(block), 'workflow-gate', 'exec')
        outcomes = ['success', 'failure', 'cancelled', 'skipped', '', 'in_progress']
        for tier in ['DOCS', 'L1', 'L2', 'L3', '']:
            for reused in ['false', 'true']:
                for values in itertools.product(outcomes, repeat=3):
                    env = dict(zip(['RIFTBOUND_PREPARE', 'RIFTBOUND_QUALITY', 'RIFTBOUND_RULE_SUITE'], values))
                    env['RIFTBOUND_TIER'] = tier
                    env['RIFTBOUND_REUSED'] = reused
                    expected = ('success', 'success', 'skipped' if tier == 'DOCS' or reused == 'true' else 'success')
                    with self.subTest(tier=tier, reused=reused, values=values), patch.dict(os.environ, env):
                        if values == expected:
                            exec(code, {})
                        else:
                            with self.assertRaises(SystemExit):
                                exec(code, {})

    def test_latest_pr_or_main_sha_is_used_by_every_checkout_and_test_receipt(self):
        text = self.workflow()
        self.assertEqual(3, text.count('ref: ${{ github.sha }}'))
        self.assertEqual(5, text.count('--event-sha "$GITHUB_SHA"'))
        self.assertIn('--commit "$GITHUB_SHA"', text)
        self.assertIn('group: l2-${{ github.event.pull_request.number || github.ref }}', text)
        self.assertIn("cancel-in-progress: ${{ github.event_name != 'workflow_dispatch' }}", text)
        triggers = text.split('permissions:', 1)[0]
        for name in ['push:', 'pull_request:', 'workflow_dispatch:']:
            self.assertIn(name, triggers)
        self.assertNotIn('paths:', triggers)

    def test_main_reuse_has_read_access_to_the_required_github_records(self):
        permissions = self.workflow().split('permissions:', 1)[1].split('concurrency:', 1)[0]
        self.assertIn('actions: read', permissions)
        self.assertIn('contents: read', permissions)
        self.assertIn('pull-requests: read', permissions)

    def test_closed_pr_cleanup_uses_the_pr_group_without_publishing_a_required_check(self):
        text = (ROOT / '.github/workflows/backend-l2-close.yml').read_text()
        triggers = text.split('permissions:', 1)[0]
        self.assertIn('pull_request:', triggers)
        self.assertIn('branches: [main]', triggers)
        self.assertIn('types: [closed]', triggers)
        self.assertNotIn('push:', triggers)
        self.assertNotIn('workflow_dispatch:', triggers)
        self.assertIn('group: l2-${{ github.event.pull_request.number }}', text)
        self.assertIn('group: l2-${{ github.event.pull_request.number || github.ref }}', self.workflow())
        self.assertIn('cancel-in-progress: true', text)
        # A skipped/successful closure check must never satisfy branch protection
        # after reopening a PR whose complete rule verification was canceled.
        self.assertNotIn('L2 required', text)
        self.assertNotIn('types:', self.workflow().split('permissions:', 1)[0])
