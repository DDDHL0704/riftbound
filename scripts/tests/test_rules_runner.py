import importlib.util
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

spec=importlib.util.spec_from_file_location('rules_runner',Path(__file__).parents[1]/'test-rules.py')
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)

class RuleRunnerTests(unittest.TestCase):
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

if __name__=='__main__':unittest.main()
