import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('retained_commit', ROOT/'scripts/ensure-retained-ci-commit.py')
RETAINED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RETAINED)


class RetainedCommitTests(unittest.TestCase):
    def test_retries_timeout_and_transport_failure_for_the_same_fixed_objects(self):
        objects = ['a'*40, 'b'*40]
        errors = [subprocess.TimeoutExpired('git', 120),
                  subprocess.CalledProcessError(128, 'git'), None]
        with patch.object(RETAINED.subprocess, 'run', side_effect=errors) as run:
            RETAINED.fetch(ROOT, objects)
        self.assertEqual(3, run.call_count)
        self.assertEqual(run.call_args_list[0], run.call_args_list[2])
        self.assertEqual('\n'.join(objects)+'\n', run.call_args.kwargs['input'])
        self.assertEqual(120, run.call_args.kwargs['timeout'])
        self.assertIn('--filter=blob:none', run.call_args.args[0])

    def test_exhausted_fetch_fails_closed_without_a_different_commit(self):
        error = subprocess.TimeoutExpired('git', 120)
        with patch.object(RETAINED.subprocess, 'run', side_effect=error) as run:
            with self.assertRaises(subprocess.TimeoutExpired):
                RETAINED.fetch(ROOT, ['c'*40], shallow=True)
        self.assertEqual(3, run.call_count)
        self.assertIn('--depth=1', run.call_args.args[0])
        self.assertEqual('c'*40+'\n', run.call_args.kwargs['input'])

    def test_full_identity_is_required_before_any_git_or_fetch(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            baseline = root/'docs/audit/legacy-source-baseline.json'
            baseline.parent.mkdir(parents=True)
            for identity in ['main', 'a'*8, '$(false)', 1, None]:
                with self.subTest(identity=identity):
                    baseline.write_text(json.dumps(dict(commit=identity)))
                    with patch.object(RETAINED.subprocess, 'run') as run:
                        with self.assertRaises(ValueError):
                            RETAINED.ensure(root)
                        run.assert_not_called()

    def test_filtered_fetch_reads_original_pins_and_excludes_unreferenced_evidence(self):
        with tempfile.TemporaryDirectory() as folder:
            upstream, checkout = Path(folder)/'upstream', Path(folder)/'checkout'
            upstream.mkdir(); checkout.mkdir()

            def git(root, *args):
                return subprocess.check_output(['git', *args], cwd=root, stderr=subprocess.PIPE).decode().strip()

            git(upstream, 'init', '-q')
            git(upstream, 'config', 'uploadpack.allowFilter', 'true')
            git(upstream, 'config', 'uploadpack.allowAnySHA1InWant', 'true')
            manifest_path = upstream/'docs/audit/current-manifest.json'
            manifest_path.parent.mkdir(parents=True)
            content = b'original retained source\n'
            (upstream/'source.txt').write_bytes(content)
            (upstream/'unreferenced-evidence.bin').write_bytes(b'large historical evidence'*10000)
            manifest_path.write_text(json.dumps(dict(inputs=[dict(path='source.txt',
                sha256=hashlib.sha256(content).hexdigest()), dict(path='absent-must-not-be-forged')]))+'\n')
            manifest = manifest_path.read_bytes()
            git(upstream, 'add', '.')
            git(upstream, '-c', 'user.name=CI fixture', '-c', 'user.email=fixture@example.invalid',
                '-c', 'commit.gpgsign=false', 'commit', '-qm', 'retained')
            commit = git(upstream, 'rev-parse', 'HEAD')
            excluded = git(upstream, 'rev-parse', 'HEAD:unreferenced-evidence.bin')
            git(checkout, 'init', '-q')
            git(checkout, 'remote', 'add', 'origin', upstream.as_uri())
            baseline = checkout/'docs/audit/legacy-source-baseline.json'
            baseline.parent.mkdir(parents=True)
            baseline.write_text(json.dumps(dict(commit=commit,
                manifestSha256=hashlib.sha256(manifest).hexdigest()))+'\n')
            original = baseline.read_bytes()
            RETAINED.ensure(checkout)
            self.assertEqual(original, baseline.read_bytes())
            self.assertEqual(content, subprocess.check_output(
                ['git', 'show', commit+':source.txt'], cwd=checkout))
            self.assertEqual(manifest, subprocess.check_output(
                ['git', 'show', commit+':docs/audit/current-manifest.json'], cwd=checkout))
            packed = '\n'.join(git(checkout, 'verify-pack', '-v', str(index))
                for index in (checkout/'.git/objects/pack').glob('*.idx'))
            self.assertNotIn(excluded, packed)
            with patch.object(RETAINED, 'fetch') as fetch:
                RETAINED.ensure(checkout)
                fetch.assert_not_called()
