import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('l2_targeted', ROOT / 'scripts/run-l2-targeted.py')
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)


class TargetSelectionTests(unittest.TestCase):
    def test_generated_command_is_accepted_by_the_actual_rule_runner_cli(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            project = root / 'tests'
            assembly = project / 'bin/Release/net10.0/Riftbound.ConformanceTests.dll'
            assembly.parent.mkdir(parents=True)
            assembly.write_bytes(b'synthetic parser-only unit fixture')
            discovery = '    Riftbound.ConformanceTests.NewConsumerTests.ActualAssertion\n'
            command = RUNNER.command_for(['NewConsumerTests'])
            args = command[1:] + ['--plan-only', '--output', str(root / 'plan')]
            with patch.object(RUNNER.RULES, 'ROOT', root), patch.object(RUNNER.RULES, 'PROJECT', project), \
                 patch.object(RUNNER.RULES, 'load_weights', return_value={}), \
                 patch.object(RUNNER.RULES, 'l3_acceptance_methods', return_value=set()), \
                 patch.object(RUNNER.RULES.subprocess, 'run', return_value=SimpleNamespace(stdout=discovery, stderr='')), \
                 patch.object(RUNNER.RULES.shutil, 'which', return_value='/synthetic/dotnet'), \
                 patch('sys.argv', args):
                self.assertEqual(0, RUNNER.RULES.main())
            self.assertTrue((root / 'plan/plan.json').is_file())
            self.assertFalse((root / 'plan/summary.json').exists())

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
