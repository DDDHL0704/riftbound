import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

spec=importlib.util.spec_from_file_location('rules_runner',Path(__file__).parents[1]/'test-rules.py')
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)

PASSING_TRX = '''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
<Results>
<UnitTestResult testName="Riftbound.ConformanceTests.Rules.Theory(a: 1)" outcome="Passed" />
<UnitTestResult testName="Riftbound.ConformanceTests.Rules.Theory(a: 2)" outcome="Passed" />
</Results>
<ResultSummary><Counters total="2" passed="2" failed="0" notExecuted="0" /></ResultSummary>
</TestRun>'''

class RuleRunnerTests(unittest.TestCase):
    def report_xml(self, text):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'results.trx'
            path.write_text(text)
            return runner.report(path)

    def test_trx_requires_explicit_result_counters(self):
        for attribute in ('total="2"', 'passed="2"', 'failed="0"', 'notExecuted="0"'):
            with self.subTest(missing=attribute), self.assertRaises(ValueError):
                rows, counters = self.report_xml(PASSING_TRX.replace(attribute, ''))
                runner.verify(rows, counters, {'Riftbound.ConformanceTests.Rules.Theory'})

    def test_real_trx_theory_rows_preserve_case_counts_and_method_coverage(self):
        rows, counters = self.report_xml(PASSING_TRX)
        self.assertEqual(2, len(rows))
        self.assertEqual(dict(total=2, passed=2, failed=0, notExecuted=0), counters)
        runner.verify(rows, counters, {'Riftbound.ConformanceTests.Rules.Theory'})

    def test_empty_malformed_or_inconsistent_trx_fails_closed(self):
        for text in ('', '<TestRun>', PASSING_TRX.replace('total="2"', 'total="999"'),
                     PASSING_TRX.replace('passed="2"', 'passed="1"'),
                     PASSING_TRX.replace('outcome="Passed"', 'outcome="Failed"'),
                     PASSING_TRX.replace('outcome="Passed"', 'outcome="NotExecuted"')):
            with self.subTest(text=text), self.assertRaises((ValueError, ET.ParseError)):
                rows, counters = self.report_xml(text)
                runner.verify(rows, counters, {'Riftbound.ConformanceTests.Rules.Theory'})

    def test_full_starts_ordinary_rules_and_longest_game_in_first_wave(self):
        ordinary=runner.PREFIX+'Rules.EarlyFailure'
        longest=runner.FULL_CLASS+'LongMatrix'
        methods={ordinary,longest,*(runner.FULL_CLASS+str(i) for i in range(12))}
        shards=runner.plan(methods,'full',4,[],{longest:1000})
        self.assertEqual('rules',shards[0]['name'])
        self.assertEqual({ordinary},shards[0]['methods'])
        self.assertEqual({longest},shards[1]['methods'])
        assigned=[name for shard in shards for name in shard['methods']]
        self.assertEqual(methods,set(assigned))
        self.assertEqual(len(methods),len(assigned))

    def test_full_assigns_every_method_once_including_new_untimed_tests(self):
        ordinary=runner.PREFIX+'NewRuleTests.NewCase'
        methods={ordinary,*(runner.FULL_CLASS+str(i) for i in range(9))}
        shards=runner.plan(methods,'full',3,[],{runner.FULL_CLASS+'0':300})
        actual=[name for shard in shards for name in shard['methods']]
        self.assertEqual(methods,set(actual));self.assertEqual(len(methods),len(actual))
        self.assertTrue(any(s['methods']=={runner.FULL_CLASS+'0'} for s in shards))

    def test_theory_discovery_and_runtime_rows_share_method_coverage(self):
        text='以下测试可用:\n    '+runner.PREFIX+'Rules.Theory(a: 1)\n    '+runner.PREFIX+'Rules.Theory(a: 2)\n'
        expected=runner.discover(text)
        rows=[ET.Element('result',testName=runner.PREFIX+'Rules.Theory(a: '+str(i)+')',outcome='Passed') for i in range(3)]
        runner.verify(rows,dict(total=3,passed=3,failed=0,notExecuted=0),expected)

    def test_unrecognized_discovery_and_zero_focus_fail_closed(self):
        with self.assertRaises(ValueError):runner.discover('以下测试可用:\n    Custom display name\n')
        with self.assertRaises(ValueError):runner.plan({runner.PREFIX+'A.Test'},'focus',1,['NotPresent'],{})

    def test_missing_test_and_skip_cannot_report_success(self):
        row=ET.Element('result',testName=runner.PREFIX+'A.Test',outcome='Passed')
        with self.assertRaises(ValueError):runner.verify([row],dict(total=1,passed=1,failed=0,notExecuted=0),{runner.PREFIX+'B.Test'})
        row.set('outcome','NotExecuted')
        with self.assertRaises(ValueError):runner.verify([row],dict(total=1,passed=0,failed=0,notExecuted=1),{runner.PREFIX+'A.Test'})

    def test_focus_excludes_expensive_games_even_when_name_matches(self):
        small=runner.PREFIX+'RecastTests.A'
        shards=runner.plan({small,runner.FULL_CLASS+'Recast'},'focus',4,['recast'],{})
        self.assertEqual({small},shards[0]['methods'])

    def test_explicit_game_focus_runs_only_matching_methods_and_checks_coverage(self):
        game=runner.FULL_CLASS+'PreconstructedCatalogDecks'
        methods={game,runner.FULL_CLASS+'OtherGame',runner.PREFIX+'Rules.OtherRule'}
        shard=runner.plan(methods,'focus',4,['PreconstructedCatalogDecks'],{},include_games=True)[0]
        self.assertEqual({game},shard['methods'])
        self.assertNotIn('FullyQualifiedName!~',shard['filter'])
        row=ET.Element('result',testName=game,outcome='Passed')
        runner.verify([row],dict(total=1,passed=1,failed=0,notExecuted=0),shard['methods'])
        with self.assertRaises(ValueError):
            runner.verify([row],dict(total=1,passed=1,failed=0,notExecuted=0),methods)

    def test_check_requires_named_smoke_tests(self):
        with self.assertRaises(ValueError):runner.plan({runner.PREFIX+'A.Test'},'check',4,[],{})

    def test_l2_defers_exact_full_opening_identity_and_full_still_contains_every_method(self):
        deferred = runner.PREFIX + 'OpeningTests.FullJournalRecovery'
        fast = runner.PREFIX + 'RecoveryTests.MechanismBoundary'
        new_untagged = runner.PREFIX + 'OpeningTests.NewUnclassifiedCase'
        matrix = runner.FULL_CLASS + 'OriginalMatrix'
        smoke = {runner.FULL_CLASS + name for name in runner.SMOKE}
        methods = {deferred, fast, new_untagged, matrix} | smoke
        with patch.object(runner, 'l3_acceptance_methods', return_value={deferred}):
            l2 = runner.plan(methods, 'l2', 2, [], {})[0]
            self.assertEqual({fast, new_untagged} | smoke, l2['methods'])
            self.assertIn('FullyQualifiedName!=' + deferred, l2['filter'])
            full = runner.plan(methods, 'full', 4, [], {})
            all_full = [name for shard in full for name in shard['methods']]
            self.assertEqual(methods, set(all_full))
            self.assertEqual(len(methods), len(all_full))
            self.assertEqual({deferred, matrix}, methods - l2['methods'])
            focused = runner.plan(methods, 'focus', 2, ['Recovery'], {}, l2_only=True)[0]
            self.assertEqual({fast}, focused['methods'])
            with self.assertRaises(ValueError):
                runner.plan(methods - {deferred}, 'l2', 2, [], {})

    def test_l2_adds_only_the_available_bounded_conquest_prefix_to_both_plan_and_filter(self):
        bounded = next(iter(runner.L2_BOUNDED_GAMES))
        ordinary = runner.PREFIX + 'RecoveryTests.MechanismBoundary'
        other_game = runner.FULL_CLASS + 'OriginalFullVictory'
        smoke = {runner.FULL_CLASS + name for name in runner.SMOKE}
        methods = {ordinary, other_game, bounded} | smoke
        with patch.object(runner, 'l3_acceptance_methods', return_value=set()):
            selected = runner.l2_rule_methods(methods)
            self.assertEqual({ordinary, bounded} | smoke, selected)
            l2 = runner.plan(methods, 'l2', 2, [], {})[0]
            self.assertEqual(selected, l2['methods'])
            self.assertIn('FullyQualifiedName=' + bounded, l2['filter'])
            self.assertNotIn('FullyQualifiedName=' + other_game, l2['filter'])
            self.assertNotIn(bounded, runner.plan(methods, 'check', 2, [], {})[0]['methods'])
            self.assertEqual({ordinary} | smoke, runner.l2_rule_methods(methods - {bounded}))
            full = runner.plan(methods, 'full', 2, [], {})
            self.assertEqual(methods, {name for shard in full for name in shard['methods']})
        with patch.object(runner, 'l3_acceptance_methods', return_value={bounded}):
            with self.assertRaisesRegex(ValueError, 'bounded L2 method'):
                runner.l2_rule_methods(methods)

    def test_declared_l3_methods_resolve_to_preserved_source_methods(self):
        import json
        rows = json.loads(runner.L3_POLICY.read_text())['methods']
        names = runner.l3_acceptance_methods()
        self.assertEqual(18, len(names))
        for row in rows:
            self.assertIn(row['method'], names)
            text = (runner.ROOT / row['source']).read_text()
            self.assertIn(row['method'].rsplit('.', 1)[-1] + '(', text)

if __name__=='__main__':unittest.main()
