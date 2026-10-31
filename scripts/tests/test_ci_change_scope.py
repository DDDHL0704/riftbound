import importlib.util
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('ci_scope', Path(__file__).parents[1] / 'ci-change-scope.py')
scope = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scope)


class ScopeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name)
        self.git('init', '-q')
        self.git('config', 'user.name', 'Scope Test')
        self.git('config', 'user.email', 'scope@example.invalid')
        self.write('README.md', 'original\n')
        self.base = self.commit()

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.repo, stderr=subprocess.PIPE).decode().strip()

    def write(self, name, text):
        path = self.repo / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)

    def commit(self):
        self.git('add', '--all')
        self.git('commit', '-qm', 'scope fixture')
        return self.git('rev-parse', 'HEAD')

    def decision(self, base=None):
        return scope.decide('push', {'before': base or self.base},
                            self.git('rev-parse', 'HEAD'), self.repo)

    def test_real_documentation_diff_uses_light_scope_and_records_inputs(self):
        self.write('README.md', 'new policy\n')
        head = self.commit()
        result = self.decision()
        self.assertFalse(result['full'])
        self.assertEqual(head, result['headSha'])
        self.assertEqual(self.base, result['baseSha'])
        self.assertEqual(['README.md'], [c['path'] for c in result['changes']])

    def test_backlog_and_developer_notes_are_docs_but_test_consumed_docs_are_not(self):
        for path in ['docs/BACKLOG.md', 'docs/CARD_BACKLOG.md', 'AGENTS.md',
                     'docs/agents/domain.md']:
            with self.subTest(path=path):
                self.write(path, 'updated task\n')
                self.commit()
                self.assertEqual('DOCS', self.decision()['tier'])
                self.git('reset', '--hard', self.base)
        for path in ['docs/DEPLOYMENT.md', 'docs/legacy/rules-evidence-index.md',
                     'docs/legacy/audit/current-manifest.json']:
            with self.subTest(path=path):
                self.write(path, 'changed test input\n')
                self.commit()
                self.assertNotEqual('DOCS', self.decision()['tier'])
                self.git('reset', '--hard', self.base)

    def test_require_docs_cli_rechecks_git_and_rejects_new_tests_even_when_full_is_false(self):
        script = self.repo / 'scripts/ci-change-scope.py'
        self.write(str(script.relative_to(self.repo)), Path(scope.__file__).read_text())
        self.base = self.commit()
        self.write('docs/BACKLOG.md', 'mark a task complete\n')
        head = self.commit()
        event = self.repo / '.git/docs-event.json'
        event.write_text(json.dumps({'before': self.base}))

        def run():
            output = self.repo / '.git/docs-decision.json'
            result = subprocess.run(['python3', str(script), '--event', str(event),
                                     '--event-name', 'push', '--head', head,
                                     '--require-docs', '--output', str(output)],
                                    capture_output=True, text=True)
            return result.returncode, json.loads(output.read_text())

        code, decision = run()
        self.assertEqual(0, code)
        self.assertEqual('DOCS', decision['tier'])
        self.write('tests/Riftbound.ConformanceTests/NewConsumerTests.cs', 'new test\n')
        head = self.commit()
        code, decision = run()
        self.assertNotEqual(0, code)
        self.assertEqual('L1', decision['tier'])
        self.assertFalse(decision['full'])

    def test_l2_main_docs_push_still_verifies_code_from_replaced_pending_runs(self):
        self.write('scripts/ci-change-scope.py', Path(scope.__file__).read_text())
        self.base = self.commit()
        self.write('docs/BACKLOG.md', 'latest pending main revision\n')
        head = self.commit()
        event, output = self.repo / '.git/event.json', self.repo / '.git/scope.json'
        for name, payload, tier in [('push', {'before': self.base}, 'L3'),
                                    ('pull_request', {'pull_request': {'base': {'sha': self.base}}}, 'DOCS')]:
            with self.subTest(event=name):
                event.write_text(json.dumps(payload))
                subprocess.run(['python3', str(self.repo / 'scripts/ci-change-scope.py'),
                                '--docs-pr-only', '--event', str(event), '--event-name', name,
                                '--head', head, '--output', str(output)], check=True, capture_output=True)
                result = json.loads(output.read_text())
                self.assertEqual(tier, result['tier'])
                self.assertEqual(tier != 'DOCS', result['full'])

    def test_main_docs_push_can_use_the_same_fail_closed_documentation_scope(self):
        self.write('scripts/ci-change-scope.py', Path(scope.__file__).read_text())
        self.base = self.commit()
        self.write('docs/BACKLOG.md', 'latest main documentation\\n')
        head = self.commit()
        event = self.repo / '.git/event.json'
        output = self.repo / '.git/scope.json'
        event.write_text(json.dumps({'before': self.base}))
        subprocess.run([
            'python3', str(self.repo / 'scripts/ci-change-scope.py'),
            '--docs-pr-only', '--allow-main-docs', '--event', str(event),
            '--event-name', 'push', '--head', head, '--output', str(output),
        ], check=True, capture_output=True)
        result = json.loads(output.read_text())
        self.assertEqual('DOCS', result['tier'])
        self.assertFalse(result['full'])
        self.assertEqual('NOT_RUN_DOCUMENTATION_ONLY', result['ruleAndPackagingExecution'])

    def test_unknown_inputs_never_inherit_a_markdown_or_directory_exemption(self):
        paths = ['src/Rule.cs', 'docs/DEPLOYMENT.md', 'docs/NewPolicy.md',
                 'docs/legacy/evidence/ledger.json', 'data/official/card-catalog.json',
                 'tests/Fixtures/example.json', 'clients/godot/README.md',
                 'scripts/test-rules.py', '.github/workflows/native-desktop.yml']
        for path in paths:
            with self.subTest(path=path):
                self.write(path, 'changed\n')
                self.write('README.md', path+'\n')
                head = self.commit()
                self.assertTrue(self.decision()['full'])
                self.git('reset', '--hard', self.base)
                self.assertNotEqual(self.base, head)

    def test_rename_is_checked_at_both_paths(self):
        self.git('mv', 'README.md', 'unreviewed.md')
        self.commit()
        result = self.decision()
        self.assertTrue(result['full'])
        self.assertEqual({'README.md', 'unreviewed.md'}, {c['path'] for c in result['changes']})
        self.assertEqual({'A', 'D'}, {c['status'] for c in result['changes']})

    @unittest.skipIf(os.name == 'nt', 'POSIX mode and symlink changes')
    def test_documentation_symlink_and_executable_mode_require_full(self):
        path = self.repo / 'README.md'
        path.chmod(0o755)
        self.commit()
        self.assertTrue(self.decision()['full'])
        self.git('reset', '--hard', self.base)
        path.unlink()
        path.symlink_to('external-input.md')
        self.commit()
        self.assertTrue(self.decision()['full'])

    def test_unknown_base_empty_diff_and_mismatched_checkout_require_full(self):
        head = self.git('rev-parse', 'HEAD')
        for base in ['0'*40, '1'*40, 'invalid', head, None, 42]:
            with self.subTest(base=base):
                self.assertTrue(scope.decide('push', {'before': base}, head, self.repo)['full'])
        self.assertTrue(scope.decide('push', {'before': self.base}, '2'*40, self.repo)['full'])

    def test_force_push_with_nonancestor_base_requires_full(self):
        self.write('README.md', 'old branch\n')
        old = self.commit()
        self.git('reset', '--hard', self.base)
        self.write('README.md', 'replacement branch\n')
        self.commit()
        self.assertTrue(self.decision(old)['full'])

    def test_pr_base_is_used_and_manual_gate_always_runs_full(self):
        self.write('README.md', 'policy\n')
        head = self.commit()
        event = {'pull_request': {'base': {'sha': self.base}}}
        self.assertFalse(scope.decide('pull_request', event, head, self.repo)['full'])
        self.assertTrue(scope.decide('workflow_dispatch', event, head, self.repo)['full'])

    def test_required_gate_checkout_can_prove_a_docs_merge_against_its_base(self):
        self.git('checkout', '-qb', 'docs-change')
        self.write('docs/BACKLOG.md', 'complete a task\n')
        self.commit()
        self.git('checkout', '-qB', 'candidate', self.base)
        self.git('merge', '--no-ff', '--no-edit', 'docs-change')
        head = self.git('rev-parse', 'HEAD')
        event = {'pull_request': {'base': {'sha': self.base}}}
        workflow = (Path(__file__).parents[2] / '.github/workflows/backend-l2.yml').read_text()
        checkout = workflow.split('  l2_required:\n', 1)[1].split('      - name:', 1)[0]
        configured = re.search(r'fetch-depth: (\d+)', checkout)
        depth = int(configured[1]) if configured else 1
        for name, clone_depth, expected in [('required-gate', depth, 'DOCS'),
                                            ('shallow-negative-control', 1, None)]:
            with self.subTest(checkout=name, depth=clone_depth):
                clone = self.repo.parent / (self.repo.name + '-' + name)
                self.addCleanup(shutil.rmtree, clone)
                subprocess.run(['git', 'clone', '-q', '--depth=' + str(clone_depth),
                                self.repo.as_uri(), str(clone)], check=True,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE)
                result = scope.decide('pull_request', event, head, clone, allow_fetch=True)
                if expected:
                    self.assertEqual(expected, result.get('tier'), result)
                    self.assertFalse(result['full'])
                else:
                    self.assertTrue(result['full'], result)

    def test_early_recheck_cli_uses_full_suite_when_history_disappears(self):
        self.write('scripts/ci-change-scope.py', Path(scope.__file__).read_text())
        self.base = self.commit()
        self.git('checkout', '-qb', 'docs-change')
        self.write('docs/BACKLOG.md', 'complete a task\n')
        self.commit()
        self.git('checkout', '-qB', 'candidate', self.base)
        self.git('merge', '--no-ff', '--no-edit', 'docs-change')
        head = self.git('rev-parse', 'HEAD')
        event = self.repo / '.git/event.json'
        event.write_text(json.dumps({'pull_request': {'base': {'sha': self.base}}}))
        previous = self.repo / '.git/scope-candidate.json'
        command = ['python3', str(self.repo / 'scripts/ci-change-scope.py'), '--event', str(event),
                   '--event-name', 'pull_request', '--head', head, '--output', str(previous)]
        subprocess.run(command, check=True, capture_output=True)
        self.assertEqual('DOCS', json.loads(previous.read_text())['tier'])
        clone = self.repo.parent / (self.repo.name + '-early-recheck')
        self.addCleanup(shutil.rmtree, clone)
        subprocess.run(['git', 'clone', '-q', '--depth=1', self.repo.as_uri(), str(clone)],
                       check=True, capture_output=True)
        output, github_output = clone / '.git/scope.json', clone / '.git/github-output'
        command[1] = str(clone / 'scripts/ci-change-scope.py')
        command[-1] = str(output)
        subprocess.run(command + ['--recheck', str(previous), '--github-output', str(github_output)],
                       check=True, capture_output=True)
        decision = json.loads(output.read_text())
        self.assertEqual(('L3', True, 'full'), (decision['tier'], decision['full'], decision['profile']))
        self.assertIn('tier=L3', github_output.read_text())
        self.assertIn('full=true', github_output.read_text())

    def docs_receipt(self):
        self.write('docs/BACKLOG.md', 'completed a task\n')
        head = self.commit()
        event = {'before': self.base}
        decision = self.decision()
        decision.update(scope.scope_context('push', event, head),
                        ruleAndPackagingExecution='NOT_RUN_DOCUMENTATION_ONLY')
        directory = self.repo / '.git/docs-artifact'
        directory.mkdir()
        previous = directory / 'scope-candidate.json'
        previous.write_text(json.dumps(decision))
        final = scope.recheck(dict(decision), previous)
        (directory / 'scope.json').write_text(json.dumps(final))
        checkout = dict(status='FIXED_CANDIDATE_CHECKOUT_VERIFIED', actualSha=head, expectedSha=head,
                        eventSha=head, treeSha=self.git('rev-parse', 'HEAD^{tree}'),
                        sourceIndexSha256=hashlib.sha256(scope.git(self.repo, 'ls-tree', '-rz', 'HEAD')).hexdigest(),
                        runId=os.environ.get('GITHUB_RUN_ID'), runAttempt=os.environ.get('GITHUB_RUN_ATTEMPT'))
        (directory / 'checkout.json').write_text(json.dumps(checkout))
        gate = self.repo / '.git/gate-checkout.json'
        gate.write_text(json.dumps(checkout))
        return directory, gate, event, head

    @patch.dict(os.environ, {'GITHUB_RUN_ID': '123', 'GITHUB_RUN_ATTEMPT': '2'})
    def test_recheck_preserves_docs_but_defaults_to_full_for_missing_stale_or_invalid_input(self):
        directory, _, _, _ = self.docs_receipt()
        path = directory / 'scope-candidate.json'
        original = path.read_bytes()
        current = json.loads(original)
        self.assertEqual('DOCS', scope.recheck(dict(current), path)['tier'])
        for value in [b'invalid json', b'[]', json.dumps(dict(current, headSha='2'*40)).encode(),
                      json.dumps(dict(current, baseSha='3'*40)).encode(),
                      json.dumps(dict(current, tier='L3', full=True)).encode()]:
            with self.subTest(value=value):
                path.write_bytes(value)
                result = scope.recheck(dict(current), path)
                self.assertEqual(('L3', True, 'full'), (result['tier'], result['full'], result['profile']))
        path.unlink()
        self.assertTrue(scope.recheck(dict(current), path)['full'])

    @patch.dict(os.environ, {'GITHUB_RUN_ID': '123', 'GITHUB_RUN_ATTEMPT': '2'})
    def test_docs_gate_checks_fixed_receipts_without_a_second_git_scope_classification(self):
        directory, gate, event, head = self.docs_receipt()
        with patch.object(scope, 'git', side_effect=AssertionError('No late Git scope selection')):
            result = scope.verify_docs_receipt(directory, gate, 'push', event, head)
        self.assertEqual('DOCUMENTATION_EXEMPTION_VERIFIED', result['status'])

    @patch.dict(os.environ, {'GITHUB_RUN_ID': '123', 'GITHUB_RUN_ATTEMPT': '2'})
    def test_docs_gate_rejects_tampered_cross_input_and_missing_receipts(self):
        directory, gate, event, head = self.docs_receipt()
        paths = [directory / name for name in ['scope-candidate.json', 'scope.json', 'checkout.json']] + [gate]
        originals = {path: path.read_bytes() for path in paths}
        mutations = [
            (paths[0], 'baseSha', '3'*40), (paths[0], 'changes', []),
            (paths[1], 'headSha', '2'*40), (paths[1], 'eventSha256', 'stale'),
            (paths[1], 'selectorSha256', 'old-selector'), (paths[1], 'priorScopeSha256', 'unlinked'),
            (paths[1], 'full', True), (paths[1], 'runId', '124'), (paths[1], 'runAttempt', '1'),
            (paths[1], 'changes', [dict(path='src/Rule.cs', oldMode='100644', newMode='100644', status='M')]),
            (paths[2], 'actualSha', '2'*40), (paths[2], 'runId', '124'),
            (paths[3], 'treeSha', '3'*40), (paths[3], 'sourceIndexSha256', 'different-tree'),
            (paths[3], 'status', 'FIXED_CANDIDATE_CHECKOUT_REJECTED'),
        ]
        for path, key, value in mutations:
            with self.subTest(file=path.name, key=key):
                data = json.loads(originals[path]); data[key] = value
                path.write_text(json.dumps(data))
                with self.assertRaises(ValueError):
                    scope.verify_docs_receipt(directory, gate, 'push', event, head)
                path.write_bytes(originals[path])
        for path in paths:
            with self.subTest(missing=path):
                path.unlink()
                with self.assertRaises(OSError):
                    scope.verify_docs_receipt(directory, gate, 'push', event, head)
                path.write_bytes(originals[path])
        with self.assertRaises(ValueError):
            scope.verify_docs_receipt(directory, gate, 'push', {'before': '3'*40}, head)
        with self.assertRaises(ValueError):
            scope.verify_docs_receipt(directory, gate, 'workflow_dispatch', event, head)

    def test_malformed_events_and_raw_diff_fail_closed(self):
        head = self.git('rev-parse', 'HEAD')
        for event in [None, [], {'pull_request': None}, {'pull_request': {'base': []}}]:
            with self.subTest(event=event):
                self.assertTrue(scope.decide('pull_request', event, head, self.repo)['full'])
        for raw in [b':100644 100644 x y M\0README.md\0',
                    b':100644 100644 abc def R100\0README.md\0next.md\0',
                    b'broken', b'\xff\0']:
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                scope.parse_changes(raw)

    def test_safe_new_tests_and_backend_changes_have_explicit_bounded_tiers(self):
        def change(path, status='M', old='100644', new='100644'):
            return dict(path=path, status=status, oldMode=old, newMode=new)
        c = change('tests/Riftbound.ConformanceTests/NewConsumerTests.cs', 'A', '000000')
        l1 = scope.tier_details([c])
        self.assertEqual(('L1', 'focus', ['NewConsumerTests']), (l1['tier'], l1['profile'], l1['matches']))
        self.assertFalse(l1['full']); self.assertFalse(l1['sql'])
        l2 = scope.tier_details([c, change('src/Riftbound.Engine/CoreRuleEngine.cs')])
        self.assertEqual(('L2', 'check'), (l2['tier'], l2['profile']))
        self.assertTrue(l2['sql']); self.assertTrue(l2['network']); self.assertFalse(l2['full'])
        for unsafe in [change(c['path']), change('tests/Riftbound.ConformanceTests/FullGameEndToEndTests.NewTests.cs', 'A', '000000'),
                       change('src/Riftbound.Engine/Rule.cs', 'D', '100644', '000000'),
                       change('src/Riftbound.Engine/Rule.cs', new='120000'),
                       change('data/official/card-catalog.zh-CN.json'), change('scripts/test-rules.py')]:
            with self.subTest(unsafe=unsafe): self.assertTrue(scope.tier_details([unsafe])['full'])
        self.assertTrue(scope.tier_details([c, change('unknown.txt')])['full'])

    def test_nightly_and_manual_never_inherit_scoped_shortcuts(self):
        head = self.git('rev-parse', 'HEAD')
        for event in ['schedule', 'workflow_dispatch']:
            self.assertTrue(scope.decide(event, {'before': self.base}, head, self.repo)['full'])

    def test_cli_unreadable_event_emits_full_in_github_output(self):
        event = self.repo / 'event.json'
        event.write_text('invalid json')
        output = self.repo / 'scope.json'
        github_output = self.repo / 'github-output'
        subprocess.run(['python3', str(Path(scope.__file__)), '--event', str(event),
                        '--head', self.base, '--output', str(output),
                        '--github-output', str(github_output)], check=True,
                       stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.assertTrue(json.loads(output.read_text())['full'])
        flags = dict(line.split('=', 1) for line in github_output.read_text().splitlines())
        self.assertEqual('true', flags['full'])
        self.assertEqual('L3', flags['tier'])
        self.assertTrue(all(flags[k] == 'true' for k in ['sql', 'network', 'packaging']))


if __name__ == '__main__':
    unittest.main()
