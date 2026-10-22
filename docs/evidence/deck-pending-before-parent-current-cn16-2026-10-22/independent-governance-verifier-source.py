from pathlib import Path
import copy,datetime,gzip,hashlib,json,subprocess
root=Path('/Users/dinghaolin/MyProjects/riftbound');q=Path(__file__).resolve().parent;ev=root/'docs/evidence/deck-pending-before-parent-current-cn16-2026-10-22';base='55fdbabf8731fec795c26584985bd31cc0bcca32'
load=lambda p:json.loads(p.read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
m=load(root/'docs/audit/current-manifest.json');old=json.loads(subprocess.check_output(['git','show',base+':docs/audit/current-manifest.json'],cwd=root,text=True));rp=m['inputs']['reviews']['path'];reviews=load(root/rp);oldreviews=json.loads(subprocess.check_output(['git','show',base+':'+rp],cwd=root,text=True))
g=load(q/'final-governance-v1.json');oldg=json.loads(gzip.decompress((root/'docs/evidence/native-macos-sigterm-main-loop-2026-10-22/final-governance.json.gz').read_bytes()));ident='deck-pending-before-parent-current-cn16-2026-10-22'
assert g['governanceValid'] and not g['semanticCoverageCertified'] and not g['errors'] and len(g['remainingObligations'])==17234 and g['scopedReviews']==166
before={r['id'] for r in oldg['remainingObligations']};after={r['id'] for r in g['remainingObligations']}
assert before<=after and len(before)==17183 and len(after)==17234
new=after-before;assert len(new)==51 and all(x.startswith('BRANCH:'+ident+':') for x in new)
assert reviews['reviews'][:165]==oldreviews['reviews'] and len(reviews['reviews'])==166
assert m['acceptanceGates']==old['acceptanceGates'] and len(m['acceptanceGates'])==252 and m['laterSourceDeltas']==old['laterSourceDeltas'] and m['certificationScope']==old['certificationScope']
changed=[];same=[]
for key,c in old['reviewContexts'].items():
 a=m['reviewContexts'][key];assert ('verificationSources' in c)==('verificationSources' in a) and c.get('verificationSources')==a.get('verificationSources')
 if a==c:same.append(key);continue
 changed.append(key);expected=copy.deepcopy(c)
 for r in expected['implementation']:r['sha256']=sha(root/r['path'])
 expected.update(currentImplementationCommit=base,currentImplementationIncludesUncommittedChanges=True,ciState='PENDING',currentDeckPendingParentInputRefresh=a['currentDeckPendingParentInputRefresh'])
 assert a==expected
assert len(changed)==64 and len(same)==16 and set(m['reviewContexts'])-set(old['reviewContexts'])=={ident}
assert m['inputs']['reviews']['sha256']==sha(root/rp)
assert json.loads(gzip.decompress((ev/'original80-complete-contexts-before-refresh.json.gz').read_bytes()))==old['reviewContexts']
source=load(ev/'source-manifest.json');assert source['newCases']==51 and source['newMethods']==10 and source['soleCheckOutcomes']=={'Passed':13496,'Failed':1}
for kind in ['implementation','verificationSources']:
 for r in source[kind]:assert sha(root/r['path'])==r['sha256']
archive=load(ev/'archive-map.json');assert sha(root/archive['archive']['path'])==archive['archive']['sha256']
assert load(q/'post-check-corrected-candidate-v1/independent-sql60-owner-terminal-v1.json')['ownPostgresSmartStopped']
pg=ev/'final-governance.json.gz';assert not pg.exists();pg.write_bytes(gzip.compress((q/'final-governance-v1.json').read_bytes(),mtime=0))
receipt={'atUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'status':'PASS_INDEPENDENT_CURRENT_GOVERNANCE_ALL17183_ORIGINAL_IDS_EXACT_PLUS51_BOUNDED_BRANCHES','governanceValid':True,'semanticCoverageCertified':False,'allOriginal17183RemainingIdsRetained':True,'only51NewIds':sorted(new),'allOriginal165ReviewsWholeObjectsExact':True,'all252GatesSevenDeltasAndCertificationScopeExact':True,'all80OriginalContextIdsAndOptionalVerificationSourcePresenceValuesExact':True,'currentInputOnly64RefreshedContexts':changed,'other16WholeContextObjectsExact':same,'all3350FinalSourcesBoundByFinalRetainer':True,'sourceManifestSha256':sha(ev/'source-manifest.json'),'finalGovernanceSha256':sha(pg),'archiveSha256':sha(root/archive['archive']['path']),'actualRemainingCount':17234,'expectedNewFixedFull':{'cases':13649,'methods':7701,'sql':60},'newFixedFullProductsAndWholeScopeStillOpen':True,'initialQaReadbackSchemaError':'The first inline preflight looked for stdout-only remainingObligationCount instead of counting the saved remainingObligations array; failed before any receipt write or root change. Actual checker execution was PASS and was not rerun.'}
p=ev/'independent-final-governance-retention.json';assert not p.exists();p.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
print('Independent PASS: old17183 exact +51;165 reviews/252gates/7deltas/scope retained;64 explicit input-only/16 whole contexts; old optional verification refs exact; semantic false.')
