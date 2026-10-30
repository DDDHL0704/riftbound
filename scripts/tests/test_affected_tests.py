import argparse
from contextlib import redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
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
            ('tests/Riftbound.ConformanceTests/PostgresMatchRecoveryStoreSmokeTests.cs',
             'var schema = File.ReadAllTextAsync(Path.Combine(root, "src", "schema.sql")); Assert.Equal(1, 1);'),
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
        self.assertNotIn('PostgresMatchRecoveryStoreSmokeTests', classes)
        self.assertEqual({'RAD·012/167'}, cards)

    def test_changed_test_nested_helpers_do_not_become_broad_filters(self):
        files = [
            ('tests/Riftbound.ConformanceTests/RunePrintingTests.cs',
             'public class RunePrintingTests { private class Driver {} '
             'private record Printing(string No); private class Store {} }'),
            ('tests/Riftbound.ConformanceTests/OtherDriverTests.cs',
             'public class OtherDriverTests {}'),
            ('tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs',
             'public class OfficialOpeningTests {}'),
        ]
        classes, _ = runner.select_affected_test_classes([files[0][0]], '', files)
        self.assertEqual({'RunePrintingTests', 'OfficialOpeningTests'}, classes)

    def test_changed_standalone_helper_selects_its_consumers(self):
        files = [
            ('tests/Riftbound.ConformanceTests/RuneTestDriver.cs',
             'internal class RuneTestDriver {}'),
            ('tests/Riftbound.ConformanceTests/RuneConsumerTests.cs',
             'public class RuneConsumerTests { RuneTestDriver driver = new(); }'),
            ('tests/Riftbound.ConformanceTests/UnrelatedTests.cs',
             'public class UnrelatedTests {}'),
            ('tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs',
             'public class OfficialOpeningTests {}'),
        ]
        classes, _ = runner.select_affected_test_classes([files[0][0]], '', files)
        self.assertEqual({'RuneConsumerTests', 'OfficialOpeningTests'}, classes)

    def test_consumer_suites_are_selected_without_selecting_nested_helpers(self):
        files = [
            ('tests/Riftbound.ConformanceTests/Ven122PhysicalWardSpellConsumers.cs',
             'public class Ven122PhysicalWardSpellConsumers { '
             '[Theory] public void PaysWard() {} private class Driver {} }'),
            ('tests/Riftbound.ConformanceTests/Ven010PaidCurrentGraveConsumers.cs',
             'public class Ven010PaidCurrentGraveConsumers { GraveyardRules rules; '
             '[Fact] public void UsesCurrentCount() {} }'),
            ('tests/Riftbound.ConformanceTests/Ven093And122SubmittedSelfConsumers.cs',
             'public class Ven093And122SubmittedSelfConsumers { const string No = "VEN·093"; '
             '[Fact] public void PlaysFromDeck() {} private class Store {} }'),
            ('tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs',
             'public class OfficialOpeningTests {}'),
        ]
        for paths, diff, sources, expected in [
            ([files[0][0]], '', {}, 'Ven122PhysicalWardSpellConsumers'),
            (['src/Riftbound.Engine/GraveyardRules.cs'], '',
             {'src/Riftbound.Engine/GraveyardRules.cs': 'public class GraveyardRules {}'},
             'Ven010PaidCurrentGraveConsumers'),
            (['data/cards.json'], '+ VEN·093\n', {}, 'Ven093And122SubmittedSelfConsumers'),
        ]:
            with self.subTest(expected=expected):
                classes, _ = runner.select_affected_test_classes(paths, diff, files, sources)
                self.assertEqual({expected, 'OfficialOpeningTests'}, classes)

    def test_affected_selection_requires_base_to_be_an_ancestor(self):
        failure = subprocess.CalledProcessError(1, ['git', 'merge-base'])
        with patch.object(runner, '_git_text', side_effect=['head-sha', 'base-sha']), \
             patch.object(runner.subprocess, 'run', side_effect=failure):
            with self.assertRaisesRegex(runner.AffectedBranchOutOfDate, 'sync origin/main'):
                runner.affected_selection('origin/main', repo=Path('/tmp/repo'))


class AffectedExecutionTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.output = Path(self.folder.name) / 'results'
        self.args = argparse.Namespace(base='origin/main', print_filter=False,
                                       configuration='Release', output=self.output, jobs=1)
        self.selection = {'baseSha': 'base', 'headSha': 'head', 'changedPaths': ['CardTests.cs'],
                          'classes': ['CardTests', 'OfficialOpeningTests'], 'cardTokens': [],
                          'filter': '(FullyQualifiedName~CardTests|FullyQualifiedName~OfficialOpeningTests)'}
        self.trx = ('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                    '<Results><UnitTestResult testName="CardTests.Case" outcome="Passed" /></Results>'
                    '<ResultSummary><Counters total="1" passed="1" failed="0" notExecuted="0" />'
                    '</ResultSummary></TestRun>')
        selection_patch = patch.object(runner, 'affected_selection', return_value=self.selection)
        selection_patch.start()
        self.addCleanup(selection_patch.stop)
        dotnet_patch = patch.object(runner.shutil, 'which', return_value='/path/limited-dotnet')
        dotnet_patch.start()
        self.addCleanup(dotnet_patch.stop)
        self.stdout = io.StringIO()

    def run_with_report(self, command, **kwargs):
        (self.output / 'results.trx').write_text(self.trx)
        return subprocess.CompletedProcess(command, 0)

    def test_one_filtered_invocation_builds_and_runs_with_visible_diagnostics(self):
        with patch.object(runner.subprocess, 'run', side_effect=self.run_with_report) as child, \
             redirect_stdout(self.stdout):
            self.assertEqual(0, runner.run_affected(self.args))
        child.assert_called_once()
        command = child.call_args.args[0]
        self.assertEqual(['/path/limited-dotnet', 'test', str(runner.PROJECT)], command[:3])
        self.assertEqual('Release', command[command.index('-c') + 1])
        self.assertIn('-m:1', command)
        self.assertNotIn('--no-build', command)
        self.assertNotIn('--no-restore', command)
        self.assertEqual(self.selection['filter'], command[command.index('--filter') + 1])
        self.assertEqual(str(self.output.resolve()), command[command.index('--results-directory') + 1])
        self.assertTrue(child.call_args.kwargs['check'])
        self.assertEqual(runner.ROOT, child.call_args.kwargs['cwd'])
        # Inherit stdout/stderr, so compilation errors remain visible while queued tests build.
        self.assertNotIn('stdout', child.call_args.kwargs)
        self.assertNotIn('stderr', child.call_args.kwargs)
        self.assertNotIn('capture_output', child.call_args.kwargs)
        self.assertEqual(self.selection, json.loads((self.output / 'selection.json').read_text()))

    def test_compile_or_test_failure_cannot_publish_a_passing_selection(self):
        failure = subprocess.CalledProcessError(1, ['dotnet', 'test'])
        with patch.object(runner.subprocess, 'run', side_effect=failure) as child, \
             redirect_stdout(self.stdout):
            with self.assertRaises(subprocess.CalledProcessError):
                runner.run_affected(self.args)
        child.assert_called_once()
        self.assertFalse((self.output / 'selection.json').exists())

    def test_skipped_test_cannot_publish_a_passing_selection(self):
        self.trx = self.trx.replace('outcome="Passed"', 'outcome="NotExecuted"') \
                           .replace('passed="1"', 'passed="0"') \
                           .replace('notExecuted="0"', 'notExecuted="1"')
        with patch.object(runner.subprocess, 'run', side_effect=self.run_with_report), \
             redirect_stdout(self.stdout):
            with self.assertRaisesRegex(ValueError, 'Tests did not all pass'):
                runner.run_affected(self.args)
        self.assertFalse((self.output / 'selection.json').exists())

    def test_print_filter_does_not_build_or_run_tests(self):
        self.args.print_filter = True
        with patch.object(runner.subprocess, 'run') as child, redirect_stdout(self.stdout):
            self.assertEqual(0, runner.run_affected(self.args))
        child.assert_not_called()
        self.assertEqual(self.selection['filter'], self.stdout.getvalue().strip())
        self.assertFalse(self.output.exists())


if __name__ == '__main__':
    unittest.main()
