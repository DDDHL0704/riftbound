import importlib.util
import hashlib
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
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
    def test_duration_history_keeps_method_totals_and_optional_exact_case_costs(self):
        method = runner.PREFIX + 'Rules.Theory'
        case = method + '(a: 1)'
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'timings.json'
            path.write_text(json.dumps({'weights': {method: 3600}}))
            self.assertEqual({method: 3600}, runner.load_weights(path))
            path.write_text(json.dumps({'weights': {method: 3600}, 'caseWeights': {case: 3400}}))
            self.assertEqual({method: 3600, case: 3400}, runner.load_weights(path))

    def run_with_wrapper(self, github_actions='', extra_args=(), dotnet_root='/inherited/sdk'):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            project = root / 'tests/Riftbound.ConformanceTests'
            for configuration in ('Debug', 'Release'):
                assembly = project / f'bin/{configuration}/net10.0/Riftbound.ConformanceTests.dll'
                assembly.parent.mkdir(parents=True)
                assembly.write_bytes(configuration.encode())
            configuration = extra_args[extra_args.index('--configuration') + 1] if '--configuration' in extra_args else 'Release'
            local = root / 'home/.dotnet/dotnet'
            local.parent.mkdir(parents=True)
            local.touch()
            wrapper = str(root / 'bin/dotnet')
            output = root / 'output'
            discovery = 'Available tests:\n    ' + runner.PREFIX + 'Rules.Theory(a: 1)\n'
            discovery += '    ' + runner.PREFIX + 'Rules.Theory(a: 2)\n'

            def start_test(command, **kwargs):
                directory = Path(command[command.index('--results-directory') + 1])
                (directory / 'results.trx').write_text(PASSING_TRX)
                return SimpleNamespace(poll=lambda: 0)

            with patch.object(runner, 'ROOT', root), patch.object(runner, 'PROJECT', project), \
                    patch.object(runner.Path, 'home', return_value=root / 'home'), \
                    patch.object(runner.shutil, 'which', return_value=wrapper), \
                    patch.object(runner.os, 'cpu_count', return_value=10), \
                    patch.dict(runner.os.environ, {'GITHUB_ACTIONS': github_actions,
                                                  **({'DOTNET_ROOT': dotnet_root} if dotnet_root is not None else {})}), \
                    patch.object(runner, 'load_weights', return_value={}), \
                    patch.object(runner.subprocess, 'run', return_value=SimpleNamespace(stdout=discovery, stderr='')) as run, \
                    patch.object(runner.subprocess, 'Popen', side_effect=start_test) as popen, \
                    patch.object(runner.sys, 'argv', ['test-rules.py', 'focus', '--match', 'Rules.Theory',
                                                   '--output', str(output), *extra_args]):
                if dotnet_root is None:
                    runner.os.environ.pop('DOTNET_ROOT', None)
                self.assertEqual(0, runner.main())
            self.assertEqual('PASSED', json.loads((output / 'summary.json').read_text())['status'])
            for call in [*run.call_args_list, *popen.call_args_list]:
                self.assertEqual(wrapper, call.args[0][0])
                self.assertIn('-m:1', call.args[0])
                self.assertEqual(configuration, call.args[0][call.args[0].index('-c') + 1])
                if dotnet_root is None:
                    self.assertNotIn('DOTNET_ROOT', call.kwargs['env'])
                else:
                    self.assertEqual(dotnet_root, call.kwargs['env']['DOTNET_ROOT'])
            test_command = popen.call_args.args[0]
            self.assertIn('--filter', test_command)
            self.assertIn('FullyQualifiedName~Rules.Theory', test_command[test_command.index('--filter') + 1])
            plan = json.loads((output / 'plan.json').read_text())
            self.assertEqual(configuration, plan['configuration'])
            self.assertEqual(hashlib.sha256(configuration.encode()).hexdigest(), plan['testAssemblySha256'])
            return plan

    def test_path_wrapper_and_inherited_sdk_are_used_for_every_filtered_command(self):
        self.assertEqual(1, self.run_with_wrapper()['jobs'])

    def test_wrapper_directory_is_not_invented_as_sdk_root(self):
        self.assertEqual(1, self.run_with_wrapper(dotnet_root=None)['jobs'])

    def test_ci_keeps_parallel_default_and_explicit_jobs_still_take_precedence(self):
        self.assertEqual(4, self.run_with_wrapper(github_actions='true')['jobs'])
        self.assertEqual(1, self.run_with_wrapper(github_actions='true', extra_args=['--jobs', '1'])['jobs'])

    def test_debug_override_builds_discovers_executes_and_hashes_the_same_configuration(self):
        self.assertEqual('Debug', self.run_with_wrapper(extra_args=['--configuration', 'Debug'])['configuration'])

    def test_missing_path_sdk_does_not_fall_back_to_home_install(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            local = root / '.dotnet/dotnet'
            local.parent.mkdir()
            local.touch()
            with patch.object(runner.Path, 'home', return_value=root), \
                    patch.object(runner.shutil, 'which', return_value=None), \
                    patch.object(runner.subprocess, 'run') as run, \
                    patch.object(runner.sys, 'argv', ['test-rules.py', 'focus', '--match', 'Rules',
                                                   '--output', str(root / 'output')]):
                with self.assertRaisesRegex(ValueError, 'not found on PATH'):
                    runner.main()
                run.assert_not_called()

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

    def test_l2_requires_academy_echo_and_both_original_complete_games(self):
        required = {runner.FULL_CLASS + name for name in [
            'OfficialLowCurveDecksReachScoreVictoryAfterRealBattleThroughServerPrompts',
            'OfficialDeckMidgamePaysRumbleReducedGraveyardMechanicalPaymentAndScoreVictoryActionLogReplaysToFinalStateHash',
            'OfficialDeckMidgameResolvesPiltoverAcademyHeldNextSpellEchoAndScoreVictoryActionLogReplaysToFinalStateHash',
        ]}
        methods = required | {runner.PREFIX + 'Rules.Ordinary', runner.FULL_CLASS + 'OtherFullGame'}
        with patch.object(runner, 'l3_acceptance_methods', return_value=set()):
            for profile in ['check', 'l2']:
                planned = runner.plan(methods, profile, 1, [], {})[0]
                self.assertTrue(required <= planned['methods'])
                for name in required:
                    self.assertIn('FullyQualifiedName=' + name, planned['filter'])
                    with self.assertRaisesRegex(ValueError, 'required smoke test'):
                        runner.plan(methods - {name}, profile, 1, [], {})
            full = [name for shard in runner.plan(methods, 'full', 2, [], {}) for name in shard['methods']]
            self.assertEqual(methods, set(full))
            self.assertEqual(len(methods), len(full))

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
