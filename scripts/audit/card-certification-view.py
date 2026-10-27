#!/usr/bin/env python3
"""Small whole-card view over retained sources; no registry-to-certification inference."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
SEMANTIC_FIELDS = ('cardName', 'subTitle', 'cardEffect', 'energy', 'returnEnergy', 'power',
                   'cardColorList', 'hero', 'region', 'tag', 'cardGroupLimit', 'cardQaList',
                   'errata', 'otherDesc')
STATES = {'UNIMPLEMENTED', 'IMPLEMENTED_PENDING_VERIFICATION', 'SEMANTIC_REVIEWED_PENDING_INTEGRATION',
          'FIXED_CANDIDATE_CERTIFIED'}


def normalized(value):
    return value.replace('·', '-').upper()


def hashed(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True).encode()).hexdigest()


def read_pin(root, ref):
    path = (root/ref['path']).resolve()
    if not path.is_relative_to(root.resolve()) or not path.is_file() or (root/ref['path']).is_symlink():
        raise ValueError('Missing or escaped source: '+ref['path'])
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != ref['sha256']:
        raise ValueError('Source hash mismatch: '+ref['path'])
    return json.loads(raw)


def build(root, ledger):
    if ledger.get('schemaVersion') != 1:
        raise ValueError('Unsupported card certification ledger')
    read_pin(root, ledger['scope']['sourceManifest'])
    source = {k: read_pin(root, ledger['sources'][k]) for k in ('cards', 'cn', 'en')}
    cn = {normalized(c['cardNo']): c for c in source['cn']['cards']}
    if len(cn) != len(source['cn']['cards']):
        raise ValueError('Duplicate CN printing identity')
    runtime_path = root/'data/official/card-catalog.zh-CN.json'
    runtime_exists = runtime_path.is_file()
    runtime = json.loads(runtime_path.read_text()) if runtime_exists else None
    if runtime_exists and (not isinstance(runtime, dict) or not isinstance(runtime.get('cards'), list)):
        raise ValueError('Invalid runtime catalog: expected a cards list')
    active = {normalized(c['cardNo']) for c in runtime['cards']} if runtime is not None else None
    if runtime is not None and len(active) != len(runtime['cards']):
        raise ValueError('Duplicate runtime printing identity')
    groups, mapping = {}, {}
    for printing in source['cards']['records']:
        identity = printing['id']
        if identity in mapping:
            raise ValueError('Duplicate retained printing identity')
        card = cn.get(identity)
        if card and all(k in card for k in SEMANTIC_FIELDS) and isinstance(card['cardEffect'], str) and card.get('cardName') and (card.get('cardCategoryList') or card.get('cardCategory')):
            semantics = {k: card.get(k) for k in SEMANTIC_FIELDS}
            semantics['category'] = card.get('cardCategoryList') or [card.get('cardCategory')]
            family = re.match(r'^([A-Z]+-\d+)', identity)
            # Same official numeric family AND byte-exact semantic fields. Never name-only.
            key = 'CN:'+family[1]+':'+hashed(semantics)[:16] if family else 'PRINTING:'+identity
            name = card['cardName']
            proof = 'SAME_CN_NUMERIC_FAMILY_AND_EXACT_SEMANTIC_FIELDS' if family else 'NO_FAMILY_MAPPING'
        else:
            key, name, proof = 'PRINTING:'+identity, identity, 'NO_CONFIRMED_CN_FUNCTION_MAPPING'
        group = groups.setdefault(key, dict(functionId=key, name=name, printings=[],
            mappingBasis=proof, applicability='UNKNOWN_PENDING_REGIONAL_EFFECTIVE_DISPOSITION',
            state='UNREVIEWED', wholeCardCertified=False, activePrintings=[]))
        group['printings'].append(identity)
        if (active is not None and identity in active) or (active is None and printing.get('activeCardNo')):
            group['activePrintings'].append(identity)
        mapping[identity] = key
    seen = set()
    for decision in ledger['certifications']:
        key = decision['functionId']
        if key not in groups or key in seen or decision['state'] not in STATES:
            raise ValueError('Unknown, duplicate or invalid whole-card decision')
        seen.add(key)
        group = groups[key]
        if sorted(decision['printings']) != sorted(group['printings']):
            raise ValueError('Decision does not map every exact printing')
        if not decision.get('sourceRefs') or not decision.get('effectAndKeywordObligations'):
            raise ValueError('Whole-card decision needs explicit sources and all-effect review')
        group.update(state=decision['state'], review=decision)
        if decision['state'] == 'FIXED_CANDIDATE_CERTIFIED':
            # This view intentionally cannot promote a scoped receipt or registry flag.
            # Candidate certification adapter must be implemented and tested with L3 first.
            raise ValueError('Fixed-candidate whole-card certification adapter not yet implemented')
    rows = sorted(groups.values(), key=lambda x: x['functionId'])
    return dict(schemaVersion=1, semanticCoverageCertified=False,
        scope=ledger['scope'], runtimeCatalog=dict(path=str(runtime_path.relative_to(root)),
        sha256=hashlib.sha256(runtime_path.read_bytes()).hexdigest(), entries=len(active),
        outsideRetainedBase=sorted(active-set(mapping))) if active is not None else None, counts=dict(retainedPrintings=len(mapping), provisionalFunctionGroups=len(rows),
        unresolvedMappingPrintings=sum(len(g['printings']) for g in rows if g['mappingBasis'] != 'SAME_CN_NUMERIC_FAMILY_AND_EXACT_SEMANTIC_FIELDS'),
        applicableFunctionCount=None, certifiedWholeCards=0, uncertifiedFunctionGroups=len(rows),
        states=dict(Counter(g['state'] for g in rows))),
        limitations=['Function groups are conservative source identities, not a verified applicable denominator.',
                    'English-only mappings, effective dates, all-effect review and candidate certification remain open.',
                    'Supplemental printing/news obligations remain in sourceManifest; this view counts only its retained 1740-printing base, not the entire later-source denominator.'],
        functions=rows)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root', type=Path, default=ROOT)
    p.add_argument('--ledger', type=Path, default=Path('docs/audit/card-certification-ledger.json'))
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    result = build(a.root, json.loads((a.root/a.ledger).read_text()))
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(result['counts'], ensure_ascii=False))


if __name__ == '__main__':
    main()
