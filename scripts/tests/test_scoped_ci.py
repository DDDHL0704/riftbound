import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('scoped', Path(__file__).parents[1]/'run-scoped-ci.py')
run = importlib.util.module_from_spec(spec)
spec.loader.exec_module(run)


class ScopedCiTests(unittest.TestCase):
    def test_bounded_command_is_derived_and_tier_forgery_is_rejected(self):
        changes = [dict(path='tests/Riftbound.ConformanceTests/ConsumerTests.cs', status='A', oldMode='000000', newMode='100644')]
        decision = dict(changes=changes, **run.scope.tier_details(changes))
        args = run.command_for(decision)
        self.assertEqual(['focus', '--match', 'ConsumerTests'], args[2:])
        for field, value in [('profile', 'full'), ('matches', []), ('sql', True), ('tier', 'L2')]:
            bad = copy.deepcopy(decision); bad[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError): run.command_for(bad)
        decision['changes'].append(dict(path='src/Riftbound.Engine/Rule.cs', status='M', oldMode='100644', newMode='100644'))
        with self.assertRaises(ValueError): run.command_for(decision)

    def test_unknown_or_docs_cannot_be_relabeled_scoped(self):
        for path in ['scripts/runner.py', 'README.md']:
            changes = [dict(path=path, status='M', oldMode='100644', newMode='100644')]
            d = dict(changes=changes, **run.scope.tier_details(changes))
            with self.assertRaises(ValueError): run.command_for(d)


if __name__ == '__main__':
    unittest.main()
