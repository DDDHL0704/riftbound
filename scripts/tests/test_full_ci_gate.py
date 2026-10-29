import importlib.util
import itertools
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('full_ci_gate', ROOT / 'scripts/full-ci-gate.py')
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


class FullCiGateTests(unittest.TestCase):
    def test_every_child_must_succeed_including_failed_cancelled_skipped_and_missing_states(self):
        for states in itertools.product(['success', 'failure', 'cancelled', 'skipped', ''], repeat=3):
            results = dict(zip(['scope', 'rule_suite', 'postgres'], states))
            with self.subTest(states=states):
                if states == ('success',) * 3:
                    self.assertEqual('FIXED_COMMIT_FULL_CHILD_JOBS_ALL_SUCCEEDED', GATE.full_gate('1' * 40, results)['status'])
                else:
                    with self.assertRaises(ValueError):
                        GATE.full_gate('1' * 40, results)

    def test_missing_or_extra_dependency_and_unpinned_commit_fail_closed(self):
        good = {'scope': 'success', 'rule_suite': 'success', 'postgres': 'success'}
        for commit, jobs in [('HEAD', good), ('1' * 39, good), ('1' * 40, {'scope': 'success'}),
                             ('1' * 40, {**good, 'unreviewed': 'success'})]:
            with self.subTest(commit=commit, jobs=jobs), self.assertRaises(ValueError):
                GATE.full_gate(commit, jobs)

    def test_cli_rejection_creates_no_success_receipt(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'gate.json'
            result = subprocess.run([sys.executable, str(ROOT / 'scripts/full-ci-gate.py'), '--commit', '1' * 40,
                '--scope', 'success', '--rule-suite', 'success', '--postgres', 'skipped', '--output', str(output)],
                capture_output=True, text=True)
            self.assertNotEqual(0, result.returncode)
            self.assertFalse(output.exists())

    def test_cli_success_records_the_exact_commit_and_all_dependency_results(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'gate.json'
            subprocess.run([sys.executable, str(ROOT / 'scripts/full-ci-gate.py'), '--commit', '2' * 40,
                '--scope', 'success', '--rule-suite', 'success', '--postgres', 'success', '--output', str(output)],
                check=True, capture_output=True, text=True)
            result = json.loads(output.read_text())
            self.assertEqual('2' * 40, result['commit'])
            self.assertEqual({'scope': 'success', 'rule_suite': 'success', 'postgres': 'success'}, result['jobs'])

    def test_nightly_runs_all_full_children_from_the_fixed_main_snapshot(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        jobs = dict(re.findall(r'^  ([a-z_]+):\n(.*?)(?=^  [a-z_]+:\n|\Z)', text, re.M | re.S))
        for name in ['rule_prepare', 'postgres_partitions', 'native_network', 'related_network']:
            self.assertIn('    needs: scope\n', jobs[name])
            self.assertIn('ref: ${{ needs.scope.outputs.commit }}', jobs[name])
            self.assertNotIn('continue-on-error', jobs[name])
        for name in ['rule_shards', 'postgres_partitions']:
            self.assertIn('timeout-minutes: 360', jobs[name])
            self.assertIn('image: postgres:16.13@sha256:', jobs[name])
            self.assertIn('ConnectionStrings__Riftbound: Host=127.0.0.1;', jobs[name])
        for name in ['rule_shards', 'rule_suite']:
            self.assertIn('ref: ${{ needs.scope.outputs.commit }}', jobs[name])
            self.assertNotIn('continue-on-error', jobs[name])
        self.assertIn('needs: [scope, rule_prepare]', jobs['rule_shards'])
        self.assertIn('needs: [scope, rule_prepare, rule_shards]', jobs['rule_suite'])
        self.assertIn('scripts/l2-distributed.py prepare --profile full', jobs['rule_prepare'])
        self.assertIn('scripts/l2-distributed.py shard', jobs['rule_shards'])
        self.assertIn('scripts/l2-distributed.py aggregate-full', jobs['rule_suite'])
        self.assertIn('RIFTBOUND_PREPARE: ${{ needs.rule_prepare.result }}', jobs['rule_suite'])
        self.assertIn('RIFTBOUND_RULE_SHARDS: ${{ needs.rule_shards.result }}', jobs['rule_suite'])
        self.assertIn('shard: [shard-1, shard-2, shard-3, shard-4, shard-5, shard-6]', jobs['rule_shards'])
        self.assertIn('fail-fast: false', jobs['rule_shards'])
        self.assertIn('partition: [0, 1, 2, 3]', jobs['postgres_partitions'])
        self.assertIn('dotnet run --project tests/Riftbound.PostgresAcceptance -- --ci-partition ${{ matrix.partition }} 4', jobs['postgres_partitions'])
        self.assertIn('dotnet run --project tests/Riftbound.NativeNetworkSmoke', jobs['native_network'])
        self.assertIn('python3 scripts/run-process-restart-ci.py', jobs['related_network'])
        self.assertIn('needs: [scope, postgres_partitions, native_network, related_network]', jobs['postgres'])
        self.assertIn('python3 scripts/sql-ci-gate.py', jobs['postgres'])
        self.assertIn('needs: [scope, rule_suite, postgres]', jobs['rules'])
        self.assertIn('if: ${{ always() }}', jobs['rules'])
        self.assertIn('python3 scripts/full-ci-gate.py', jobs['rules'])
        for job in ['scope', 'rule_suite', 'postgres']:
            self.assertIn('${{ needs.' + job + '.result }}', jobs['rules'])

    def test_one_nightly_schedule_and_manual_entry_keep_full_meaning_separate_from_daily_l2(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        self.assertIn("cron: '0 18 * * *'", text)
        self.assertEqual(1, text.count('cron:'))
        triggers = text.split('permissions:', 1)[0]
        self.assertIn('workflow_dispatch:', triggers)
        self.assertNotIn('  push:', triggers)
        self.assertNotIn('  pull_request:', triggers)
        self.assertIn('retention-days: 90', text)
        self.assertNotIn('continue-on-error', text)
        daily = (ROOT / '.github/workflows/backend-l2.yml').read_text()
        self.assertIn('  push:', daily)
        self.assertIn('  pull_request:', daily)
        self.assertIn('python3 scripts/l2-distributed.py prepare', daily)
        self.assertIn('python3 scripts/l2-distributed.py shard', daily)
        self.assertIn('python3 scripts/l2-distributed.py aggregate', daily)
        self.assertIn('needs: [quality, prepare, rule_suite, targeted]', daily)
        self.assertIn('fail-fast: false', daily)
        self.assertNotIn('scripts/test-rules.py full', daily)
        self.assertNotIn('services:', daily)

    def test_actual_test_and_full_gate_steps_cannot_be_conditionally_skipped(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        jobs = dict(re.findall(r'^  ([a-z_]+):\n(.*?)(?=^  [a-z_]+:\n|\Z)', text, re.M | re.S))
        for job, name in [('rule_prepare', 'Build once and enumerate every full rule case'),
                          ('rule_shards', 'Execute every assigned full rule case without rebuilding'),
                          ('rule_suite', 'Require every discovered full rule case exactly once'),
                          ('postgres_partitions', 'Real PostgreSQL complete checked partition'),
                          ('native_network', 'Native client connection recovery'),
                          ('postgres', 'Require every fixed SQL case, SignalR and process restart result'),
                          ('rules', 'Require every fixed full child job')]:
            block = jobs[job].split('      - name: ' + name + '\n', 1)[1]
            block = re.split(r'\n      - ', block, maxsplit=1)[0]
            self.assertNotIn('        if:', block)
