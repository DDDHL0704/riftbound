import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('source_scope_reference', ROOT / 'tools/source-guard-scope/validate_reference.py')
SCOPE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SCOPE)


class SourceScopeReferenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'discovery.log'
        self.method = 'Riftbound.ConformanceTests.SourceTests.HistoricalGuard'
        self.other = 'Riftbound.ConformanceTests.GameTests.KeepUnknownMechanism'
        self.path.write_text('Tests:\n    ' + self.method + '\n    ' + self.other + '\n')
        self.head = 'a' * 40
        methods = sorted([self.method, self.other])
        self.reference = dict(commonSourceGuardMethods=[self.method], profiles=[dict(profile='fixed',
            sourceHead=self.head, extraSourceGuardMethods=[], sourceGuardMethodCount=1,
            discoveredMethodSetSha256=hashlib.sha256(('\n'.join(methods) + '\n').encode()).hexdigest())])

    def verify(self, reference=None, head=None):
        return SCOPE.validate(reference or self.reference, 'fixed', head or self.head, self.path)

    def test_reference_is_scope_only_and_keeps_unselected_methods_in_the_original_suite(self):
        result = self.verify()
        self.assertEqual(2, result['discoveredMethods'])
        self.assertEqual(1, result['sourceGuardMethods'])
        self.assertFalse(result['semanticTestsExecuted'])
        self.assertFalse(result['semanticPassClaim'])
        self.assertFalse(result['existingLanesChanged'])

    def test_wrong_or_abbreviated_source_sha_is_not_a_new_input(self):
        for head in ['HEAD', 'b' * 40, self.head[:8]]:
            with self.subTest(head=head), self.assertRaises(ValueError): self.verify(head=head)

    def test_new_unselected_method_still_invalidates_an_old_discovery_reference(self):
        with self.path.open('a') as stream: stream.write('    Riftbound.ConformanceTests.NewTests.NeverSilentlyDrop\n')
        with self.assertRaisesRegex(ValueError, 'Discovery method set changed'): self.verify()

    def test_missing_source_method_or_empty_discovery_fails(self):
        for text in ['', 'Tests:\n    ' + self.other + '\n']:
            self.path.write_text(text)
            with self.subTest(text=text), self.assertRaises(ValueError): self.verify()

    def test_duplicate_empty_or_false_count_manifest_fails(self):
        for members in [[], [self.method, self.method]]:
            reference = copy.deepcopy(self.reference);reference['commonSourceGuardMethods'] = members
            with self.subTest(members=members), self.assertRaises(ValueError): self.verify(reference)
        reference = copy.deepcopy(self.reference);reference['profiles'][0]['sourceGuardMethodCount'] = 999
        with self.assertRaises(ValueError): self.verify(reference)

    def test_malformed_discovery_does_not_become_a_subset(self):
        for text in ['Tests:\n    Custom display label\n', json.dumps(['Foreign.Method'])]:
            self.path.write_text(text)
            with self.subTest(text=text), self.assertRaises(ValueError): self.verify()
