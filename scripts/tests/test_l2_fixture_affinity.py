from collections import Counter
import importlib.util
from pathlib import Path
import unittest


SPEC = importlib.util.spec_from_file_location('fixture_affinity_selection',
    Path(__file__).resolve().parents[1] / 'l2_case_selection.py')
S = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(S)


class SharedFixtureSchedulingTests(unittest.TestCase):
    def fixture(self, weight=100):
        classes = sorted(S.SHARED_FIXTURE_CLASSES[0])
        units = [dict(kind='method', method=classes[0] + '.Case' + str(i)) for i in range(3)]
        units.append(dict(kind='method', method=classes[1] + '.Memo'))
        units.extend(dict(kind='method', method='Independent.Class' + str(i) + '.Case') for i in range(5))
        cases = {unit['method']: [unit['method']] for unit in units}
        weights = {unit['method']: weight if unit['method'].startswith(classes[0]) else 100 for unit in units}
        weights[units[-1]['method']] = 600
        return units, cases, weights

    def test_shared_accepted_fixture_runs_in_one_process_with_exact_case_coverage(self):
        units, cases, weights = self.fixture()
        shards = S.partition(units, cases, weights, 3)
        S.validate(shards, cases, list(cases))
        together = [shard for shard in shards if any(
            unit['method'].rsplit('.', 1)[0] in S.SHARED_FIXTURE_CLASSES[0]
            for unit in shard['selectors'])]
        self.assertEqual(1, len(together))
        self.assertEqual(4, sum(unit['method'].rsplit('.', 1)[0] in S.SHARED_FIXTURE_CLASSES[0]
                                for unit in together[0]['selectors']))
        self.assertEqual(Counter(cases.keys()), Counter(name for shard in shards for name in S.case_names(shard, cases)))

    def test_long_serial_fixture_class_keeps_its_existing_parallel_shards(self):
        units, cases, weights = self.fixture(weight=600)
        shards = S.partition(units, cases, weights, 3)
        test_class = sorted(S.SHARED_FIXTURE_CLASSES[0])[0]
        places = {shard['name'] for shard in shards for unit in shard['selectors']
                  if unit['method'].rsplit('.', 1)[0] == test_class}
        self.assertGreater(len(places), 1)
        S.validate(shards, cases, list(cases))

    def test_affinity_never_leaves_a_required_shard_empty(self):
        units, cases, weights = self.fixture()
        shards = S.partition(units, cases, weights, len(units))
        self.assertTrue(all(len(shard['selectors']) == 1 for shard in shards))
        S.validate(shards, cases, list(cases))

    def test_splitting_parameter_rows_preserves_every_original_row_once(self):
        units, cases, weights = self.fixture()
        method = units[0]['method']
        cases[method] = [method + '(value: 1)', method + '(value: 2)']
        units = [dict(kind='case', method=method, case=row) for row in cases[method]] + units[1:]
        shards = S.partition(units, cases, weights, 3)
        discovery = [row for rows in cases.values() for row in rows]
        S.validate(shards, cases, discovery)
        self.assertEqual(Counter(discovery), Counter(name for shard in shards for name in S.case_names(shard, cases)))

    def test_scheduling_does_not_depend_on_input_order(self):
        units, cases, weights = self.fixture()
        self.assertEqual(S.partition(units, cases, weights, 3),
                         S.partition(list(reversed(units)), cases, weights, 3))
