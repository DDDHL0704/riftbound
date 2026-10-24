import re
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class WorkflowCandidateTests(unittest.TestCase):
    def test_nightly_pins_main_once_and_all_children_use_that_exact_sha(self):
        text = (ROOT / ".github/workflows/native-desktop.yml").read_text()
        jobs = dict(re.findall(r"^  ([a-z0-9_]+):\n(.*?)(?=^  [a-z_]+:\n|\Z)", text, re.M | re.S))
        self.assertIn("ref: main", jobs['scope'])
        self.assertIn("steps.fixed.outputs.commit", jobs['scope'])
        for name, block in jobs.items():
            if name != 'scope' and "- uses: actions/checkout@" in block:
                with self.subTest(job=name):
                    self.assertIn("ref: ${{ needs.scope.outputs.commit }}", block)
                    self.assertNotIn("ref: main", block)
        self.assertNotIn("cancel-in-progress: true", text)
        self.assertIn('--expected "${{ needs.scope.outputs.commit }}"', jobs['related_network'])
        self.assertIn('scripts/run-process-restart-ci.py', jobs['related_network'])
        self.assertIn('scripts/run-payment-reconnect-ci.py', jobs['related_network'])
        self.assertIn('name: payment-reconnect-results', jobs['related_network'])
        self.assertIn('retention-days: 90', jobs['related_network'])
        self.assertEqual(1, text.count("cron:"))
        self.assertNotIn('--commit "$GITHUB_SHA"', text)
        self.assertEqual(2, text.count('--commit "$RIFTBOUND_FIXED_COMMIT"'))
        self.assertIn('RIFTBOUND_FIXED_COMMIT: ${{ needs.scope.outputs.commit }}', jobs['postgres_partitions'])
        partition = (ROOT / 'tests/Riftbound.PostgresAcceptance/SqlAcceptancePartition.cs').read_text()
        self.assertIn('Environment.GetEnvironmentVariable("RIFTBOUND_FIXED_COMMIT") ?? Environment.GetEnvironmentVariable("GITHUB_SHA")', partition)

    def test_l2_checks_the_actual_pr_merge_or_main_commit_and_has_no_l3_dependency(self):
        text = (ROOT / ".github/workflows/backend-l2.yml").read_text()
        self.assertEqual(4, text.count("ref: ${{ github.sha }}"))
        self.assertIn("needs: [quality, targeted, rule_suite]", text)
        self.assertIn("name: L2 required", text)
        for value in ['needs.postgres', 'needs.native_network', 'needs.related_network', 'continue-on-error']:
            self.assertNotIn(value, text)
