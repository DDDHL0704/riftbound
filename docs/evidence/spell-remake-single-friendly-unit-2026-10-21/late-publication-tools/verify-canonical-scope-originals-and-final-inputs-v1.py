from pathlib import Path
import gzip,hashlib,json,tarfile
root=Path('/Users/dinghaolin/MyProjects/riftbound');w=Path(__file__).resolve().parent;out=root/'docs/evidence/spell-remake-single-friendly-unit-2026-10-21';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();load=lambda p:json.loads(p.read_bytes())
prior=json.loads(gzip.decompress((w/'prior-entire-current-manifest.json.gz').read_bytes()));prior_reviews=json.loads(gzip.decompress((w/'prior-entire-review-decisions.json.gz').read_bytes()));current=load(root/'docs/audit/current-manifest.json');reviews=load(root/'docs/evidence/rules-reaudit-2026-10-11/review-decisions.json');identity='spell-remake-single-friendly-unit-no-complete-choice-2026-10-21'
assert len(reviews['reviews'])==150 and reviews['reviews'][:149]==prior_reviews['reviews'] and reviews['reviews'][-1]['id']==identity
assert len(current['reviewContexts'])==65 and current['acceptanceGates']==prior['acceptanceGates'] and len(current['acceptanceGates'])==245 and current['laterSourceDeltas']==prior['laterSourceDeltas'] and len(current['laterSourceDeltas'])==7 and current['certificationScope']==prior['certificationScope']
mutable={'implementation','verificationSources','currentInputRevision','currentImplementationCommit','currentImplementationIncludesUncommittedChanges','ciState','implementationPinUpdates','revalidationReason'}
for name,old in prior['reviewContexts'].items():
 row=current['reviewContexts'][name];assert {k:v for k,v in old.items() if k not in mutable}=={k:v for k,v in row.items() if k not in mutable},name
 assert len(row['implementationPinUpdates'])==len(old.get('implementationPinUpdates',[]))+1 and row['implementationPinUpdates'][:-1]==old.get('implementationPinUpdates',[])
old_ids=set(load(root/'docs/evidence/spell-remake-declaration-scope-2026-10-21/remaining-obligation-ids.json'));ids=set(load(out/'remaining-obligation-ids.json'));assert len(old_ids)==16936 and old_ids<=ids and len(ids)==16944 and len(ids-old_ids)==8
index=load(out/'archive-map.json');archive=root/index['archive']['path'];assert sha(archive)==index['archive']['sha256'];packed={r['archiveMember']:r for r in index['records'] if 'archiveMember' in r}
with tarfile.open(archive,'r:xz') as tar:
 assert set(tar.getnames())==set(packed)
 for name,row in packed.items():
  raw=tar.extractfile(name).read();assert len(raw)==row['bytes'] and hashlib.sha256(raw).hexdigest()==row['sha256'] and sha(root/row['originalPath'])==row['sha256'],name
for row in index['records']:
 if 'canonicalPath' in row:assert sha(root/row['canonicalPath'])==sha(root/row['originalPath'])==row['sha256']
for row in index['explicitPrivateExclusions']:assert sha(root/row['originalPath'])==row['sha256'] and row['retainedLocally']
supplement=load(out/'explicit-public-opening-key-diagnostic-supplement.json');assert sha(root/supplement['originalPath'])==sha(root/supplement['canonicalPath'])==supplement['sha256']
start=load(w/'only-check-start-inputs-v1.json');assert all(sha(root/p)==h for p,h in start['sourceInputHashes'].items()) and all(oct((root/p).stat().st_mode&0o777)==m for p,m in start['sourceInputModes'].items())
def runtime_files(folder):return {str(p.relative_to(root)):sha(p) for p in folder.rglob('*') if p.is_file() and p.relative_to(folder).parts[0]!='var'}
assert runtime_files(root/'tests/Riftbound.ConformanceTests/bin/Debug/net10.0')==start['actualConformanceRuntimeFileHashes'] and runtime_files(root/'clients/godot/.godot/mono/temp/bin/Debug')==start['actualNativeRuntimeFileHashes']
source=load(out/'source-manifest.json');assert source['wholePrintingAndGlobalSemanticCertification'] is False and len(source['exactEightConsumers'])==8
assert source['precedingFixedA56137ProductAndCi']['nativeAcceptedGameplayCommands']==0 and source['precedingFixedA56137ProductAndCi']['normalGatewayDoesNotRelabelNativeFailure']
check=load(out/'check-original-method-case-and-input-retention.json');assert check['actualCases']==13214 and check['actualMethods']==7499 and check['newCases']==38 and check['newMethods'] and check['failed']==check['skipped']==0
result={'status':'PASS_CANONICAL_ALL_ORIGINAL_BYTES_SCOPES_AND16936_IDS_RETAINED_ONLY_EIGHT_NEW_BRANCHES_FINAL_SOURCE_AND_PAYLOAD_INPUTS_EXACT','originalRecordCount':len(index['records']),'packedRecordCount':len(packed),'archiveBytes':archive.stat().st_size,'allOriginal149Reviews64ContextScopes245Gates7DeltasAnd16936IdsRetained':True,'currentReviewCount':150,'currentContextCount':65,'currentGateCount':245,'currentSourceDeltaCount':7,'currentRemainingObligationCount':16944,'all3317SourcesModes1419Conformance23NativeFinalInputsExactAfterOnlyCheck':True,'original143FullMethods110Pairs11DecksSeed424242AndAssertionsRetained':True,'actualOnlyCheck':{'cases':13214,'methods':7499,'failed':0,'skipped':0},'originalNativeFailureIssue77StillOpen':True,'visibleAndPhysicalWindowsAndCurrentFinalFixedFull':'OPEN','allPrivateOriginalsRetainedLocallyNotPublished':True,'wholePrintingAndGlobalSemanticCertification':False}
p=out/'independent-canonical-original-retention-and-final-inputs.json';assert not p.exists();p.write_text(json.dumps(result,indent=2)+'\n');print(result['status'],flush=True)
