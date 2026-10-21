from pathlib import Path
import collections, datetime, gzip, hashlib, json, re, tarfile

root=Path('/Users/dinghaolin/MyProjects/riftbound');w=Path(__file__).resolve().parent
ev=root/'docs/evidence/special-trigger-damage-current-cn16-2026-10-22'
load=lambda p:json.loads(p.read_text())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
pin=lambda p:{'path':str(p.relative_to(root)),'sha256':sha(p)}
prior=w/'prior-special-trigger-publication-v1'
old=json.loads(gzip.decompress((prior/'prior-governance-before-coherent-publication-v1.json.gz').read_bytes()))
new=load(w/'postpublication-governance-v2.json')
assert new['governanceValid'] is True and new['semanticCoverageCertified'] is False and new['errors']==[]
old_ids=[x['id'] for x in old['remainingObligations']];new_ids=[x['id'] for x in new['remainingObligations']]
assert len(old_ids)==17042 and len(new_ids)==17088 and len(set(old_ids))==17042 and len(set(new_ids))==17088
assert set(old_ids)<=set(new_ids) and len(set(new_ids)-set(old_ids))==46
assert new['counts']==old['counts']
m=load(root/'docs/audit/current-manifest.json');om=json.loads(gzip.decompress((prior/'current-manifest.json.gz').read_bytes()))
reviews=load(root/m['inputs']['reviews']['path']);oldreviews=json.loads(gzip.decompress((prior/'review-decisions.json.gz').read_bytes()))
assert len(reviews['reviews'])==161 and reviews['reviews'][:158]==oldreviews['reviews']
assert len(m['reviewContexts'])==76 and all(m['reviewContexts'][k]==v for k,v in om['reviewContexts'].items())
assert m['acceptanceGates']==om['acceptanceGates'] and m['laterSourceDeltas']==om['laterSourceDeltas']
assert len(m['acceptanceGates'])==245 and len(m['laterSourceDeltas'])==7
for k in om:
    if k not in {'inputs','reviewContexts'}:assert m[k]==om[k],k
assert {k:v for k,v in m['inputs'].items() if k!='reviews'}=={k:v for k,v in om['inputs'].items() if k!='reviews'}
assert sha(root/m['inputs']['reviews']['path'])==m['inputs']['reviews']['sha256']
mapping=load(ev/'archive-map.json');archive=root/mapping['archive']['path'];assert sha(archive)==mapping['archive']['sha256']
with tarfile.open(archive,'r:gz') as tar:
    assert set(tar.getnames())=={v['path'] for v in mapping['members']}
    for row in mapping['members']:
        raw=tar.extractfile(row['path']).read();assert len(raw)==row['bytes'] and hashlib.sha256(raw).hexdigest()==row['sha256']
        assert (root/row['path']).read_bytes()==raw,row['path']
oldmap_path=root/'docs/evidence/controller-damage-current-cn16-2026-10-21/archive-map.json';oldmap=load(oldmap_path)
with tarfile.open(root/oldmap['archive']['path'],'r:gz') as tar:
    for row in mapping['existingIdenticalOlderArchiveMembersNotDuplicated']:
        assert row['existingArchiveMap']['path']==str(oldmap_path.relative_to(root)) and row['existingArchiveMap']['sha256']==sha(oldmap_path)
        raw=tar.extractfile(row['existingArchiveMember']).read();assert hashlib.sha256(raw).hexdigest()==row['sha256'] and raw==(root/row['path']).read_bytes()
newcontexts={k:v for k,v in m['reviewContexts'].items() if k not in om['reviewContexts']}
assert sorted(len(v['branches']) for v in newcontexts.values())==[1,1,44]
for key,c in newcontexts.items():
    for ref in [c['sourceManifest'],*c['implementation'],*c['verificationSources'],*c['evidence']]:assert sha(root/ref['path'])==ref['sha256']
    assert c['ciState']=='PENDING' and c['ciReceipts']==[]
    assert all(b['decision']['state']=='OPEN' and b['wholePrintingCertified'] is False for b in c['branches'])
cases=load(w/'all-forty-four-current-special-trigger-actual-new-case-identities-v1.json')['cases']
branches=load(ev/'forty-four-exact-behavior-branches.json')['branches']
assert {b['boundedCase']['actualExecutedCase'] for b in branches}==set(cases)
assert sum(b['boundedCase']['controlledSqlSamePrintingCountAndLocationBranch'] for b in branches)==16
check=load(ev/'check-original-case-method-and-input-retention.json')
assert (check['actualCases'],check['actualMethods'],check['newCases'],check['newMethods'])==(13348,7521,44,5)
assert check['oldCasesMultisetPreserved'] and check['oldMethodsPreserved'] and check['allFrozenAndExecutedInputsUnchanged']
native=load(ev/'preceding-fixed7a-visible41-seating-replay-counterexample.json')
assert native['status']=='CONFIRMED_NATIVE_FIRST_DECK_HISTORICAL_SEAT_REPLAY_COUNTEREXAMPLE'
assert native['commands']==41 and native['events']==263 and native['finalOnlyRecoveryValidationErrors']==[] and native['allHistoricalReplayOrWholeProductAcceptanceClosed'] is False
assert len(native['exactOriginalStoredFirstStateSeatMap'])==1 and len(native['actualFirstReplayedStateSeatMap'])==2
for filename,number in [('frost-controller-issue84.json',84),('native-history-issue85.json',85)]:
    issue=load(ev/filename);assert issue['actualPostedIssue']['number']==number and issue['exactBodyReadbackIncludingNewline'] is True
    assert {v['name'] for v in issue['actualPostedIssue']['labels']}=={'bug','ready-for-agent'}
log=w/'postpublication-verification-tool-tests-v1.log';text=log.read_text();assert re.search(r'Ran 74 tests',text) and text.rstrip().endswith('OK')
for source,name in [(w/'postpublication-governance-v2.json','final-governance.json.gz'),(log,'verification-tool-tests.log.gz')]:
    raw=source.read_bytes();target=ev/name;assert not target.exists();target.write_bytes(gzip.compress(raw,mtime=0));assert gzip.decompress(target.read_bytes())==raw
proof={'atUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'status':'INDEPENDENT_CANONICAL_SPECIAL_TRIGGER_PUBLICATION_AND_ORIGINAL_RETENTION_PASS',
    'original17042ObligationIdentitiesRetained':True,'actualObligations':17088,'addedBoundedBranches':44,'addedConfirmedOpenFindings':2,
    'allOriginal158Reviews73Contexts245GatesSevenDeltasCompleteObjectsExact':True,'allOriginalCoverageCountsDatesSourcesAndGapsUnchanged':True,
    'actualReviews':161,'actualContexts':76,'semanticCoverageCertified':False,'governanceErrors':[],
    'canonicalArchiveClosedSetMembers':len(mapping['members']),'existingIdenticalMembersReused':len(mapping['existingIdenticalOlderArchiveMembersNotDuplicated']),
    'allArchivedAndReusedOriginalBytesIndependentlyVerified':True,'allCurrentEvidenceSourceAndImplementationPinsVerified':True,
    'fortyFourActualCasesSixteenExactSqlCasesBoundIndependently':True,'onlyCheck13348Methods7521OriginalMultisetAndSourcesRetained':True,
    'fixed7aVisible41NormalScoreDoesNotCloseConfirmedHistoryFailure':True,'frostController84AndNativeHistory85RemainOpen':True,
    'verificationToolTests':74,'newFixedFullCurrentNativeAndWindows':'OPEN','finalGovernance':pin(ev/'final-governance.json.gz'),
    'sourceManifest':pin(ev/'source-manifest.json'),'noSecondCheck':True,'goalComplete':False}
with (ev/'independent-publication-and-retention-verification.json').open('x') as s:json.dump(proof,s,ensure_ascii=False,indent=2)
print('PASS canonical archive/inputs/46 branches; old17042 retained ->17088;161/76; old158/73/245/7 objects exact;74 tools; semantic false')
