#!/usr/bin/env python3
"""Index all fetched official sources. Indexing never certifies semantic coverage.

Review decisions live separately and reference source hashes, implementation,
executable outcomes and explicitly bounded scope. Regeneration cannot erase them.
"""
import argparse
from collections import Counter
from datetime import date
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--date', required=True)
args = parser.parse_args()
DAY = date.fromisoformat(args.date).isoformat()
source = ROOT / 'data/official/upstream' / DAY
output = ROOT / 'docs/legacy/evidence' / f'rules-reaudit-{DAY}'
output.mkdir(parents=True, exist_ok=True)

def read(path):
    return json.loads(path.read_text())

def write(name, value):
    (output / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')

def digest(value):
    raw = json.dumps(value, ensure_ascii=False, sort_keys=True).encode()
    return hashlib.sha256(raw).hexdigest()

def number(value):
    # Formatting-only normalization. Preserve set, variant and denominator.
    return value.replace('·', '-').upper()

active = read(ROOT / 'data/official/card-catalog.zh-CN.json')['cards']
cn = read(source / 'card-catalog.zh-CN.json')['cards']
en_doc = read(source / 'card-gallery.en-US.json')
en = en_doc['cards']
active_by_number = {number(c['cardNo']): c for c in active}
entries = {}
for language, cards, key in [('zh-CN', cn, 'cardNo'), ('en-US', en, 'publicCode')]:
    for card in cards:
        identity = number(card[key])
        entry = entries.setdefault(identity, dict(id=identity, sources=[],
            activeCardNo=active_by_number.get(identity, {}).get('cardNo'),
            reviewStatus='NOT_REVIEWED', implementation=[], tests=[], nativeEvidence=[]))
        entry['sources'].append(dict(language=language, sourceId=card['id'], cardNo=card[key],
            name=card.get('cardName', card.get('name')), sha256=digest(card)))
for identity, card in active_by_number.items():
    if identity not in entries:
        entries[identity] = dict(id=identity, sources=[], activeCardNo=card['cardNo'],
            reviewStatus='ACTIVE_BUT_NOT_IN_CURRENT_OFFICIAL_SOURCES', implementation=[], tests=[], nativeEvidence=[])
write('all-card-ledger.json', dict(date=DAY, records=sorted(entries.values(), key=lambda c:c['id']),
    note='One entry per exact normalized printed number; language equality and variant equivalence still require review. Source presence and registration are not behavior verification.'))

cn_corpus = read(output / 'official-corpus.json')
global_sources = read(source / 'global-rules-sources.json')
documents = []
blocks = []
for doc in cn_corpus['documents']:
    doc_id = 'CN-' + doc['sha256'][:16]
    effective = '2026-10-16' if '10.16生效' in doc['title'] and DAY.startswith('2026-') else None
    documents.append(dict(id=doc_id, language='zh-CN', title=doc['title'], url=doc['url'],
        sha256=doc['sha256'], published=doc['updated'], effectiveDate=effective,
        versionStatus='PUBLISHED_FUTURE_EFFECTIVE' if effective and effective > DAY else 'APPLICABILITY_REVIEW_REQUIRED',
        pages=len(doc['pages'])))
    for record in cn_corpus['records']:
        if record['document'] != doc['id']:
            continue
        blocks.append(dict(id=doc_id + '-' + record['id'].split('-', 2)[2], document=doc_id,
            sourceRecord=record['id'], textHash=digest(record['text']), reviewStatus='NOT_REVIEWED'))
for doc in global_sources['records']:
    doc_id = 'EN-' + doc['sha256'][:16]
    future = ('radiance-patch' in doc['url'] or 'october-2026-tournament' in doc['url']
              or doc['url'].endswith(('5c7946f7c3b8c6e0faff85ab6dadc6d2e721ea94.pdf', '861752822d217edf37d9febaeaf77a7d924c1bb6.pdf')))
    documents.append(dict(id=doc_id, language='en-US', title=doc.get('title', doc['url']),
        url=doc['url'], sha256=doc['sha256'], published=doc.get('published'),
        effectiveDate='2026-10-16' if future else None,
        versionStatus='PUBLISHED_FUTURE_EFFECTIVE' if future and DAY < '2026-10-16' else 'APPLICABILITY_REVIEW_REQUIRED',
        accordionGroups=doc.get('accordionGroups', 0)))
    for page, text in enumerate(doc['text'].split('\f'), 1):
        for ordinal, block in enumerate(filter(str.strip, re.split(r'\n\s*\n', text)), 1):
            blocks.append(dict(id=f'{doc_id}-P{page:03}-B{ordinal:03}', document=doc_id,
                text=block.strip(), textHash=digest(block.strip()), reviewStatus='NOT_REVIEWED',
                requiresVisualReview='[IMAGE:' in block))
assert len({b['id'] for b in blocks}) == len(blocks), 'Duplicate source blocks'
write('all-rule-ledger.json', dict(date=DAY, documents=documents, records=blocks,
    note='Every paragraph, example and accordion answer is retained; table/diagram visual checks and supersession review remain required.'))
qa = read(source / 'card-qa.zh-CN.json')
summary = dict(date=DAY, goalStatus='IN_PROGRESS_NOT_READY',
    cnOfficialPrintings=len(cn), enReturnedPrintings=len(en), enReportedTotal=en_doc['reportedTotal'],
    enSourceCountGap=en_doc['reportedMinusReturned'], combinedExactPrintingNumbers=len(entries),
    activeCatalogEntries=len(active), outsideActiveCatalog=sum(not e['activeCardNo'] for e in entries.values()),
    publishedRadiancePrintings=sum(c.get('set', {}).get('value', {}).get('id') == 'RAD' for c in en),
    cnQaFields=len(qa['entries']), cnNonemptyQaFields=sum(bool(c['cardQaList']) for c in qa['entries']),
    sourceDocuments=len(documents), sourceBlocks=len(blocks),
    sourceFetchErrors=global_sources['errors'], verifiedByIndexing=0,
    completionRequirements=['All available official sources reconciled; source gaps explicitly resolved.',
        'Every card, rule, QA answer and erratum has source-version-specific review and executable evidence or a justified non-gameplay disposition.',
        'Shared engine implementations replace incorrect development paths; all applicable regressions pass.',
        'Native, dual-client, reconnect, packaging and Windows gates have their own evidence; none inferred from backend tests.',
        'Current effective rules and published future rules have explicit applicability and separate validation.'])
write('comprehensive-summary.json', summary)
print(json.dumps({k:v for k,v in summary.items() if k!='completionRequirements'}, ensure_ascii=False))
