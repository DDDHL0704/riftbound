import importlib.util
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location(
    'rules_runner', Path(__file__).parents[1] / 'test-rules.py')
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class AffectedRuleTests(unittest.TestCase):
    def test_changed_tests_source_types_cards_and_architecture_guards_are_selected(self):
        files = [
            ('tests/Riftbound.ConformanceTests/ChangedTests.cs',
             'public sealed class ChangedTests { [Fact] public void Case() {} }'),
            ('tests/Riftbound.ConformanceTests/FooTests.cs',
             'public sealed class FooTests { Foo value = new(); }'),
            ('tests/Riftbound.ConformanceTests/CardTests.cs',
             'public sealed class CardTests { const string No = "RAD·012/167"; }'),
            ('tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs',
             'public sealed class CardCatalogBaselineTests { var x = File.ReadAllText("catalog"); Assert.Contains("1089", x); }'),
            ('tests/Riftbound.ConformanceTests/HiddenInfoPrivacyGuardTests.cs',
             'public sealed class HiddenInfoPrivacyGuardTests { [Fact] public void Guard() {} }'),
            ('tests/Riftbound.ConformanceTests/ArchitectureTests.cs',
             'public sealed class ArchitectureTests { var x = SourceFileParts.ReadAllText("x"); Assert.DoesNotContain("bad", x); }'),
            ('tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs',
             'public sealed class OfficialOpeningTests { [Fact] public void Opening() {} }'),
        ]
        classes, cards = runner.select_affected_test_classes(
            ['tests/Riftbound.ConformanceTests/ChangedTests.cs', 'src/Riftbound.Engine/Foo.cs', 'data/cards.json'],
            '+ RAD·012/167\n', files, {'src/Riftbound.Engine/Foo.cs': 'public sealed class Foo {}'})
        self.assertIn('ChangedTests', classes)
        self.assertIn('FooTests', classes)
        self.assertIn('CardTests', classes)
        self.assertIn('CardCatalogBaselineTests', classes)
        self.assertIn('HiddenInfoPrivacyGuardTests', classes)
        self.assertIn('ArchitectureTests', classes)
        self.assertIn('OfficialOpeningTests', classes)
        self.assertEqual({'RAD·012/167'}, cards)

    def test_affected_selection_requires_base_to_be_an_ancestor(self):
        failure = subprocess.CalledProcessError(1, ['git', 'merge-base'])
        with patch.object(runner, '_git_text', side_effect=['head-sha', 'base-sha']), \
             patch.object(runner.subprocess, 'run', side_effect=failure):
            with self.assertRaisesRegex(runner.AffectedBranchOutOfDate, 'sync origin/main'):
                runner.affected_selection('origin/main', repo=Path('/tmp/repo'))


if __name__ == '__main__':
    unittest.main()
