import copy
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


spec = importlib.util.spec_from_file_location('news_governance',
    Path(__file__).parents[1]/'audit/check-project-governance.py')
gov = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gov)


class OfficialNewsObligationTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.scope = dict(region='CN', language='zh-CN', applicableAsOf='2026-10-16')
        self.card_source = self.write('cards.json', b'{}')
        self.raw = b'<div id="js_content"><p>Q: one?</p><img data-src="https://example.invalid/a.png"/><br><p>A: two.</p></div><p>outside</p>'
        self.archive = self.write('article.html.gz', gzip.compress(self.raw, mtime=0))
        self.body = self.write('article.txt', b'Q: one?\nA: two.')
        self.image = self.write('image.png', b'image source bytes')
        listed, documents = [], []
        for ordinal, state in enumerate(['ARTICLE_RETRIEVED', 'SOURCE_GAP', 'RULES_HUB_RESPONSE_NOT_INDEPENDENT_QA']):
            url = 'https://example.invalid/news/'+str(ordinal)
            identity = 'CN-NEWS-'+hashlib.sha256(url.encode()).hexdigest()[:16]
            listed.append(dict(name='Article '+str(ordinal), jumpUrl=url, pushDate='2026-10-10'))
            doc = dict(id=identity, url=url, titleAsListed=listed[-1]['name'], publishedAsListed='2026-10-10',
                region='CN', language='zh-CN', auditApplicableAsOf='2026-10-16', acquisitionState=state,
                semanticReview='OPEN', applicability='OPEN', wholePrintingCertified=False, blocks=[], images=[])
            if state != 'SOURCE_GAP':
                doc['archivedHtml'] = dict(gzip=self.archive, rawSha256=hashlib.sha256(self.raw).hexdigest(), rawBytes=len(self.raw))
            if state == 'ARTICLE_RETRIEVED':
                doc['bodyText'] = self.body
                for n, text in enumerate(['Q: one?', 'A: two.'], 1):
                    doc['blocks'].append(dict(id=identity+'-T'+str(n), firstLine=n, lastLine=n,
                        textSha256=hashlib.sha256(text.encode()).hexdigest(), exactNamePrintingCandidates=[],
                        semanticReview='OPEN', wholePrintingCertified=False, implementationEvidence=[], testEvidence=[]))
                doc['images'] = [dict(id=identity+'-I1', ordinal=1, url='https://example.invalid/a.png',
                                      bytes=self.image, semanticReview='OPEN')]
            if state == 'RULES_HUB_RESPONSE_NOT_INDEPENDENT_QA':
                doc['independentFaqBodyRetrieved'] = False
            documents.append(doc)
        inventory = self.write_json('inventory.json', dict(rulesCaseCandidates=listed))
        self.ledger = dict(schemaVersion=1, kind='OFFICIAL_CN_NEWS_SOURCE_OBLIGATIONS', **self.scope,
            semanticCoverageCertified=False, cardSource=self.card_source, candidateInventory=inventory,
            newsListingResponses=[self.write_json('listing.json', dict(result=dict(total=3, list=listed)))], documents=documents)

    def write(self, name, raw):
        (self.root/name).write_bytes(raw)
        return dict(path=name, sha256=hashlib.sha256(raw).hexdigest())

    def write_json(self, name, obj):
        return self.write(name, json.dumps(obj).encode())

    def rows(self):
        ref = self.write_json('ledger.json', self.ledger)
        return gov.official_news_obligations(self.root, ref, self.scope, self.card_source, {'OGN-001/298'})

    def test_original_html_binds_all_text_images_and_unretrieved_sources_without_certification(self):
        rows = self.rows()
        self.assertEqual(6, len(rows))
        self.assertEqual({'OFFICIAL_NEWS_SOURCE', 'OFFICIAL_NEWS_TEXT_BRANCH', 'OFFICIAL_NEWS_IMAGE_BRANCH'},
                         {r['kind'] for r in rows})
        self.assertEqual(1, sum(r.get('acquisitionState') == 'SOURCE_GAP' for r in rows))
        self.assertTrue(all(r['semanticReview'] == r['productAcceptance'] == 'OPEN' for r in rows))
        self.assertTrue(all(r['wholePrintingCertified'] is False for r in rows))

    def test_missing_source_or_duplicate_identity_cannot_shrink_the_universe(self):
        original = copy.deepcopy(self.ledger)
        self.ledger['documents'].pop()
        with self.assertRaisesRegex(ValueError, 'source universe'):
            self.rows()
        self.ledger = original
        self.ledger['documents'].append(copy.deepcopy(original['documents'][0]))
        with self.assertRaisesRegex(ValueError, 'duplicate identity'):
            self.rows()

    def test_titles_outside_rule_candidate_filter_still_produce_open_body_obligations(self):
        listing = json.loads((self.root/'listing.json').read_bytes())
        listing['result']['list'].append(dict(name='Card reveal', jumpUrl='https://example.invalid/reveal', pushDate='2026-10-11'))
        listing['result']['total'] = 4
        self.ledger['newsListingResponses'] = [self.write_json('listing.json', listing)]
        rows = self.rows()
        unclassified = [r for r in rows if r['kind'] == 'OFFICIAL_NEWS_UNCLASSIFIED_SOURCE']
        self.assertEqual(7, len(rows))
        self.assertEqual(1, len(unclassified))
        self.assertEqual('Card reveal', unclassified[0]['sourceRecord']['name'])
        self.assertEqual('OPEN_BODY_AND_RULE_RELEVANCE_NOT_REVIEWED', unclassified[0]['applicability'])
        listing['result']['list'].pop()
        self.ledger['newsListingResponses'] = [self.write_json('listing.json', listing)]
        with self.assertRaisesRegex(ValueError, 'listing is incomplete'):
            self.rows()

    def test_missing_overlapping_or_changed_text_block_is_rejected(self):
        original = copy.deepcopy(self.ledger)
        mutations = [lambda d: d['blocks'].pop(),
                     lambda d: d['blocks'].append({**d['blocks'][0], 'id':'duplicate-range'}),
                     lambda d: d['blocks'][0].update(textSha256='0'*64)]
        for mutate in mutations:
            with self.subTest(mutate=mutate):
                self.ledger = copy.deepcopy(original)
                mutate(self.ledger['documents'][0])
                with self.assertRaises(ValueError):
                    self.rows()

    def test_image_omission_and_extracted_text_rewrite_fail_against_original_html(self):
        original = copy.deepcopy(self.ledger)
        self.ledger['documents'][0]['images'].clear()
        with self.assertRaisesRegex(ValueError, 'image extraction'):
            self.rows()
        self.ledger = original
        self.ledger['documents'][0]['bodyText'] = self.write('changed.txt', b'Q: changed?\nA: two.')
        with self.assertRaisesRegex(ValueError, 'text extraction'):
            self.rows()

    def test_repacked_html_still_needs_the_original_uncompressed_byte_identity(self):
        doc = self.ledger['documents'][0]
        doc['archivedHtml']['gzip'] = self.write('changed.gz', gzip.compress(self.raw+b'changed'))
        with self.assertRaisesRegex(ValueError, 'original HTML byte identity'):
            self.rows()

    def test_rules_hub_or_verification_page_cannot_be_promoted_to_independent_article(self):
        self.ledger['documents'][2]['independentFaqBodyRetrieved'] = True
        with self.assertRaisesRegex(ValueError, 'independent FAQ'):
            self.rows()
        with self.assertRaisesRegex(ValueError, 'root missing'):
            gov.official_news_article('<h2>环境异常</h2><p>完成验证后即可继续访问。</p>'.encode())

    def test_wrong_scope_source_pin_whole_card_claim_and_unknown_printing_fail(self):
        original = copy.deepcopy(self.ledger)
        mutations = [lambda d: d.update(applicableAsOf='2026-10-15'),
                     lambda d: d.update(cardSource={'path':'cards.json', 'sha256':'0'*64}),
                     lambda d: d['documents'][0].update(wholePrintingCertified=True),
                     lambda d: d['documents'][0]['blocks'][0].update(exactNamePrintingCandidates=[{'printing':'RAD-UNKNOWN/167'}])]
        for mutate in mutations:
            self.ledger = copy.deepcopy(original)
            mutate(self.ledger)
            with self.assertRaises(ValueError):
                self.rows()

    def test_archived_body_and_image_bytes_cannot_drift(self):
        for name in ['article.html.gz', 'image.png']:
            with self.subTest(name=name):
                previous = (self.root/name).read_bytes()
                (self.root/name).write_bytes(previous+b'changed')
                with self.assertRaisesRegex(ValueError, 'Pinned input hash mismatch'):
                    self.rows()
                (self.root/name).write_bytes(previous)


if __name__ == '__main__':
    unittest.main()
