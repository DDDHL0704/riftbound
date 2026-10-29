import os
from pathlib import Path
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).parents[1] / 'dev-env.sh'


class DevEnvironmentTests(unittest.TestCase):
    def source_environment(self, wrapper_on_path):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            wrapper_bin = root / 'wrapper'
            sdk = root / 'sdk'
            wrapper_bin.mkdir()
            sdk.mkdir()
            for directory in ([wrapper_bin, sdk] if wrapper_on_path else [sdk]):
                command = directory / 'dotnet'
                command.write_text('#!/bin/sh\nexit 0\n')
                command.chmod(0o755)
            env = dict(os.environ, PATH=str(wrapper_bin), DOTNET_ROOT=str(sdk))
            result = subprocess.run(
                ['/bin/bash', '-c',
                 'node() { :; }; psql() { :; }; redis-cli() { :; }; '
                 'source "$1"; command -v dotnet', 'test-dev-env', str(SCRIPT)],
                env=env, capture_output=True, text=True, check=True)
            self.assertIn('DOTNET_ROOT=' + str(sdk), result.stdout)
            return result.stdout.splitlines()[-1], str((wrapper_bin if wrapper_on_path else sdk) / 'dotnet')

    def test_existing_path_wrapper_precedes_direct_sdk(self):
        actual, expected = self.source_environment(wrapper_on_path=True)
        self.assertEqual(expected, actual)

    def test_sdk_fallback_is_available_when_path_has_no_dotnet(self):
        actual, expected = self.source_environment(wrapper_on_path=False)
        self.assertEqual(expected, actual)


if __name__ == '__main__':
    unittest.main()
