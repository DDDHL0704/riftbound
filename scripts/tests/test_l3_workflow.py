import fnmatch
import re
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class WorkflowCandidateTests(unittest.TestCase):
    @staticmethod
    def harness_matches(path):
        text = (ROOT / '.github/workflows/backend-harness.yml').read_text()
        block = re.search(r"^    paths:\n((?:      (?:- .+|#.*)\n)+)", text, re.M)
        if block is None:
            raise AssertionError('The harness needs an explicit pull-request path scope.')
        patterns = re.findall(r"^      - '([^']+)'$", block.group(1), re.M)
        return any(fnmatch.fnmatchcase(path, pattern) for pattern in patterns)

    def test_payment_consumers_do_not_start_the_shared_restart_harness(self):
        # These two actual PR131 changes triggered the unintended extra run.
        for path in ('tests/Riftbound.NativeNetworkSmoke/PaymentAcceptanceStartupHook.cs',
                     'tests/Riftbound.NativeNetworkSmoke/PaymentResourceSurplusNetworkAcceptance.cs',
                     'tests/Riftbound.NativeNetworkSmoke/OptionalAdditionalCostReductionNetworkAcceptance.cs'):
            with self.subTest(path=path):
                self.assertFalse(self.harness_matches(path))

    def test_shared_restart_tools_still_start_the_harness(self):
        for path in ('.github/workflows/backend-harness.yml',
                     'scripts/run-process-restart-ci.py',
                     'scripts/tests/test_process_restart_ci.py',
                     'scripts/tests/test_l3_workflow.py',
                     'tests/Riftbound.NativeNetworkSmoke/Program.cs',
                     'tests/Riftbound.NativeNetworkSmoke/ProcessRestartAcceptance.cs',
                     'tests/Riftbound.NativeNetworkSmoke/Riftbound.NativeNetworkSmoke.csproj',
                     'tests/Riftbound.PostgresAcceptance/SqlAcceptancePartition.cs'):
            with self.subTest(path=path):
                self.assertTrue(self.harness_matches(path))

    def test_payment_driver_entrypoints_start_the_harness(self):
        for project in ('PaymentResourceSurplus', 'InstructionPayment'):
            for filename in ('Program.cs', f'Riftbound.{project}Acceptance.csproj'):
                with self.subTest(project=project, filename=filename):
                    self.assertTrue(self.harness_matches(f'tests/Riftbound.{project}Acceptance/{filename}'))

    def test_harness_runs_s3_and_p7_on_its_exact_merge_input(self):
        text = (ROOT / '.github/workflows/backend-harness.yml').read_text()
        self.assertIn('scripts/run-process-restart-ci.py', text)
        payment = text[text.index('- name: S3 and P7 SQL,'):]
        self.assertIn('scripts/run-payment-reconnect-ci.py --expected "${{ github.sha }}"', payment)
        self.assertIn('--case payment-resource-surplus --case instruction-power-payment', payment)
        self.assertIn('name: payment-harness-results', payment)
        self.assertIn('path: var/qa/ci-payment-harness/', payment)
        self.assertIn('retention-days: 90', payment)
        self.assertNotIn('continue-on-error', text)

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
        self.assertEqual(3, text.count("ref: ${{ github.sha }}"))
        self.assertIn("needs: [prepare, rule_suite]", text)
        self.assertIn('scripts/l2-distributed.py aggregate', text)
        self.assertIn('scripts/l2-distributed.py shard', text)
        self.assertIn("name: L2 required", text)
        for value in ['needs.postgres', 'needs.native_network', 'needs.related_network', 'continue-on-error']:
            self.assertNotIn(value, text)
