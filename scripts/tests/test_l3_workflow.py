import fnmatch
import re
from pathlib import Path
import tempfile
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
            if name == 'postgres_scoped':
                self.assertIn("ref: ${{ github.sha }}", block)
                self.assertIn("RIFTBOUND_FIXED_COMMIT: ${{ github.sha }}", block)
            elif name != 'scope' and "- uses: actions/checkout@" in block:
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
        for job in ['rule_prepare', 'rule_shards', 'rule_suite', 'postgres', 'rules']:
            self.assertIn('--commit "$RIFTBOUND_FIXED_COMMIT"', jobs[job])
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

    def test_sql_partitions_retain_resource_observation_and_the_original_full_command(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        job = text[text.index('  postgres_partitions:'):text.index('  native_network:')]
        self.assertIn('scripts/observe-ci-command.py --commit "$RIFTBOUND_FIXED_COMMIT"', job)
        self.assertIn('--label postgres-partition-${{ matrix.partition }}', job)
        self.assertIn('--output var/qa/ci-postgres-runtime -- dotnet run', job)
        self.assertIn('-c Release --no-build -- --ci-partition ${{ matrix.partition }} 4', job)
        self.assertIn('var/qa/ci-postgres-runtime/', job)
        self.assertIn('var/qa/ci-postgres/', job)
        self.assertIn('if: always()', job)
        self.assertNotIn('continue-on-error', job)

    def test_manual_sql_partition_is_explicit_and_full_is_the_default(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        inputs = text.split('permissions:', 1)[0]
        self.assertIn('default: all', inputs)
        self.assertIn("options: ['all', '0', '1', '2', '3']", inputs)
        self.assertIn("format('SQL partition {0} ({1})'", inputs)
        jobs = dict(re.findall(r'^  ([a-z_]+):\n(.*?)(?=^  [a-z_]+:\n|\Z)', text, re.M | re.S))
        self.assertIn("if: ${{ github.event_name != 'workflow_dispatch' || inputs.postgres_partition == 'all' }}", jobs['scope'])
        for name in ['rule_suite', 'postgres', 'rules']:
            header = jobs[name].split('    steps:', 1)[0]
            self.assertIn("github.event_name != 'workflow_dispatch' || inputs.postgres_partition == 'all'", header)
            self.assertIn('!cancelled()', header)

    def test_manual_sql_partition_pins_the_selected_ref_and_validates_its_index(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        job = text[text.index('  postgres_scoped:'):]
        self.assertIn("if: ${{ github.event_name == 'workflow_dispatch' && inputs.postgres_partition != 'all' }}", job)
        self.assertIn('ref: ${{ github.sha }}', job)
        self.assertIn('RIFTBOUND_FIXED_COMMIT: ${{ github.sha }}', job)
        self.assertIn('RIFTBOUND_POSTGRES_PARTITION: ${{ inputs.postgres_partition }}', job)
        self.assertIn("assert os.environ['RIFTBOUND_POSTGRES_PARTITION'] in {'0', '1', '2', '3'}", job)
        self.assertIn('--expected "$RIFTBOUND_FIXED_COMMIT" --event-sha "$RIFTBOUND_FIXED_COMMIT"', job)
        self.assertNotIn('ref: main', job)
        self.assertNotIn('needs: scope', job)

    def test_manual_sql_partition_runs_the_complete_selected_partition_only(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        job = text[text.index('  postgres_scoped:'):]
        self.assertIn('image: postgres:16.13@sha256:', job)
        self.assertIn('dotnet build tests/Riftbound.PostgresAcceptance -c Release -m:1', job)
        self.assertIn('--ci-partition "$RIFTBOUND_POSTGRES_PARTITION" 4', job)
        self.assertIn('scripts/observe-ci-command.py --commit "$RIFTBOUND_FIXED_COMMIT"', job)
        self.assertIn('name: postgres-scoped-${{ inputs.postgres_partition }}', job)
        self.assertIn('var/qa/ci-postgres-runtime/', job)
        self.assertNotIn('scripts/l2-distributed.py', job)
        self.assertNotIn('NativeNetworkSmoke', job)
        self.assertNotIn('continue-on-error', job)

    def test_manual_sql_payment_reconnect_is_explicit_and_keeps_one_case(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        inputs = text[text.index('      payment_reconnect_case:'):text.index('\npermissions:')]
        self.assertIn('default: none', inputs)
        self.assertIn("options: ['none', 'optional-additional-cost-reductions']", inputs)
        job = text[text.index('  postgres_scoped:'):]
        self.assertIn("if: ${{ inputs.payment_reconnect_case == 'optional-additional-cost-reductions' }}", job)
        self.assertIn('scripts/run-payment-reconnect-ci.py --expected "$RIFTBOUND_FIXED_COMMIT"', job)
        self.assertIn('--case optional-additional-cost-reductions --output var/qa/ci-payment-diagnostic', job)
        self.assertIn('name: payment-scoped-${{ inputs.postgres_partition }}', job)
        self.assertNotIn('payment_reconnect_case', text[text.index('  scope:'):text.index('  postgres_scoped:')])

    def test_workflow_action_inputs_cannot_repeat_yaml_keys(self):
        def check(text):
            for block in re.finditer(r'(?m)^( +)with:\s*\n', text):
                indent = len(block[1]); keys = []
                for line in text[block.end():].splitlines():
                    if not line.strip(): continue
                    depth = len(line) - len(line.lstrip())
                    if depth <= indent: break
                    key = re.match(r'([A-Za-z_][A-Za-z0-9_-]*):', line.lstrip())
                    if depth == indent + 2 and key: keys.append(key[1])
                if len(keys) != len(set(keys)): raise AssertionError('duplicate YAML action input')
        check((ROOT / '.github/workflows/native-desktop.yml').read_text())
        with self.assertRaisesRegex(AssertionError, 'duplicate YAML'):
            check('      - uses: action\n        with:\n          retention-days: 90\n          retention-days: 14\n')

    def test_manual_sql_observation_has_a_fresh_directory_after_checkout_receipt(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        job = text[text.index('  postgres_scoped:'):]
        checkout = re.search(r'scripts/ci-candidate\.py[^\n]+ --output (\S+)', job)[1]
        execution = re.search(r'scripts/observe-ci-command\.py[^\n]+ --output (\S+)', job)[1]
        with tempfile.TemporaryDirectory() as tmp:
            receipt = Path(tmp) / checkout
            receipt.parent.mkdir(parents=True)
            receipt.write_text('{}')
            # observe-ci-command deliberately refuses an existing output directory.
            (Path(tmp) / execution).mkdir(parents=True, exist_ok=False)
