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

    def test_workflow_starts_both_full_jobs_from_scope_and_preserves_required_rules_gate(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        jobs = dict(re.findall(r'^  ([a-z_]+):\n(.*?)(?=^  [a-z_]+:\n|\Z)', text, re.M | re.S))
        condition = "always() && !cancelled() && (needs.scope.result != 'success' || needs.scope.outputs.full != 'false')"
        for name in ['rule_suite', 'postgres']:
            self.assertIn('    needs: scope\n', jobs[name])
            self.assertIn(condition, jobs[name])
            self.assertIn('github.sha', jobs[name])
            self.assertIn('timeout-minutes: 360', jobs[name])
        self.assertIn('    needs: [scope, rule_suite, postgres]\n', jobs['rules'])
        self.assertIn("always() && (needs.scope.result != 'success' || needs.scope.outputs.full != 'false')", jobs['rules'])
        self.assertNotIn('!cancelled()', jobs['rules'])
        self.assertIn('python3 scripts/full-ci-gate.py', jobs['rules'])
        for job in ['scope', 'rule_suite', 'postgres']:
            self.assertIn('${{ needs.' + job + '.result }}', jobs['rules'])

    def test_full_workflow_keeps_actual_postgres_for_unit_smokes_and_all_full_sql_and_network_commands(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        jobs = dict(re.findall(r'^  ([a-z_]+):\n(.*?)(?=^  [a-z_]+:\n|\Z)', text, re.M | re.S))
        for job in ['rule_suite', 'postgres']:
            self.assertIn('image: postgres:16.13@sha256:', jobs[job])
            self.assertIn('ConnectionStrings__Riftbound: Host=127.0.0.1;', jobs[job])
        self.assertIn('python3 scripts/test-rules.py full --output var/qa/ci-rules', jobs['rule_suite'])
        self.assertIn('name: conformance-results', jobs['rule_suite'])
        self.assertIn('dotnet run --project tests/Riftbound.PostgresAcceptance\n', jobs['postgres'])
        self.assertNotIn('dotnet run --project tests/Riftbound.PostgresAcceptance --', jobs['postgres'])
        self.assertIn('dotnet run --project tests/Riftbound.NativeNetworkSmoke', jobs['postgres'])
        self.assertLess(jobs['postgres'].index('dotnet build src/Riftbound.Api'), jobs['postgres'].index('dotnet run --project src/Riftbound.Api --no-build'))
        self.assertIn('name: postgres-results', jobs['postgres'])
        self.assertIn('var/qa/ci-postgres/', jobs['postgres'])

    def test_full_jobs_do_not_downgrade_errors_or_conditionally_skip_real_gates(self):
        text = (ROOT / '.github/workflows/native-desktop.yml').read_text()
        jobs = dict(re.findall(r'^  ([a-z_]+):\n(.*?)(?=^  [a-z_]+:\n|\Z)', text, re.M | re.S))
        for job in ['rule_suite', 'postgres', 'rules']:
            self.assertNotIn('continue-on-error', jobs[job])
        for job, name in [('rule_suite', 'Complete rule suite with checked process shards'),
                          ('postgres', 'Real PostgreSQL core recovery and replay'),
                          ('postgres', 'Native client connection recovery'),
                          ('rules', 'Require every fixed full child job')]:
            block = jobs[job].split('      - name: ' + name + '\n', 1)[1]
            block = re.split(r'\n      - ', block, maxsplit=1)[0]
            self.assertNotIn('        if:', block)
