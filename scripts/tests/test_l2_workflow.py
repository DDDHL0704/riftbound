import re
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class DailyWorkflowTests(unittest.TestCase):
    def test_every_daily_entry_runs_actual_focus_and_rules_with_an_always_materialized_gate(self):
        text = (ROOT / '.github/workflows/backend-l2.yml').read_text()
        jobs = dict(re.findall(r'^  ([a-z0-9_]+):\n(.*?)(?=^  [a-z0-9_]+:\n|\Z)', text.split('jobs:\n', 1)[1], re.M | re.S))
        self.assertEqual({'quality', 'prepare', 'targeted', 'rule_suite', 'db_preflight', 'l2_required'}, set(jobs))
        self.assertIn('name: L2 required', jobs['l2_required'])
        self.assertIn('needs: [quality, prepare, rule_suite, targeted, db_preflight]', jobs['l2_required'])
        self.assertIn('if: ${{ always() }}', jobs['l2_required'])
        self.assertIn('scripts/run-l2-targeted.py', jobs['targeted'])
        self.assertNotIn('dotnet test', jobs['targeted'])
        self.assertIn('scripts/l2-distributed.py shard', jobs['rule_suite'])
        self.assertIn('fail-fast: false', jobs['rule_suite'])
        self.assertIn('max-parallel: 6', jobs['rule_suite'])
        self.assertIn('--shards 6', jobs['prepare'])
        self.assertIn('needs: [prepare, rule_suite]', jobs['targeted'])
        for name in ['quality', 'prepare', 'rule_suite']:
            self.assertNotRegex(jobs[name], r'(?m)^    if:')
        for forbidden in ['needs.postgres', 'needs.native_network', 'needs.related_network',
                          'continue-on-error', 'Riftbound.NativeNetworkSmoke', 'test_process_restart',
                          'unittest discover -s scripts/tests -v']:
            self.assertNotIn(forbidden, text)
        self.assertIn('services:', jobs['db_preflight'])
        self.assertIn('postgres:16.13@sha256:', jobs['db_preflight'])
        self.assertIn('scripts/l2-db-preflight.py run', jobs['db_preflight'])
        self.assertIn('scripts/l2-db-preflight.py verify', jobs['l2_required'])
        self.assertIn('needs.db_preflight.result', jobs['l2_required'])
        self.assertIn('test "$DB_PREFLIGHT_RESULT" = success', jobs['l2_required'])
        self.assertIn('if: always()', jobs['db_preflight'])
        self.assertIn('Build API and conformance once', jobs['prepare'])
        self.assertNotIn('dotnet build', jobs['quality'])
        self.assertIn("-p 'test_l2*.py'", jobs['quality'])

    def test_latest_pr_or_main_sha_is_used_by_every_checkout_and_test_receipt(self):
        text = (ROOT / '.github/workflows/backend-l2.yml').read_text()
        self.assertEqual(6, text.count('ref: ${{ github.sha }}'))
        self.assertEqual(6, text.count('--event-sha "$GITHUB_SHA"'))
        self.assertIn('--commit "$GITHUB_SHA"', text)
        triggers = text.split('permissions:', 1)[0]
        for name in ['push:', 'pull_request:', 'workflow_dispatch:']:
            self.assertIn(name, triggers)
        self.assertNotIn('paths:', triggers)
