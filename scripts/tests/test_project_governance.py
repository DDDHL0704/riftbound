import copy
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('governance', Path(__file__).parents[1] / 'audit/check-project-governance.py')
gov = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gov)


class GovernanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root/'proof.json').write_text('{}')
        cn_cards = [{'id': 1, 'cardNo': 'ARC\u00b7001/006'},
                    {'id': 2, 'cardNo': 'ARC\u00b7001a/006'}]
        en_cards = [{'id': 'arc-001-006', 'publicCode': 'ARC-001/006'}]
        cards = []
        for c in cn_cards:
            sources = [dict(language='zh-CN', sourceId=c['id'], cardNo=c['cardNo'], sha256=gov.digest(c))]
            if c['id'] == 1:
                e = en_cards[0]
                sources.append(dict(language='en-US', sourceId=e['id'], cardNo=e['publicCode'], sha256=gov.digest(e)))
            cards.append(dict(id=gov.number(c['cardNo']), sources=sources,
                              activeCardNo=c['cardNo'] if c['id'] == 1 else None, reviewStatus='NOT_REVIEWED'))
        cn_hash, en_hash = 'a'*64, 'b'*64
        cn_id, en_id = 'CN-'+cn_hash[:16], 'EN-'+en_hash[:16]
        self.data = dict(
            cards=dict(date='2026-10-11', records=cards),
            rules=dict(date='2026-10-11', documents=[
                dict(id=cn_id, sha256=cn_hash, url='https://example.invalid/cn'),
                dict(id=en_id, sha256=en_hash, url='https://example.invalid/en')], records=[
                dict(id=cn_id+'-P001-B001', document=cn_id, sourceRecord='DOC-01-P001-B001',
                     textHash=gov.digest('rule text'), reviewStatus='NOT_REVIEWED'),
                dict(id=en_id+'-P001-B001', document=en_id, text='global rule',
                     textHash=gov.digest('global rule'), reviewStatus='NOT_REVIEWED')]),
            corpus=dict(date='2026-10-11', documents=[dict(id='DOC-01', sha256=cn_hash,
                url='https://example.invalid/cn', pages=[dict(page=1, blocks=['DOC-01-P001-B001'])])],
                records=[dict(id='DOC-01-P001-B001', document='DOC-01', page=1, text='rule text')]),
            reviews=dict(overallStatus='IN_PROGRESS_NOT_READY', reviews=[dict(id='partial',
                status='VERIFIED_SCOPED_BEHAVIOR', scope='One bounded behavior', subjects=['ARC-001/006'],
                ruleRecords=['DOC-01-P001-B001'], evidence=['proof.json'], validation=dict(wholeCardCertification=False),
                exclusions=['Other printed abilities'])]),
            summary=dict(date='2026-10-11', goalStatus='IN_PROGRESS_NOT_READY', verifiedByIndexing=0,
                combinedExactPrintingNumbers=2, sourceDocuments=2, sourceBlocks=2, cnOfficialPrintings=2,
                enReturnedPrintings=1, enReportedTotal=2, enSourceCountGap=1, activeCatalogEntries=1,
                outsideActiveCatalog=1, cnQaFields=2, cnNonemptyQaFields=0, sourceFetchErrors=[]),
            cn=dict(cards=cn_cards), en=dict(cards=en_cards, reportedTotal=2), active=dict(cards=[cn_cards[0]]),
            qa=dict(entries=[dict(cardQaList=[]), dict(cardQaList=[])]),
            **{'global': dict(records=[dict(sha256=en_hash, url='https://example.invalid/en', text='global rule')], errors=[])})

    def validate(self):
        return gov.validate(self.data, self.root)

    def test_valid_index_retains_source_gap_variants_and_never_certifies_gameplay(self):
        result = self.validate()
        self.assertTrue(result['governanceValid'], result['errors'])
        self.assertFalse(result['semanticCoverageCertified'])
        self.assertEqual(2, result['counts']['combinedExactPrintingNumbers'])
        self.assertEqual(1, result['counts']['enSourceCountGap'])
        self.assertTrue(result['warnings'])

    def test_duplicate_id_and_dangling_reference_cannot_pass(self):
        original = copy.deepcopy(self.data)
        for key, field in [('cards', 'records'), ('rules', 'records'), ('corpus', 'records'), ('reviews', 'reviews')]:
            with self.subTest(key=key):
                self.data = copy.deepcopy(original)
                self.data[key][field].append(copy.deepcopy(self.data[key][field][0]))
                with self.assertRaisesRegex(ValueError, 'duplicate identity'):
                    self.validate()
        self.data = original
        self.data['reviews']['reviews'][0]['ruleRecords'] = ['DOC-99-P001-B001']
        self.assertFalse(self.validate()['governanceValid'])

    def test_source_text_hash_and_universe_changes_cannot_pass(self):
        original = copy.deepcopy(self.data)
        mutations = [lambda: self.data['cards']['records'][0]['sources'][0].update(sha256='0'*64),
                     lambda: self.data['rules']['records'][0].update(textHash='0'*64),
                     lambda: self.data['rules']['records'].pop(),
                     lambda: self.data['corpus']['documents'][0]['pages'][0]['blocks'].clear()]
        for mutate in mutations:
            self.data = copy.deepcopy(original)
            mutate()
            self.assertFalse(self.validate()['governanceValid'])

    def test_summary_mismatch_index_promotion_and_scoped_whole_card_claim_fail(self):
        original = copy.deepcopy(self.data)
        mutations = [lambda: self.data['summary'].update(sourceBlocks=1),
                     lambda: self.data['summary'].update(verifiedByIndexing=1),
                     lambda: self.data['summary'].update(goalStatus='COMPLETE'),
                     lambda: self.data['reviews']['reviews'][0]['validation'].update(wholeCardCertification=True)]
        for mutate in mutations:
            self.data = copy.deepcopy(original)
            mutate()
            self.assertFalse(self.validate()['governanceValid'])

    def test_freeform_legacy_review_stays_open_without_promoting_coverage(self):
        review = self.data['reviews']['reviews'][0]
        for key in ['ruleRecords', 'validation', 'exclusions']:
            del review[key]
        review['rules'] = ['DOC-01 123.1 (freeform scope)']
        result = self.validate()
        self.assertTrue(result['governanceValid'])
        self.assertFalse(result['semanticCoverageCertified'])
        self.assertTrue(any('heterogeneous' in warning for warning in result['warnings']))

    def test_future_effective_source_and_missing_evidence_fail(self):
        doc = self.data['rules']['documents'][0]
        doc.update(effectiveDate='2026-10-16', versionStatus='CURRENT')
        self.assertFalse(self.validate()['governanceValid'])
        doc['versionStatus'] = 'PUBLISHED_FUTURE_EFFECTIVE'
        self.assertTrue(self.validate()['governanceValid'])
        (self.root/'proof.json').unlink()
        self.assertFalse(self.validate()['governanceValid'])

    def test_missing_and_escaped_governance_links_fail(self):
        for name in gov.CANONICAL_DOCS:
            path = self.root/name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('# policy\n')
        self.assertEqual([], gov.check_documents(self.root))
        (self.root/'README.md').write_text('[Missing](docs/missing.md)\n[Escape](../escape.md)\n')
        self.assertEqual(2, len(gov.check_documents(self.root)))


if __name__ == '__main__':
    unittest.main()
