# -*- coding: utf-8 -*-
from pathlib import Path
import copy, datetime, gzip, hashlib, json, tarfile

root = Path('/Users/dinghaolin/MyProjects/riftbound')
w = Path(__file__).resolve().parent
ev = root / 'docs/evidence/controller-damage-current-cn16-2026-10-21'
load = lambda p: json.loads(p.read_text())
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
prior = w / 'prior-current-controller-publication-v1'
old = json.loads(gzip.decompress((prior / 'current-manifest.json.gz').read_bytes()))
old_reviews = json.loads(gzip.decompress((prior / 'review-decisions.json.gz').read_bytes()))
current = load(root / 'docs/audit/current-manifest.json')
reviews = load(root / current['inputs']['reviews']['path'])
assert len(reviews['reviews']) == 158 and reviews['reviews'][:157] == old_reviews['reviews']
assert len(current['reviewContexts']) == 73 and set(old['reviewContexts']) < set(current['reviewContexts'])
assert current['acceptanceGates'] == old['acceptanceGates'] and len(current['acceptanceGates']) == 245
assert current['laterSourceDeltas'] == old['laterSourceDeltas'] and len(current['laterSourceDeltas']) == 7
for key in old:
    if key not in {'reviewContexts', 'inputs'}: assert current[key] == old[key]
for key in old['inputs']:
    if key != 'reviews': assert current['inputs'][key] == old['inputs'][key]
assert current['inputs']['reviews']['sha256'] == sha(root / current['inputs']['reviews']['path'])
f = load(w / 'current-controller-frozen-runtime-inputs-v2.json')
stale = json.loads(gzip.decompress((prior / 'complete-current-contexts-before-input-refresh.json.gz').read_bytes()))
stale_ids = {v['id'] for v in stale}
assert len(stale_ids) == 37
unchanged = []
for identity, before in old['reviewContexts'].items():
    after = current['reviewContexts'][identity]
    if identity not in stale_ids:
        assert before == after
        unchanged.append(identity)
        continue
    for key in before:
        if key not in {'implementation', 'currentImplementationCommit', 'currentImplementationIncludesUncommittedChanges', 'ciState', 'branches'}:
            assert before[key] == after[key], (identity, key)
    assert after['currentImplementationCommit'] == f['parentCommit'] and after['currentImplementationIncludesUncommittedChanges'] and after['ciState'] == 'PENDING'
    assert len(before['implementation']) == len(after['implementation'])
    changes = []
    for a, b in zip(before['implementation'], after['implementation']):
        assert a['path'] == b['path'] and all(a[k] == b[k] for k in a if k != 'sha256')
        assert b['sha256'] == sha(root / b['path'])
        if a['sha256'] != b['sha256']: changes.append(a['path'])
    assert changes == ['src/Riftbound.Engine/CoreRuleEngine.cs']
    if identity != 'teemo-counted-current-cn16-2026-10-21': assert before['branches'] == after['branches']
assert len(unchanged) == 35
identity = 'teemo-counted-current-cn16-2026-10-21'
old_branches = {b['id']: b for b in old['reviewContexts'][identity]['branches']}
new_branches = {b['id']: b for b in current['reviewContexts'][identity]['branches']}
assert set(old_branches) == set(new_branches)
finding = 'supporting-own-on-field-annie-one-count-teemo-defense-global-skill-bonus-omitted'
for key in old_branches:
    if key != finding: assert old_branches[key] == new_branches[key]
before = old_branches[finding]; after = new_branches[finding]
for key in before:
    if key not in {'implementation', 'testExecution', 'fixedCommitCi', 'decision'}: assert before[key] == after[key]
assert before['boundedCase']['actualDamage'] == after['boundedCase']['actualDamage'] == 1
assert after['currentBoundedStageFollowup']['currentTestedNeutralOneCountDamage'] == 2 and after['decision']['state'] == 'OPEN'
followup = load(ev / 'prior-annie-finding-explicit-current-stage-followup.json')
assert followup['oldCompleteBranch'] == before and followup['newCompleteBranch'] == after
new_id = 'controller-damage-current-cn16-2026-10-21'
context = current['reviewContexts'][new_id]
assert len(context['branches']) == 50 and len({b['id'] for b in context['branches']}) == 50
assert all(b['decision']['state'] == 'OPEN' and b['wholePrintingCertified'] is False for b in context['branches'])
assert sum(b['boundedCase']['countedSqlExactSamePrintingCountAndLocalBonus'] for b in context['branches']) == 12
g = load(w / 'postpublication-current-governance-original-v1.json')
oldg = json.loads(gzip.decompress((root / 'docs/evidence/teemo-counted-current-cn16-2026-10-21/postpublication-current-governance-original.json.gz').read_bytes()))
assert g['governanceValid'] and not g['semanticCoverageCertified'] and g['errors'] == []
assert g['counts'] == oldg['counts'] and g['currentReviewCount'] == 158
old_ids = {r['id'] for r in oldg['remainingObligations']}; new_ids = {r['id'] for r in g['remainingObligations']}
assert len(old_ids) == 16992 and len(new_ids) == 17042 and old_ids <= new_ids
assert new_ids - old_ids == {'BRANCH:' + new_id + ':' + b['id'] for b in context['branches']}
mapping = load(ev / 'archive-map.json')
archive = root / mapping['archive']['path']; assert sha(archive) == mapping['archive']['sha256']
with tarfile.open(archive, 'r:gz') as tar:
    assert len(tar.getnames()) == len(set(tar.getnames())) == len(mapping['members'])
    assert set(tar.getnames()) == {v['path'] for v in mapping['members']}
    for row in mapping['members']:
        raw = tar.extractfile(row['path']).read(); assert len(raw) == row['bytes'] and hashlib.sha256(raw).hexdigest() == row['sha256']
oldmap = load(root / 'docs/evidence/teemo-counted-current-cn16-2026-10-21/archive-map.json')
oldarchive = root / oldmap['archive']['path']; assert sha(oldarchive) == oldmap['archive']['sha256']
with tarfile.open(oldarchive, 'r:*') as tar:
    for ref in mapping['existingIdenticalOlderArchiveMembersNotDuplicated']:
        raw = tar.extractfile(ref['path']).read(); assert hashlib.sha256(raw).hexdigest() == ref['sha256']
assert all(not path.endswith('/session-a.json') and not path.endswith('/session-b.json') and not path.endswith('/session-native-a.json') and not path.endswith('/session-native-b.json') for path in [m['path'] for m in mapping['members']])
assert all(sha(root / p) == h for p, h in f['sourceInputHashes'].items())
proof = {'atUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'status': 'PASS_INDEPENDENT_CONTROLLER_PUBLICATION_ALL_OLD_IDS_SOURCES_SCOPES_AND_ARCHIVE_CLOSED_SET_RETAINED', 'originalReviewsExact': 157, 'newReviewCount': 158, 'originalWholeContextsExact': 35, 'currentInputContextsRefreshedWithCompleteOldObjects': 37, 'currentContexts': 73, 'all245GateObjectsAndSevenSourceDeltasWholeExact': True, 'historicalAnnieCounterexampleActual1NotRelabeledCurrent2FollowupSeparate': True, 'originalObligationIdsRetained': 16992, 'newBranches': 50, 'currentObligations': 17042, 'allSourceUniverseCountsDatesAndScopesExact': True, 'archiveMembers': len(mapping['members']), 'archiveBytes': archive.stat().st_size, 'allArchiveAndPriorReferencedMemberBytesHashesExact': True, 'privateSessionsExcluded': True, 'governanceValid': True, 'semanticCoverageCertified': False, 'currentFixedFullVisiblePhysicalWindowsAndGoalCompletion': 'OPEN'}
result = w / 'independent-current-controller-publication-verification-v1.json'
assert not result.exists()
result.write_text(json.dumps(proof, indent=2))
print('PASS publication158/73/245/7;all157oldreviews35wholecontexts37currentpins;16992+50=17042;archive', len(mapping['members']), archive.stat().st_size)
