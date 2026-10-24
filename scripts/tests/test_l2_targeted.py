import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('l2_targeted', ROOT / 'scripts/run-l2-targeted.py')
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)


class TargetSelectionTests(unittest.TestCase):
    def test_changed_new_and_existing_classes_are_added_to_adjacent_domains(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            def git(*args):
                return subprocess.check_output(['git', *args], cwd=root, stderr=subprocess.PIPE, text=True).strip()
            git('init', '-q')
            git('config', 'user.name', 'L2 unit fixture')
            git('config', 'user.email', 'l2@example.invalid')
            test = root / 'tests/Riftbound.ConformanceTests/UnexpectedName.cs'
            test.parent.mkdir(parents=True)
            test.write_text('public class ExistingConsumerTests {}\n')
            git('add', '.'); git('commit', '-qm', 'base')
            base = git('rev-parse', 'HEAD')
            test.write_text('public class ExistingConsumerTests { /* changed */ }\n')
            (test.parent / 'New.cs').write_text('public sealed class NewConsumerTests {}\n')
            (test.parent / 'FullGameEndToEndTests.cs').write_text('public class FullGameEndToEndTests {}\n')
            git('add', '.'); git('commit', '-qm', 'changed')
            head = git('rev-parse', 'HEAD')
            self.assertEqual(['ExistingConsumerTests', 'NewConsumerTests'], RUNNER.changed_classes(root, base, head))
            self.assertEqual([], RUNNER.changed_classes(root, '', head))
            with self.assertRaisesRegex(ValueError, 'checkout differs'):
                RUNNER.changed_classes(root, base, base)
        self.assertEqual({'payment', 'combat', 'triggers', 'recovery', 'recast'}, set(RUNNER.DOMAINS))
