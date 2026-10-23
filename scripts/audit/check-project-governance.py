#!/usr/bin/env python3
"""Check source-index integrity and bounded claims; never certify gameplay.

Reads the recorded snapshot only. No source refresh, rule tests, database calls,
ledger rewrites or inheritance of scoped PASS into whole-card coverage.
"""
import argparse
from collections import Counter
from datetime import date
import gzip
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import sys
import subprocess
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
    scope = data.get('scope')
    if scope is None:
        require(all(item['date'] == snapshot_date for item in (rules, corpus, summary)),
                'Snapshot dates differ across indexes')
        applicable_date = snapshot_date
    else:
        applicable_date = date.fromisoformat(scope['applicableAsOf']).isoformat()
        require(bool(scope['region']) and bool(scope['language']), 'Missing regional certification scope')
        for key in ('cards', 'rules', 'corpus', 'summary'):
            require(data[key]['date'] == scope['sourceDates'][key], 'Declared source date mismatch: '+key)
            require(data[key]['date'] <= applicable_date, 'Source index newer than certification scope: '+key)

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
        if effective and effective > applicable_date:
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
        require(review.get('semanticReview', {}).get('wholeCardCertified', False) is False,
                'Structured scoped review cannot claim whole-card certification: '+identity)

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


def read_pin_bytes(root, ref):
    path = (root/ref['path']).resolve()
    if not path.is_relative_to(root.resolve()) or not path.is_file() or (root/ref['path']).is_symlink():
        raise ValueError('Missing or escaped pinned input: '+ref['path'])
    raw = path.read_bytes()
    if not HASH.fullmatch(ref['sha256']) or hashlib.sha256(raw).hexdigest() != ref['sha256']:
        raise ValueError('Pinned input hash mismatch: '+ref['path'])
    return raw


def read_pin(root, ref):
    return json.loads(read_pin_bytes(root, ref))


def official_news_article(raw):
    class Article(HTMLParser):
        void = {'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta',
                'param', 'source', 'track', 'wbr'}

        def __init__(self):
            super().__init__()
            self.stack, self.text, self.images = [], [], []
            self.found = self.closed = False

        def handle_starttag(self, tag, attrs):
            attrs = dict(attrs)
            if not self.stack:
                if not self.closed and attrs.get('id') == 'js_content':
                    self.found, self.stack = True, [tag]
                return
            if tag == 'img' and (url := attrs.get('data-src') or attrs.get('src')):
                self.images.append(url)
            if tag not in self.void:
                self.stack.append(tag)

        def handle_endtag(self, tag):
            if tag not in self.void and tag in self.stack:
                self.stack = self.stack[:len(self.stack)-1-self.stack[::-1].index(tag)]
                self.closed = not self.stack

        def handle_startendtag(self, tag, attrs):
            self.handle_starttag(tag, attrs)
            if tag not in self.void:
                self.handle_endtag(tag)

        def handle_data(self, value):
            if self.stack and not {'script', 'style'}.intersection(self.stack) and value.strip():
                self.text.append(value.strip())

    article = Article()
    article.feed(raw.decode('utf-8'))
    if not article.found or not article.closed or not (article.text or article.images):
        raise ValueError('Official news article root missing, incomplete or empty')
    return '\n'.join(article.text), article.images


def official_news_obligations(root, ref, scope, card_source, card_ids):
    """Retain each acquired paragraph/image and each unresolved source separately.

    This intake cannot certify behavior. Semantic decisions still need the
    existing review, implementation, test, fixed-CI and product evidence paths.
    """
    ledger = read_pin(root, ref)
    if (ledger.get('schemaVersion') != 1 or ledger.get('kind') != 'OFFICIAL_CN_NEWS_SOURCE_OBLIGATIONS'
        or ledger.get('semanticCoverageCertified') is not False or ledger.get('cardSource') != card_source
        or any(ledger.get(k) != scope[k] for k in ('region', 'language', 'applicableAsOf'))):
        raise ValueError('News ledger scope, source pin or certification claim mismatch')
    inventory = read_pin(root, ledger['candidateInventory'])['rulesCaseCandidates']
    expected = {'CN-NEWS-'+hashlib.sha256(r['jumpUrl'].encode()).hexdigest()[:16]:r for r in inventory}
    listings = [read_pin(root, item)['result'] for item in ledger['newsListingResponses']]
    listed_rows = [row for page in listings for row in page['list']]
    all_news = {'CN-NEWS-'+hashlib.sha256(row['jumpUrl'].encode()).hexdigest()[:16]:row for row in listed_rows}
    if (not listings or any(page['total'] != len(listed_rows) for page in listings)
        or len(all_news) != len(listed_rows) or any(all_news.get(k) != row for k, row in expected.items())):
        raise ValueError('Official news listing is incomplete, duplicated or differs from candidate metadata')
    documents = index(ledger['documents'], 'official news documents')
    if len(expected) != len(inventory) or set(expected) != set(documents):
        raise ValueError('Official news source universe lost or gained an identity')
    remaining = []
    for identity, row in all_news.items():
        if identity not in expected:
            remaining.append(dict(id='NEWS:'+identity, kind='OFFICIAL_NEWS_UNCLASSIFIED_SOURCE',
                                  state='OPEN', sourceLedger=ref, sourceDocument=identity, sourceRecord=row,
                                  region=ledger['region'], language=ledger['language'],
                                  applicability='OPEN_BODY_AND_RULE_RELEVANCE_NOT_REVIEWED',
                                  semanticReview='OPEN', implementationEvidence=[], testEvidence=[],
                                  fixedCommitCi='OPEN_FOR_APPLICABLE_BEHAVIOR', productAcceptance='OPEN',
                                  wholePrintingCertified=False, globalCertification=False))
    for identity, doc in documents.items():
        listed = expected[identity]
        if (doc['url'] != listed['jumpUrl'] or doc['titleAsListed'] != listed['name']
            or doc['publishedAsListed'] != listed['pushDate']
            or any(doc.get(k) != scope[k] for k in ('region', 'language'))
            or doc.get('auditApplicableAsOf') != scope['applicableAsOf']
            or doc.get('semanticReview') != 'OPEN' or doc.get('wholePrintingCertified') is not False):
            raise ValueError('News source metadata or bounded-claim mismatch: '+identity)
        acquisition = doc['acquisitionState']
        if acquisition not in {'ARTICLE_RETRIEVED', 'SOURCE_GAP', 'RULES_HUB_RESPONSE_NOT_INDEPENDENT_QA'}:
            raise ValueError('Unsupported news acquisition disposition: '+identity)
        if acquisition != 'SOURCE_GAP':
            archive = doc['archivedHtml']
            raw = gzip.decompress(read_pin_bytes(root, archive['gzip']))
            if hashlib.sha256(raw).hexdigest() != archive['rawSha256'] or len(raw) != archive['rawBytes']:
                raise ValueError('News original HTML byte identity mismatch: '+identity)
        if acquisition != 'ARTICLE_RETRIEVED' and (doc['blocks'] or doc['images']):
            raise ValueError('Unretrieved news body cannot acquire paragraph or image evidence: '+identity)
        if acquisition == 'RULES_HUB_RESPONSE_NOT_INDEPENDENT_QA' and doc.get('independentFaqBodyRetrieved') is not False:
            raise ValueError('Rules hub response cannot certify an independent FAQ body')
        common = dict(sourceLedger=ref, sourceDocument=identity, url=doc['url'], region=doc['region'],
                      language=doc['language'], applicability=doc['applicability'], semanticReview='OPEN',
                      implementationEvidence=[], testEvidence=[], fixedCommitCi='OPEN_FOR_APPLICABLE_BEHAVIOR',
                      productAcceptance='OPEN', wholePrintingCertified=False, globalCertification=False)
        remaining.append(dict(common, id='NEWS:'+identity, kind='OFFICIAL_NEWS_SOURCE',
                              acquisitionState=acquisition, state='OPEN'))
        blocks = index(doc['blocks'], 'news text blocks: '+identity)
        images = index(doc['images'], 'news image references: '+identity)
        if acquisition == 'ARTICLE_RETRIEVED':
            text, image_urls = official_news_article(raw)
            if text != read_pin_bytes(root, doc['bodyText']).decode('utf-8'):
                raise ValueError('News text extraction differs from original HTML: '+identity)
            if [i['url'] for i in images.values()] != image_urls:
                raise ValueError('News image extraction differs from original HTML: '+identity)
            lines = text.splitlines()
            covered = []
            for block in blocks.values():
                first, last = block['firstLine'], block['lastLine']
                if (not isinstance(first, int) or not isinstance(last, int) or not 1 <= first <= last <= len(lines)
                    or block.get('semanticReview') != 'OPEN' or block.get('wholePrintingCertified') is not False
                    or block.get('implementationEvidence') or block.get('testEvidence')):
                    raise ValueError('Invalid news text range or intake certification claim: '+block['id'])
                text = '\n'.join(lines[first-1:last])
                if hashlib.sha256(text.encode()).hexdigest() != block['textSha256']:
                    raise ValueError('News paragraph identity mismatch: '+block['id'])
                if any(p['printing'] not in card_ids for p in block['exactNamePrintingCandidates']):
                    raise ValueError('News paragraph references an unknown printing: '+block['id'])
                covered.extend(range(first, last+1))
                remaining.append(dict(common, id='NEWS:'+block['id'], kind='OFFICIAL_NEWS_TEXT_BRANCH',
                                      sourceVersion=doc['bodyText'], sourceRecord=block, state='OPEN'))
            if sorted(covered) != list(range(1, len(lines)+1)):
                raise ValueError('News text was dropped or covered more than once: '+identity)
            if not lines and not images:
                raise ValueError('Retrieved article has no text or image evidence: '+identity)
        if sorted(image['ordinal'] for image in images.values()) != list(range(1, len(images)+1)):
            raise ValueError('News image ordinals were dropped or duplicated: '+identity)
        for item in images.values():
            if (not item['url'].startswith('https://')
                or item.get('semanticReview') not in {'OPEN', 'OPEN_CURRENT_VERSION_RECONCILIATION'}):
                raise ValueError('News image requires an exact HTTPS source URL: '+item['id'])
            read_pin_bytes(root, item['bytes'])
            remaining.append(dict(common, id='NEWS:'+item['id'], kind='OFFICIAL_NEWS_IMAGE_BRANCH',
                                  sourceVersion=item['bytes'], sourceRecord=item, state='OPEN'))
    if len({row['id'] for row in remaining}) != len(remaining):
        raise ValueError('Duplicate unified news obligation identity')
    return remaining


def source_delta(before, after, kind):
    if kind not in {'rules', 'cards', 'cn_cards'}:
        raise ValueError('Unsupported source delta kind: '+str(kind))
    key = 'url' if kind == 'rules' else 'id'
    a = {r[key]: r for r in before['records' if kind == 'rules' else 'cards']}
    b = {r[key]: r for r in after['records' if kind == 'rules' else 'data']}
    if len(a) != len(before['records' if kind == 'rules' else 'cards']) or len(b) != len(after['records' if kind == 'rules' else 'data']):
        raise ValueError('Duplicate source delta identity')
    changed = []
    for identity in sorted(a.keys() & b.keys()):
        fields = sorted(k for k in a[identity].keys() | b[identity].keys()
                        if (k in a[identity]) != (k in b[identity]) or a[identity].get(k) != b[identity].get(k))
        if not fields:
            continue
        item = dict(id=identity, changedFields=fields)
        if kind == 'rules':
            item.update(previousContentHash=a[identity]['sha256'], currentContentHash=b[identity]['sha256'],
                        extractedTextIdentical=a[identity]['text'] == b[identity]['text'])
        else:
            item['printing'] = b[identity]['cardNo' if kind == 'cn_cards' else 'publicCode']
        changed.append(item)
    result = dict(kind=kind, addedIds=sorted(b.keys()-a.keys()), removedIds=sorted(a.keys()-b.keys()),
                  changed=changed, applicability='REQUIRES_EXPLICIT_VERSION_DISPOSITION', semanticCertification=False)
    if kind in {'cards', 'cn_cards'}:
        result.update(reportedTotal=after['metadata']['totalItems'], returnedEntries=len(b),
                      reportedMinusReturned=after['metadata']['totalItems']-len(b))
    return result


def gameplay_path(path):
    parts = Path(path).parts
    if any(p in {'bin','obj','.godot'} for p in parts):
        return False
    # This binds source-tree inputs, not packaged runtime files. Generated
    # exports and copied client catalogs are verified by the runtime gate.
    if path.startswith(('clients/godot/exports/','clients/godot/data/')):
        return False
    roots = ('src/Riftbound.Contracts/','src/Riftbound.CardCatalog/','src/Riftbound.Engine/',
             'src/Riftbound.Persistence/','src/Riftbound.Api/','clients/godot/')
    return (path.startswith(roots) and Path(path).suffix in {'.cs','.csproj','.json','.sql','.tscn','.godot'}
            or path.startswith('data/official/') and not path.startswith('data/official/upstream/')
                and Path(path).suffix == '.json'
            or path in {'Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json'})


def gameplay_inputs(root):
    return {p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest()
            for folder in ('src','clients/godot','data/official') for p in (root/folder).rglob('*')
            if p.is_file() and gameplay_path(p.relative_to(root).as_posix())} | {
                name:hashlib.sha256((root/name).read_bytes()).hexdigest()
                for name in ('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json')
                if (root/name).is_file()}


def source_fingerprints(refs, cards, rules, corpus):
    return {ref: digest(cards[ref]['sources']) if ref in cards else rules[ref]['textHash'] if ref in rules
            else digest(corpus[ref]['text']) for ref in refs}


def branch_obligation(branch, context, covered, evidence, scope, cards, rules, corpus):
    decision = branch.get('decision',{})
    state, reason = 'OPEN','Review decision or evidence is incomplete'
    if any(ref not in cards and ref not in rules and ref not in corpus for ref in branch['sourceRefs']):
        return dict(id=branch['id'],state='OPEN',reason='Missing source identity',sourceRefs=branch['sourceRefs'],
                    wholePrintingCertified=False,globalCertification=False)
    sources_match = decision.get('sourceFingerprints') == source_fingerprints(branch['sourceRefs'],cards,rules,corpus)
    if decision.get('state') == 'NOT_APPLICABLE':
        minimum = decision.get('minimumPlayers')
        if (sources_match and decision.get('reason') and isinstance(minimum,int)
            and minimum > scope.get('productPlayers',0) >= 2):
            state, reason = 'NOT_APPLICABLE',decision['reason']
    elif decision.get('state') == 'VERIFIED':
        check, native, focus = (evidence.get(k,{}) for k in ('check','native','focus'))
        methods = set(focus.get('newMethodNames',[]))
        required = set(decision.get('requiredMethods',[]))
        native_branches = {r['branch'] for r in native.get('records',[])}
        if (sources_match and covered and decision.get('reason') and required and required <= methods
            and check.get('status') == 'CHECK_AND_EXACT_CASE_METHOD_PARITY_PASS'
            and check.get('oldCasesPreserved') is True and check.get('oldMethodsPreserved') is True
            and check.get('frozenInputsUnchanged') is True and check.get('failed') == check.get('skipped') == 0
            and (not decision.get('requiredNativeBranches') or native.get('status') == 'NATIVE_PRODUCTION_AND_BACKEND_FULL_JOURNAL_READBACK_PASS'
                 and set(decision['requiredNativeBranches']) <= native_branches)):
            state, reason = 'VERIFIED',decision['reason']
        else:
            reason = 'Source, current CI/gameplay inputs or required execution evidence does not match'
    return dict(id=branch['id'],state=state,reason=reason,sourceRefs=branch['sourceRefs'],
                wholePrintingCertified=False,globalCertification=False)


def retained_implementation_baseline(root, manifest):
    """Authenticate historical pins without claiming unchanged current code."""
    config_path = root/'docs/audit/legacy-source-baseline.json'
    if not config_path.exists():
        return None, None, None
    if config_path.is_symlink():
        raise ValueError('Retained baseline must be a regular configuration')
    config = json.loads(config_path.read_text())
    commit = config.get('commit', '')
    manifest_path = 'docs/audit/current-manifest.json'
    if (config.get('schemaVersion') != 1 or not re.fullmatch(r'[0-9a-f]{40}', commit)
            or not HASH.fullmatch(config.get('manifestSha256', ''))):
        raise ValueError('Invalid retained implementation baseline identity')
    def historical(path):
        try:
            return subprocess.check_output(['git', 'show', commit+':'+path], cwd=root,
                                           stderr=subprocess.PIPE)
        except subprocess.CalledProcessError as error:
            raise ValueError('Retained baseline unavailable or missing file: '+path) from error
    raw = historical(manifest_path)
    if hashlib.sha256(raw).hexdigest() != config['manifestSha256'] or json.loads(raw) != manifest:
        raise ValueError('Retained baseline manifest changed; a new source review is required')
    if config.get('retainedInputRoles', []) not in ([], ['active']):
        raise ValueError('Only the historical active-catalog role may use a retained input')
    cache = {}
    def verify(ref):
        path = ref['path']
        if Path(path).is_absolute() or '..' in Path(path).parts or not HASH.fullmatch(ref['sha256']):
            return False
        if path not in cache:
            cache[path] = hashlib.sha256(historical(path)).hexdigest()
        return cache[path] == ref['sha256']
    return config, verify, historical


def current_audit(root, manifest):
    if manifest.get('schemaVersion') != 1:
        raise ValueError('Unsupported current audit manifest schema')
    retained, verify_retained, read_retained = retained_implementation_baseline(root, manifest)
    changed_contexts = []
    scope = manifest['certificationScope']
    if manifest.get('semanticCoverageCertified') is not False or scope.get('allOfficialObligationsRetained') is not True:
        raise ValueError('Index manifest cannot certify semantics or discard official obligations')
    data, runtime_catalog_delta = {}, None
    for key, ref in manifest['inputs'].items():
        if retained is None or key not in retained.get('retainedInputRoles', []):
            data[key] = read_pin(root, ref)
            continue
        raw = read_retained(ref['path'])
        if hashlib.sha256(raw).hexdigest() != ref['sha256']:
            raise ValueError('Retained source input hash mismatch: '+key)
        data[key] = json.loads(raw)
        path = (root/ref['path']).resolve()
        if not path.is_relative_to(root.resolve()) or not path.is_file() or (root/ref['path']).is_symlink():
            raise ValueError('Missing or escaped current runtime catalog')
        current_raw = path.read_bytes()
        current = json.loads(current_raw)
        before = {number(c['cardNo']) for c in data[key]['cards']}
        after = {number(c['cardNo']) for c in current['cards']}
        if len(after) != len(current['cards']):
            raise ValueError('Duplicate current runtime catalog printing')
        runtime_catalog_delta = dict(path=ref['path'], retainedCommit=retained['commit'],
            retainedSha256=ref['sha256'], currentSha256=hashlib.sha256(current_raw).hexdigest(),
            retainedEntries=len(before), currentEntries=len(after),
            addedPrintings=sorted(after-before), removedPrintings=sorted(before-after),
            wholeCardCertification=False)

    required = {'cards','rules','corpus','reviews','summary','cn','en','global','qa','active'}
    if set(data) != required:
        raise ValueError('Current manifest input roles differ: '+str(sorted(set(data)^required)))
    data['scope'] = scope
    result = validate(data, root)
    qa = {str(r['id']): r for r in data['qa']['entries']}
    cn = {str(r['id']): r for r in data['cn']['cards']}
    if set(qa) != set(cn) or len(qa) != len(data['qa']['entries']) or any(
        qa[k]['cardNo'] != cn[k]['cardNo'] or qa[k]['cardQaList'] != cn[k].get('cardQaList') for k in cn):
        result['errors'].append('Current QA identity/content does not match CN catalog')
    if data['qa']['catalogSha256'] != manifest['inputs']['cn']['sha256']:
        result['errors'].append('Current QA catalog pin mismatch')
    historical = read_pin(root, manifest['historicalReviewPin'])
    coverage = read_pin(root, manifest['cardCoverageManifest'])
    if coverage['scopedReviewDecisions']['sha256'] != manifest['historicalReviewPin']['sha256']:
        result['errors'].append('Historical review pin was silently changed')
    for key in ('combinedExactPrintingNumbers','cnOfficialPrintings','enReturnedPrintings','enReportedTotal','enSourceCountGap','activeCatalogEntries'):
        if coverage['counts'][key] != result['counts'][key]:
            result['errors'].append('Current card manifest count mismatch: '+key)
    deltas = []
    for delta in manifest['laterSourceDeltas']:
        before, after = read_pin(root, delta['before']), read_pin(root, delta['after'])
        value = source_delta(before, after, delta['kind'])
        value['inputRefs'] = dict(before=delta['before'], after=delta['after'])
        deltas.append(value)
    cards = index(data['cards']['records'], 'current card ledger')
    rules = index(data['rules']['records'], 'current rule ledger')
    corpus = index(data['corpus']['records'], 'current corpus')
    subjects, reviewed_rules, reviews = {}, {}, []
    contexts = manifest['reviewContexts']
    actual_inputs = gameplay_inputs(root)
    raw_reviews = index(data['reviews']['reviews'], 'current reviews')
    branch_rows = []
    if set(contexts)-set(raw_reviews):
        result['errors'].append('Context maps a missing review')
    for identity, review in raw_reviews.items():
        ids = [number(s if isinstance(s,str) else s.get('id',s.get('activeCardNo'))) for s in review.get('subjects',[])]
        refs = set(review.get('ruleRecords',[])+review.get('pendingRuleRecords',[]))
        refs.update(r.get('id') if isinstance(r,dict) else r for r in review.get('rules',[])
                    if isinstance(r,str) and (r in rules or r in corpus) or isinstance(r,dict) and r.get('id') in corpus)
        for printing in ids:
            subjects.setdefault(printing,[]).append(identity)
        for ref in refs:
            reviewed_rules.setdefault(ref,[]).append(identity)
        context = contexts.get(identity)
        branches = []
        covered = False
        if context:
            commits = [context.get('evidenceCommit'),context.get('currentImplementationCommit')]
            if any(not isinstance(c,str) or not re.fullmatch(r'[0-9a-f]{40}',c) for c in commits):
                result['errors'].append('Missing or invalid evidence/implementation commit: '+identity)
            if context['ciState'] not in {'OPEN','PENDING','VERIFIED'}:
                result['errors'].append('Unsupported separate CI state: '+identity)
            evidence = {ref['role']:read_pin(root,ref) for ref in context['evidence']}
            for ref in context['ciReceipts']:
                receipt = read_pin(root,ref)
                if (receipt.get('status') != 'FIXED_COMMIT_FULL_CI_AND_PARITY_VERIFIED'
                    or receipt.get('commit') != ref['commit'] or receipt.get('runId') != ref['runId']
                    or receipt.get('commit') != context.get('evidenceCommit')
                    or not receipt.get('jobs') or any(s != 'success' for s in receipt['jobs'].values())):
                    result['errors'].append('CI receipt does not match the pinned successful commit: '+identity)
                inputs = receipt.get('gameplaySourceInputs')
                if inputs is None or inputs.get('commit') != receipt.get('commit') or not inputs.get('files'):
                    result['errors'].append('CI receipt lacks corresponding gameplay input identity: '+identity)
                elif inputs['files'] == actual_inputs and context.get('currentImplementationCommit') == receipt['commit']:
                    covered = True
            if context['ciState'] == 'VERIFIED' and not context['ciReceipts']:
                result['errors'].append('Verified CI has no exact commit receipt: '+identity)
            implementation_unchanged = True
            for ref in context['implementation']:
                path = (root/ref['path']).resolve()
                safe = path.is_relative_to(root.resolve()) and not (root/ref['path']).is_symlink()
                matches = safe and path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest() == ref['sha256']
                implementation_unchanged = implementation_unchanged and matches
                if not safe or (verify_retained is not None and not verify_retained(ref)) or (verify_retained is None and not matches):
                    result['errors'].append('Scoped implementation pin mismatch: '+ref['path'])
            if not implementation_unchanged:
                changed_contexts.append(identity)
                covered = False
            if len({b['id'] for b in context['branches']}) != len(context['branches']) or not context['implementation']:
                result['errors'].append('Duplicate behavior branch or missing implementation context: '+identity)
            for branch in context['branches']:
                if not branch['id'] or not branch['sourceRefs'] or any(r not in rules and r not in corpus and r not in cards for r in branch['sourceRefs']):
                    result['errors'].append('Missing or dangling behavior branch source: '+identity)
                branches.append(branch)
            covered = covered and not result['errors'] and context['ciState'] == 'VERIFIED'
            if runtime_catalog_delta and runtime_catalog_delta['currentSha256'] != runtime_catalog_delta['retainedSha256']:
                covered = False
            for branch in branches:
                row = branch_obligation(branch,context,covered,evidence,scope,cards,rules,corpus)
                branch_rows.append(dict(id='BRANCH:'+identity+':'+row['id'],kind='BEHAVIOR_BRANCH',
                                        review=identity,decision=row))
        reviews.append(dict(id=identity, scope=review['scope'], subjects=ids, ruleRefs=sorted(refs),
            semanticReview=dict(state='SCOPED_RECORD', wholeCardCertified=False,
                                sourceVersionResolution='PINNED_CONTEXT' if context else 'REQUIRES_EVIDENCE_ADAPTER'),
            testExecution=dict(state='RECORDED_RECEIPTS' if context else 'REQUIRES_EVIDENCE_ADAPTER',
                               historicalClaim=review.get('testExecution',review.get('validation',{}))),
            fixedCommitCi=dict(state=context.get('ciState','OPEN') if context else 'REQUIRES_EXACT_COMMIT_RECEIPT',
                               currentImplementationCovered=covered, coverageBasis='EXACT_COMMIT_AND_GAMEPLAY_SOURCE_INPUTS',
                               receipts=context.get('ciReceipts',[]) if context else []),
            productAcceptance=dict(state='OPEN', required=['VISIBLE_PAIRED_SQL_NETWORK_SCORE_REPLAY','USER_WINDOWS_RUNTIME']),
            branches=[r['decision'] for r in branch_rows if r['review']==identity],
            implementation=context['implementation'] if context else review.get('implementation',[]),
            evidence=context['evidence'] if context else review.get('evidence',[]),
            remaining=['WHOLE_PRINTING_AND_CLAUSE_SEMANTICS','PRODUCT_ACCEPTANCE'] + ([] if context else ['SOURCE_BRANCH_AND_EVIDENCE_MAPPING']),
            recordedLegacyStatus=review.get('status')))
    def rule_review_references(identity, source_record):
        associated = set(reviewed_rules.get(identity, [])) | set(reviewed_rules.get(source_record, []))
        return [review for review in raw_reviews if review in associated]

    remaining = [dict(id='CARD:'+k,kind='PRINTING',sourceLedger=manifest['inputs']['cards'],sourceRecord=k,
                      sourceVersions=v['sources'],semanticReview='NOT_CERTIFIED',behaviorBranchMapping='REQUIRED',
                      scopedReviewRefs=subjects.get(k,[]),applicability='REQUIRES_REGIONAL_DATE_DISPOSITION') for k,v in cards.items()]
    remaining += [dict(id='RULE:'+k,kind='RULE_RECORD',sourceLedger=manifest['inputs']['rules'],sourceRecord=k,
                       document=v['document'],textHash=v['textHash'],semanticReview='NOT_CERTIFIED',behaviorBranchMapping='REQUIRED',
                       scopedReviewRefs=rule_review_references(k,v.get('sourceRecord')),
                       applicability='REQUIRES_DOCUMENT_AND_CLAUSE_DISPOSITION') for k,v in rules.items()]
    for gate in manifest['acceptanceGates']:
        if gate['state'] not in {'OPEN','BLOCKED_EXTERNAL'} or not gate['id'] or not gate['requirement']:
            result['errors'].append('Unsupported or incomplete acceptance gate')
        remaining.append({**gate, 'id':'GATE:'+gate['id'], 'kind':'ACCEPTANCE_GATE'})
    for ordinal, delta in enumerate(deltas):
        remaining.append(dict(id='DELTA:'+str(ordinal),kind='LATER_SOURCE_VERSION',state='OPEN',sourceDelta=delta))
    remaining += branch_rows
    if 'officialNewsObligations' in manifest:
        remaining += official_news_obligations(root, manifest['officialNewsObligations'], scope,
                                              manifest['inputs']['cn'], set(cards))
    result['errors'].extend(check_documents(root))
    result.update(governanceValid=not result['errors'],executionScope='CURRENT_MANIFEST_INDEX_AND_REMAINING_OBLIGATIONS_ONLY',
                  retainedImplementationBaseline=retained, changedImplementationContexts=changed_contexts,
                  runtimeCatalogDelta=runtime_catalog_delta,
                  certificationScope=scope, historicalReviewPinCount=len(historical['reviews']),
                  currentReviewCount=len(reviews), recordedStatusStrings=len({r.get('status') for r in raw_reviews.values()}),
                  cardCoverageManifest=manifest['cardCoverageManifest'], inputManifest=manifest,
                  reviewView=reviews, remainingObligations=remaining, laterSourceDeltas=deltas,
                  branchObligationStates=dict(Counter(r['decision']['state'] for r in branch_rows)),
                  unresolvedBehaviorBranches=[r['id'] for r in branch_rows if r['decision']['state']=='OPEN'],
                  semanticCoverageCertified=False, completionRate=None)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    choice = parser.add_mutually_exclusive_group()
    choice.add_argument('--date', help='Explicit historical ledger check; not current manifest validation')
    choice.add_argument('--manifest', type=Path, help='Current certification manifest; defaults to docs/audit/current-manifest.json')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    if not args.date:
        try:
            path = args.manifest or root/'docs/audit/current-manifest.json'
            if not path.is_absolute():
                path = root/path
            result = current_audit(root, json.loads(path.read_text()))
        except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
            result = dict(governanceValid=False, semanticCoverageCertified=False, errors=[type(error).__name__+': '+str(error)], warnings=[])
        result['checkerSha256'] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
        rendered = json.dumps(result, ensure_ascii=False, indent=2)+'\n'
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(rendered)
        compact = {k:v for k,v in result.items() if k not in {'inputManifest','reviewView','remainingObligations','laterSourceDeltas'}}
        compact['remainingObligationCount'] = len(result.get('remainingObligations',[]))
        print(json.dumps(compact, ensure_ascii=False, indent=2))
        return 0 if result['governanceValid'] else 1
    day = date.fromisoformat(args.date).isoformat()
    evidence = root/'docs/evidence'/('rules-reaudit-'+day)
    upstream = root/'data/official/upstream'/day
    paths = {key: evidence/name for key, name in {
        'cards': 'all-card-ledger.json', 'rules': 'all-rule-ledger.json',
        'corpus': 'official-corpus.json', 'reviews': 'review-decisions.json',
        'summary': 'comprehensive-summary.json'}.items()}
    paths.update({key: upstream/name for key, name in {
        'cn': 'card-catalog.zh-CN.json', 'en': 'card-gallery.en-US.json',
        'global': 'global-rules-sources.json', 'qa': 'card-qa.zh-CN.json'}.items()})
    # The ledger certifies this recorded index only; current runtime promotion
    # must not silently change its active-printing universe.
    paths['active'] = upstream/'active-catalog.zh-CN.json'
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
