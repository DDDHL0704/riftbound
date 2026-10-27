import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('cards', Path(__file__).parents[1]/'audit/card-certification-view.py')
cards = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cards)


class CardViewTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        one = {key: None for key in cards.SEMANTIC_FIELDS}
        one.update(cardNo='VEN·003', cardName='Example', cardEffect='Draw one.', cardCategoryList=['spell'])
        self.cn = [one, {**one, 'cardNo':'VEN·003a'}]
        self.rows = [dict(id='VEN-003', activeCardNo='VEN·003'), dict(id='VEN-003A', activeCardNo=None),
                     dict(id='SFD-999', activeCardNo=None)]
        self.ledger = dict(schemaVersion=1, scope={'sourceManifest':self.pin('manifest', {'allOfficialObligationsRetained':True})},
                           sources={}, certifications=[])
        self.refresh()

    def pin(self, name, obj):
        p = self.root/(name+'.json'); p.write_text(json.dumps(obj))
        return dict(path=p.name, sha256=hashlib.sha256(p.read_bytes()).hexdigest())

    def refresh(self):
        self.ledger['sources'] = dict(cards=self.pin('cards', dict(records=self.rows)),
            cn=self.pin('cn', dict(cards=self.cn)), en=self.pin('en', dict(cards=[])))

    def test_exact_semantic_family_merges_without_certifying_or_dropping_unknown(self):
        r = cards.build(self.root, self.ledger)
        self.assertEqual(3, r['counts']['retainedPrintings'])
        self.assertEqual(2, r['counts']['provisionalFunctionGroups'])
        self.assertEqual(1, r['counts']['unresolvedMappingPrintings'])
        self.assertIsNone(r['counts']['applicableFunctionCount'])
        self.assertEqual(0, r['counts']['certifiedWholeCards'])
        self.assertEqual({'VEN-003', 'VEN-003A', 'SFD-999'}, {p for c in r['functions'] for p in c['printings']})

    def test_current_runtime_activation_is_visible_without_rewriting_retained_sources(self):
        path = self.root/'data/official/card-catalog.zh-CN.json'; path.parent.mkdir(parents=True)
        path.write_text(json.dumps(dict(cards=[dict(cardNo='VEN·003a'), dict(cardNo='NEW·001')])))
        result = cards.build(self.root, self.ledger)
        self.assertEqual(2, result['runtimeCatalog']['entries'])
        self.assertEqual(['NEW-001'], result['runtimeCatalog']['outsideRetainedBase'])
        self.assertEqual(['VEN-003A'], [p for c in result['functions'] for p in c['activePrintings']])
        self.assertEqual(3, result['counts']['retainedPrintings'])
        self.assertEqual(0, result['counts']['certifiedWholeCards'])

    def test_same_name_different_text_or_missing_fields_cannot_merge(self):
        for changed in ({**self.cn[1], 'cardEffect':'Draw two.'},
                        {k:v for k,v in self.cn[1].items() if k != 'cardEffect'}):
            self.cn[1] = changed; self.refresh()
            self.assertEqual(3, cards.build(self.root, self.ledger)['counts']['provisionalFunctionGroups'])

    def test_category_fallback_cannot_merge_different_card_types(self):
        for category_list in ([], None):
            with self.subTest(category_list=category_list):
                self.cn[0].update(cardCategoryList=category_list, cardCategory='unit')
                self.cn[1].update(cardCategoryList=category_list, cardCategory='spell')
                self.refresh()
                result = cards.build(self.root, self.ledger)
                self.assertEqual(3, result['counts']['provisionalFunctionGroups'])
                self.assertEqual(0, result['counts']['certifiedWholeCards'])

    def test_equal_category_fallback_retains_unknown_applicability(self):
        for card in self.cn:
            card.update(cardCategoryList=[], cardCategory='unit')
        self.refresh()
        result = cards.build(self.root, self.ledger)
        self.assertEqual(2, result['counts']['provisionalFunctionGroups'])
        self.assertFalse(result['semanticCoverageCertified'])
        self.assertTrue(all(row['applicability'] == 'UNKNOWN_PENDING_REGIONAL_EFFECTIVE_DISPOSITION'
                            and not row['wholeCardCertified'] for row in result['functions']))

    def test_present_malformed_runtime_catalog_cannot_fall_back_to_retained_activation(self):
        path = self.root/'data/official/card-catalog.zh-CN.json'
        path.parent.mkdir(parents=True)
        for value in ({}, [], None):
            with self.subTest(value=value):
                path.write_text(json.dumps(value))
                with self.assertRaisesRegex(ValueError, 'runtime catalog'):
                    cards.build(self.root, self.ledger)

    def test_explicit_empty_runtime_catalog_preserves_zero_active_entries(self):
        path = self.root/'data/official/card-catalog.zh-CN.json'
        path.parent.mkdir(parents=True)
        path.write_text(json.dumps(dict(cards=[])))
        result = cards.build(self.root, self.ledger)
        self.assertEqual(0, result['runtimeCatalog']['entries'])
        self.assertEqual([], [p for row in result['functions'] for p in row['activePrintings']])
        self.assertIsNone(result['counts']['applicableFunctionCount'])
        self.assertEqual(0, result['counts']['certifiedWholeCards'])

    def test_mismatched_source_and_duplicate_printing_fail_closed(self):
        (self.root/'cn.json').write_text('{}')
        with self.assertRaisesRegex(ValueError, 'hash mismatch'): cards.build(self.root, self.ledger)
        self.rows.append(copy.deepcopy(self.rows[0])); self.refresh()
        with self.assertRaisesRegex(ValueError, 'Duplicate retained'): cards.build(self.root, self.ledger)

    def test_registry_flag_and_partial_mapping_cannot_certify_whole_card(self):
        key = cards.build(self.root, self.ledger)['functions'][0]['functionId']
        self.ledger['certifications'] = [dict(functionId=key, state='FIXED_CANDIDATE_CERTIFIED',
            printings=['VEN-003'], implemented=True)]
        with self.assertRaisesRegex(ValueError, 'every exact printing'): cards.build(self.root, self.ledger)
        self.ledger['certifications'][0]['printings'].append('VEN-003A')
        with self.assertRaisesRegex(ValueError, 'explicit sources'): cards.build(self.root, self.ledger)
        self.ledger['certifications'][0].update(sourceRefs=['official'], effectAndKeywordObligations=['one'])
        with self.assertRaisesRegex(ValueError, 'adapter not yet implemented'): cards.build(self.root, self.ledger)


if __name__ == '__main__':
    unittest.main()
