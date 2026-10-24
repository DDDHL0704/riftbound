import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('process_restart', ROOT / 'scripts/run-process-restart-ci.py')
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)


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
