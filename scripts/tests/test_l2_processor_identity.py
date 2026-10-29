import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('l2_processor_identity', ROOT / 'scripts/l2-distributed.py')
D = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(D)


class ProcessorIdentityTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.info = self.root / 'cpuinfo'
        self.info.write_text('processor : 0\nmodel name : Test CPU\nmodel name : Test CPU\n')

    def cpu(self, logical, socket, core):
        folder = self.root / f'cpu{logical}' / 'topology'
        folder.mkdir(parents=True)
        (folder / 'physical_package_id').write_text(str(socket))
        (folder / 'core_id').write_text(str(core))

    def observe(self, affinity):
        return D.processor_identity(self.info, self.root, affinity)

    def test_smt_siblings_count_as_one_physical_core(self):
        for logical, core in enumerate([0, 1, 0, 1]):
            self.cpu(logical, 0, core)
        result = self.observe({0, 1, 2, 3})
        self.assertEqual(['Test CPU'], result['modelNames'])
        self.assertEqual(4, result['affinityLogicalCpuCount'])
        self.assertEqual(2, result['affinityPhysicalCoreCount'])
        subset = self.observe({1, 3})
        self.assertEqual(1, subset['affinityPhysicalCoreCount'])
        self.assertEqual([1, 3], [row['cpu'] for row in subset['affinityCpuTopology']])

    def test_core_ids_on_different_sockets_remain_distinct(self):
        self.cpu(0, 0, 0)
        self.cpu(1, 1, 0)
        self.assertEqual(2, self.observe({0, 1})['affinityPhysicalCoreCount'])

    def test_missing_or_unknown_topology_is_optional_without_false_core_counts(self):
        for core in [None, -1, 'unknown']:
            with self.subTest(core=core):
                if core is not None:
                    folder = self.root / 'cpu0' / 'topology'
                    folder.mkdir(parents=True, exist_ok=True)
                    (folder / 'physical_package_id').write_text('0')
                    (folder / 'core_id').write_text(str(core))
                result = self.observe({0})
                self.assertEqual(['Test CPU'], result['modelNames'])
                self.assertIn('topologyReadError', result)
                self.assertNotIn('affinityPhysicalCoreCount', result)

    def test_missing_model_does_not_discard_valid_topology(self):
        self.info.unlink()
        self.cpu(0, 0, 0)
        result = self.observe({0})
        self.assertIn('modelReadError', result)
        self.assertEqual(1, result['affinityPhysicalCoreCount'])

    def test_empty_affinity_does_not_report_a_zero_core_runner(self):
        result = self.observe(set())
        self.assertIn('topologyReadError', result)
        self.assertNotIn('affinityPhysicalCoreCount', result)
