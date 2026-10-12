#!/usr/bin/env python3
"""Check source-index integrity and bounded claims; never certify gameplay.

Reads the recorded snapshot only. No source refresh, rule tests, database calls,
ledger rewrites or inheritance of scoped PASS into whole-card coverage.
"""
import argparse
from collections import Counter
from datetime import date
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[2]
HASH = re.compile(r'[0-9a-f]{64}')
EXACT_CN_RECORD = re.compile(r'DOC-\d+-P\d+-B\d+')
CANONICAL_DOCS = (
    'AGENTS.md', 'README.md', 'GLOSSARY.md', 'docs/START_HERE.md',
    'docs/PROJECT_STATUS.md', 'docs/PROJECT_GOVERNANCE.md',
    'docs/REPOSITORY_AUDIT_2026-10-12.md', 'docs/RULE_TEST_WORKFLOW.md',
    'docs/development-audit-status.md', 'docs/agents/issue-tracker.md',
    'docs/agents/triage-labels.md', 'docs/agents/domain.md',
)


def digest(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True).encode()).hexdigest()


def number(value):
    # Formatting only: do not merge variants, sets or printed denominators.
    if not isinstance(value, str) or not value:
        raise ValueError('Missing exact printing number')
    return value.replace('\u00b7', '-').upper()


def index(rows, label):
    if not isinstance(rows, list):
        raise ValueError(label+' must be a list')
    result = {}
    for row in rows:
        identity = row.get('id') if isinstance(row, dict) else None
        if not isinstance(identity, str) or not identity or identity in result:
            raise ValueError('Missing or duplicate identity in '+label+': '+str(identity))
        result[identity] = row
    return result


def validate(data, root):
    errors, warnings = [], []

    def require(condition, message):
        if not condition:
            errors.append(message)

    cards, rules, corpus, reviews, summary = (data[k] for k in ('cards', 'rules', 'corpus', 'reviews', 'summary'))
    card_index = index(cards['records'], 'card ledger')
    rule_index = index(rules['records'], 'rule ledger')
    document_index = index(rules['documents'], 'rule documents')
    cn_records = index(corpus['records'], 'CN corpus')
    cn_documents = index(corpus['documents'], 'CN documents')
    review_index = index(reviews['reviews'], 'review decisions')
    snapshot_date = cards['date']
    date.fromisoformat(snapshot_date)
    require(all(item['date'] == snapshot_date for item in (rules, corpus, summary)),
            'Snapshot dates differ across indexes')

    expected_sources = {}
    for language, key, items in [('zh-CN', 'cardNo', data['cn']['cards']),
                                  ('en-US', 'publicCode', data['en']['cards'])]:
        for item in items:
            identity = number(item[key])
            expected_sources.setdefault(identity, []).append((language, str(item['id']), item[key], digest(item)))
    active = {number(item['cardNo']): item['cardNo'] for item in data['active']['cards']}
    require(len(active) == len(data['active']['cards']), 'Duplicate active printing identity')
    require(set(card_index) == set(expected_sources) | set(active), 'Card universe lost or gained printing identities')
    for identity, item in card_index.items():
        actual = [(s['language'], str(s['sourceId']), s['cardNo'], s['sha256']) for s in item['sources']]
        require(Counter(actual) == Counter(expected_sources.get(identity, [])), 'Card source/hash mismatch: '+identity)
        require(item.get('activeCardNo') == active.get(identity), 'Active printing mismatch: '+identity)
        require(number(identity) == identity, 'Printing identity is not normalized: '+identity)

    expected_rules = {}
    expected_documents = {}
    for doc_id, doc in cn_documents.items():
        identity = 'CN-'+doc['sha256'][:16]
        require(bool(HASH.fullmatch(doc['sha256'])), 'Invalid CN document hash: '+doc_id)
        expected_documents[identity] = (doc['sha256'], doc['url'])
        page_records = []
        for page in doc['pages']:
            for record_id in page['blocks']:
                record = cn_records.get(record_id)
                require(record is not None and record['document'] == doc_id and record['page'] == page['page'],
                        'Dangling or mislocated CN page record: '+record_id)
                page_records.append(record_id)
        doc_records = {r['id'] for r in cn_records.values() if r['document'] == doc_id}
        require(set(page_records) == doc_records and len(page_records) == len(doc_records),
                'CN page index does not cover each record once: '+doc_id)
    for identity, record in cn_records.items():
        require(record['document'] in cn_documents, 'Dangling CN document: '+identity)
        doc = cn_documents.get(record['document'])
        if doc:
            ledger_id = 'CN-'+doc['sha256'][:16]+'-'+identity.split('-', 2)[2]
            expected_rules[ledger_id] = (digest(record['text']), identity, None)
    for doc in data['global']['records']:
        identity = 'EN-'+doc['sha256'][:16]
        require(bool(HASH.fullmatch(doc['sha256'])) and identity not in expected_documents,
                'Invalid or duplicate global source hash: '+identity)
        expected_documents[identity] = (doc['sha256'], doc['url'])
        for page, text in enumerate(doc['text'].split('\f'), 1):
            for ordinal, block in enumerate(filter(str.strip, re.split(r'\n\s*\n', text)), 1):
                expected_rules[f'{identity}-P{page:03}-B{ordinal:03}'] = (digest(block.strip()), None, block.strip())
    require(set(expected_documents) == set(document_index), 'Document universe differs from recorded sources')
    for identity, item in document_index.items():
        require(expected_documents.get(identity) == (item['sha256'], item['url']), 'Document source mismatch: '+identity)
        effective = item.get('effectiveDate')
        if effective and effective > snapshot_date:
            require(item.get('versionStatus') == 'PUBLISHED_FUTURE_EFFECTIVE',
                    'Future-effective source is presented as current: '+identity)
    require(set(expected_rules) == set(rule_index), 'Rule universe lost or gained source records')
    for identity, item in rule_index.items():
        require(item['document'] in document_index, 'Dangling ledger document: '+identity)
        require((item['textHash'], item.get('sourceRecord'), item.get('text')) == expected_rules.get(identity),
                'Rule text/hash/reference mismatch: '+identity)

    expected_counts = {
        'combinedExactPrintingNumbers': len(card_index), 'sourceDocuments': len(document_index),
        'sourceBlocks': len(rule_index), 'cnOfficialPrintings': len(data['cn']['cards']),
        'enReturnedPrintings': len(data['en']['cards']), 'enReportedTotal': data['en']['reportedTotal'],
        'enSourceCountGap': data['en']['reportedTotal']-len(data['en']['cards']),
        'activeCatalogEntries': len(active), 'outsideActiveCatalog': sum(not c.get('activeCardNo') for c in card_index.values()),
        'cnQaFields': len(data['qa']['entries']),
        'cnNonemptyQaFields': sum(bool(c['cardQaList']) for c in data['qa']['entries']),
    }
    for key, expected in expected_counts.items():
        require(summary.get(key) == expected, 'Summary count mismatch: '+key)
    require(summary.get('verifiedByIndexing') == 0, 'Indexing cannot certify coverage')
    require(summary.get('sourceFetchErrors') == data['global']['errors'], 'Source error list mismatch')
    if expected_counts['enSourceCountGap'] or data['global']['errors']:
        warnings.append('Official source gaps/errors remain unresolved')

    exact_refs, subjects = set(), set()
    for identity, review in review_index.items():
        require(isinstance(review.get('scope'), str) and bool(review['scope'].strip()), 'Review has no scope: '+identity)
        if 'ruleRecords' not in review or 'validation' not in review or 'exclusions' not in review:
            warnings.append(identity+': heterogeneous review schema; no complete certification inferred')
        for record_id in review.get('ruleRecords', [])+review.get('pendingRuleRecords', []):
            require(record_id in cn_records or record_id in rule_index, 'Dangling exact review record: '+record_id)
            exact_refs.add(record_id)
        for rule in review.get('rules', []):
            record_id = rule.get('id') if isinstance(rule, dict) else rule
            if isinstance(record_id, str) and EXACT_CN_RECORD.fullmatch(record_id):
                require(record_id in cn_records, 'Dangling exact review record: '+record_id)
                if isinstance(rule, dict) and 'text' in rule and record_id in cn_records:
                    require(rule['text'] == cn_records[record_id]['text'], 'Review embeds stale rule text: '+record_id)
                exact_refs.add(record_id)
        for subject in review.get('subjects', []):
            raw = subject if isinstance(subject, str) else subject.get('id', subject.get('activeCardNo'))
            subject_id = number(raw)
            require(subject_id in card_index, 'Dangling review printing: '+subject_id)
            subjects.add(subject_id)
        paths = review.get('evidence', [])
        paths = [paths] if isinstance(paths, str) else list(paths)
        if review.get('auditDocument'):
            paths.append(review['auditDocument'])
        for path in paths:
            target = (root/path).resolve()
            require(target.is_relative_to(root.resolve()) and target.exists(), 'Missing or escaped review evidence: '+path)
        whole = review.get('wholeCardCertification', review.get('validation', {}).get('wholeCardCertification', False))
        require(whole is False, 'Scoped review cannot claim whole-card certification: '+identity)

    card_status = Counter(c['reviewStatus'] for c in card_index.values())
    rule_status = Counter(r['reviewStatus'] for r in rule_index.values())
    if any(s not in {'NOT_REVIEWED', 'ACTIVE_BUT_NOT_IN_CURRENT_OFFICIAL_SOURCES'} for s in card_status) or any(s != 'NOT_REVIEWED' for s in rule_status):
        warnings.append('Ledger statuses beyond indexing require independent semantic evidence review')
    unreviewed = card_status.get('NOT_REVIEWED', 0)+rule_status.get('NOT_REVIEWED', 0)
    if unreviewed or expected_counts['enSourceCountGap'] or warnings:
        require(summary.get('goalStatus') not in {'READY', 'COMPLETE', 'COMPLETED'}
                and reviews.get('overallStatus') not in {'READY', 'COMPLETE', 'COMPLETED'},
                'Completion claim conflicts with unresolved scope')
    return dict(governanceValid=not errors, semanticCoverageCertified=False,
                executionScope='SOURCE_INDEX_AND_REFERENCE_INTEGRITY_ONLY',
                snapshotDate=snapshot_date, counts=expected_counts,
                cardStatuses=dict(card_status), ruleStatuses=dict(rule_status),
                scopedReviews=len(review_index), exactReviewReferences=len(exact_refs),
                referencedPrintings=len(subjects), errors=errors, warnings=warnings)


def check_documents(root):
    errors = []
    for name in CANONICAL_DOCS:
        path = root/name
        if not path.is_file() or path.is_symlink():
            errors.append('Missing regular governance document: '+name)
            continue
        for target in re.findall(r'\[[^\]]+\]\(([^\s)]+)\)', path.read_text()):
            parsed = urlsplit(target.strip('<>'))
            if parsed.scheme or not parsed.path:
                continue
            linked = (path.parent/unquote(parsed.path)).resolve()
            if not linked.is_relative_to(root.resolve()) or not linked.exists():
                errors.append('Broken governance reference: '+name+' -> '+target)
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--date', default='2026-10-11', help='Recorded ledger date; not a claim of live currency')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    day = date.fromisoformat(args.date).isoformat()
    root = args.root.resolve()
    evidence = root/'docs/evidence'/('rules-reaudit-'+day)
    upstream = root/'data/official/upstream'/day
    paths = {key: evidence/name for key, name in {
        'cards': 'all-card-ledger.json', 'rules': 'all-rule-ledger.json',
        'corpus': 'official-corpus.json', 'reviews': 'review-decisions.json',
        'summary': 'comprehensive-summary.json'}.items()}
    paths.update({key: upstream/name for key, name in {
        'cn': 'card-catalog.zh-CN.json', 'en': 'card-gallery.en-US.json',
        'global': 'global-rules-sources.json', 'qa': 'card-qa.zh-CN.json'}.items()})
    paths['active'] = root/'data/official/card-catalog.zh-CN.json'
    try:
        data = {key: json.loads(path.read_text()) for key, path in paths.items()}
        result = validate(data, root)
        result['errors'].extend(check_documents(root))
        result['governanceValid'] = not result['errors']
        result['inputSha256'] = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths.values()}
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        result = dict(governanceValid=False, semanticCoverageCertified=False,
                      errors=[type(error).__name__+': '+str(error)], warnings=[])
    result['checkerSha256'] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    rendered = json.dumps(result, ensure_ascii=False, indent=2)+'\n'
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered)
    print(rendered, end='')
    return 0 if result['governanceValid'] else 1


if __name__ == '__main__':
    sys.exit(main())
