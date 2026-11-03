import importlib.util
from contextlib import redirect_stdout
import io
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('process_restart', ROOT / 'scripts/run-process-restart-ci.py')
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)


class RunLoggedTests(unittest.TestCase):
    def test_failed_driver_prints_its_error_and_keeps_the_full_log_and_exit_code(self):
        with tempfile.TemporaryDirectory() as folder:
            log = Path(folder) / 'driver-prepare.log'
            command = [sys.executable, '-c',
                       'import sys\nfor i in range(100): print(f"driver line {i}", flush=True)\n'
                       'print("original driver assertion", file=sys.stderr)\nsys.exit(17)']
            console = io.StringIO()
            with redirect_stdout(console), self.assertRaises(subprocess.CalledProcessError) as failed:
                RUNNER.run_logged(command, log, dict(os.environ))
            expected = [f'driver line {i}' for i in range(100)] + ['original driver assertion']
            self.assertEqual(expected, log.read_text().splitlines())
            self.assertEqual([f'Command failed; last 80 lines of {log}:'] + expected[-80:],
                             console.getvalue().splitlines())
            self.assertEqual(17, failed.exception.returncode)
            self.assertEqual(command, failed.exception.cmd)

    def test_success_uses_the_given_environment_and_keeps_output_in_the_log(self):
        with tempfile.TemporaryDirectory() as folder:
            log = Path(folder) / 'driver.log'
            command = [sys.executable, '-c',
                       'import os,sys; print(os.environ["RIFTBOUND_TEST_DRIVER_ENV"], flush=True); '
                       'print("driver warning", file=sys.stderr)']
            console = io.StringIO()
            with redirect_stdout(console):
                RUNNER.run_logged(command, log, dict(os.environ, RIFTBOUND_TEST_DRIVER_ENV='configured'))
            self.assertEqual(['configured', 'driver warning'], log.read_text().splitlines())
            self.assertEqual('', console.getvalue())

    def test_invalid_utf8_cannot_hide_the_original_failure_or_change_its_log(self):
        with tempfile.TemporaryDirectory() as folder:
            log = Path(folder) / 'driver.log'
            command = [sys.executable, '-c',
                       'import sys; sys.stderr.buffer.write(b"driver error: \\xff"); sys.exit(23)']
            console = io.StringIO()
            with redirect_stdout(console), self.assertRaises(subprocess.CalledProcessError) as failed:
                RUNNER.run_logged(command, log, dict(os.environ))
            self.assertEqual(b'driver error: \xff', log.read_bytes())
            self.assertIn('driver error: \ufffd\n', console.getvalue())
            self.assertEqual(23, failed.exception.returncode)

    def test_existing_log_is_preserved_without_starting_the_command(self):
        with tempfile.TemporaryDirectory() as folder:
            log, marker = Path(folder) / 'driver.log', Path(folder) / 'started'
            log.write_bytes(b'previous original output')
            command = [sys.executable, '-c', 'from pathlib import Path; import sys; Path(sys.argv[1]).touch()', str(marker)]
            with self.assertRaises(FileExistsError):
                RUNNER.run_logged(command, log, dict(os.environ))
            self.assertEqual(b'previous original output', log.read_bytes())
            self.assertFalse(marker.exists())


class RestartSequenceTests(unittest.TestCase):
    def setUp(self):
        self.calls = []
        self.pid = 0

    def launch(self, phase):
        self.pid += 1
        self.calls.append(('start', self.pid))
        return type('UnitProcess', (), {'pid': self.pid})()

    def driver(self, phase):
        self.calls.append(('driver', phase))

    def stop(self, process):
        self.calls.append(('stop', process.pid))
        return 0

    def test_driver_import_does_not_dirty_a_fresh_checkout(self):
        import os, subprocess, sys, tempfile
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            scripts = root / 'scripts'
            scripts.mkdir()
            for name in ['ci-candidate.py', 'run-process-restart-ci.py']:
                (scripts / name).write_bytes((ROOT / 'scripts' / name).read_bytes())
            command = "import sys,runpy;sys.pycache_prefix=None;sys.dont_write_bytecode=False;sys.argv=['scripts/run-process-restart-ci.py','--help'];runpy.run_path(sys.argv[0],run_name='__main__')"
            subprocess.run([sys.executable, '-c', command], cwd=root, check=True, capture_output=True)
            self.assertFalse((scripts / '__pycache__').exists())

    def test_old_process_is_stopped_before_recovery_uses_a_new_process(self):
        result = RUNNER.restart_sequence(self.launch, self.driver, lambda pid: {'syntheticUnitFixture': True}, self.stop)
        self.assertEqual([('start', 1), ('driver', 'restart-prepare'), ('stop', 1),
                          ('start', 2), ('driver', 'restart-recover'), ('stop', 2)], self.calls)
        self.assertEqual([1, 2], [p['processId'] for p in result])

    def test_failed_driver_cannot_recover_and_its_process_is_cleaned_up(self):
        def fail(phase): raise ValueError('original semantic failure')
        with self.assertRaisesRegex(ValueError, 'original semantic failure'):
            RUNNER.restart_sequence(self.launch, fail, lambda pid: {}, self.stop)
        self.assertEqual([('start', 1), ('stop', 1)], self.calls)

    def test_changed_original_sql_history_cannot_pass(self):
        with self.assertRaisesRegex(ValueError, 'changed the original SQL'):
            RUNNER.restart_sequence(self.launch, self.driver, lambda pid: {'different': pid}, self.stop)
        self.assertEqual(('stop', 2), self.calls[-1])

    def test_resume_uses_the_recovered_process_only_after_original_history_capture(self):
        def capture(pid):
            self.calls.append(('capture', pid))
            return {'original': True}
        RUNNER.restart_sequence(self.launch, self.driver, capture, self.stop,
                                lambda: self.calls.append(('resume', self.pid)))
        self.assertEqual([('start', 1), ('driver', 'restart-prepare'), ('capture', 1), ('stop', 1),
                          ('start', 2), ('driver', 'restart-recover'), ('capture', 2), ('resume', 2), ('stop', 2)], self.calls)

    def test_changed_history_prevents_resume_and_still_stops_the_recovered_process(self):
        with self.assertRaisesRegex(ValueError, 'changed the original SQL'):
            RUNNER.restart_sequence(self.launch, self.driver, lambda pid: {'different': pid}, self.stop,
                                    lambda: self.calls.append(('resume', self.pid)))
        self.assertNotIn(('resume', 2), self.calls)
        self.assertEqual(('stop', 2), self.calls[-1])

    def test_failed_resume_cleans_up_the_recovered_process(self):
        def resume(): raise ValueError('sealed resume rejected')
        with self.assertRaisesRegex(ValueError, 'sealed resume rejected'):
            RUNNER.restart_sequence(self.launch, self.driver, lambda pid: {'original': True}, self.stop, resume)
        self.assertEqual(('stop', 2), self.calls[-1])

    def test_reused_pid_cannot_prove_restart(self):
        def launch(phase): return type('UnitProcess', (), {'pid': 7})()
        with self.assertRaisesRegex(ValueError, 'new actual API process'):
            RUNNER.restart_sequence(launch, self.driver, lambda pid: {}, self.stop)

    def test_cleanup_failure_is_retained_without_masking_original_failure(self):
        def fail(phase): raise ValueError('original rejection')
        def stop(process): raise RuntimeError('cleanup failed')
        with self.assertRaisesRegex(ValueError, 'original rejection') as caught:
            RUNNER.restart_sequence(self.launch, fail, lambda pid: {}, stop)
        self.assertIn('cleanup failed', caught.exception.riftbound_cleanup_errors[0])
