import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'scripts/observe-ci-command.py'


class ExecutionProfileTests(unittest.TestCase):
    def run_command(self, output, source, commit=None, arguments=(), env=None):
        commit = commit or subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
        return subprocess.run([sys.executable, str(SCRIPT), '--commit', commit,
                               '--label', 'postgres-partition-0', '--output', str(output), '--',
                               sys.executable, '-c', source, *arguments], cwd=ROOT,
                              capture_output=True, text=True, env=env)

    def test_success_retains_both_output_streams_and_bound_resource_samples(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'observation'
            result = self.run_command(output, 'import sys; print("SQL_CASE_STARTED example", flush=True); '
                                      'print("SQL_CASE_PASSED example", file=sys.stderr)')
            self.assertEqual(0, result.returncode, result.stderr)
            for line in ('SQL_CASE_STARTED example', 'SQL_CASE_PASSED example'):
                self.assertIn(line, result.stdout)
                self.assertIn(line, (output / 'command.log').read_text())
            data = json.loads((output / 'execution-runtime.json').read_text())
            self.assertEqual('postgres-partition-0', data['label'])
            self.assertEqual('CI_COMMAND', data['kind'])
            self.assertEqual(0, data['exitCode'])
            self.assertEqual(subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT,
                                                    text=True).strip(), data['commit'])
            raw = (output / 'execution-runtime.jsonl').read_bytes()
            self.assertEqual(hashlib.sha256(raw).hexdigest(), data['samplesSha256'])
            self.assertEqual([json.loads(line) for line in raw.splitlines()], data['samples'])
            self.assertGreaterEqual(len(data['samples']), 2)
            self.assertIn('processorIdentity', data)
            self.assertEqual(0, data['samples'][-1]['exitCode'])

    def test_child_failure_keeps_its_exit_code_and_diagnostic_output(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'observation'
            result = self.run_command(output, 'import sys; print("SQL failed", file=sys.stderr); sys.exit(17)')
            self.assertEqual(17, result.returncode)
            self.assertIn('SQL failed', result.stdout)
            self.assertEqual(17, json.loads((output / 'execution-runtime.json').read_text())['exitCode'])

    def test_command_arguments_and_environment_reach_the_child_unchanged(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'observation'
            arguments = ['--ci-partition', '0', '4', '--', 'a value with spaces']
            result = self.run_command(output, 'import json, os, sys; '
                                      'print(json.dumps([sys.argv[1:], os.environ["RIFTBOUND_PROFILE_TEST"]]))',
                                      arguments=arguments, env=dict(os.environ, RIFTBOUND_PROFILE_TEST='inherited'))
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual([arguments, 'inherited'], json.loads((output / 'command.log').read_text()))

    def test_output_cannot_be_reused_or_its_original_log_replaced(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'observation'
            first = self.run_command(output, 'print("original")')
            self.assertEqual(0, first.returncode, first.stderr)
            original = {p.name: p.read_bytes() for p in output.iterdir()}
            second = self.run_command(output, 'print("replacement")')
            self.assertNotEqual(0, second.returncode)
            self.assertEqual(original, {p.name: p.read_bytes() for p in output.iterdir()})

    def test_wrong_commit_rejects_before_running_the_child(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'observation'
            result = self.run_command(output, 'print("should not run")', commit='0' * 40)
            self.assertNotEqual(0, result.returncode)
            self.assertNotIn('should not run', result.stdout)
            self.assertFalse(output.exists())


if __name__ == '__main__':
    unittest.main()
