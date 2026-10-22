from pathlib import Path
import copy,datetime,gzip,hashlib,json,tarfile
root=Path('/Users/dinghaolin/MyProjects/riftbound');w=Path(__file__).resolve().parent;ev=root/'docs/evidence/main-initial-snapshot-reconnect-2026-10-22'
load=lambda p:json.loads(p.read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
old=json.loads(gzip.decompress((w/'prior-main-snapshot-publication-v1/current-manifest.json.gz').read_bytes()));current=load(root/'docs/audit/current-manifest.json');prep=load(w/'client-snapshot-product-gate-publication-preparation-v1.json');changed={x['contextId'] for x in prep['changes']};assert len(changed)==4
assert len(old['reviewContexts'])==len(current['reviewContexts'])==80 and set(old['reviewContexts'])==set(current['reviewContexts'])
assert current['acceptanceGates'][:250]==old['acceptanceGates'] and len(current['acceptanceGates'])==251
assert current['laterSourceDeltas']==old['laterSourceDeltas'] and len(current['laterSourceDeltas'])==7 and current['certificationScope']==old['certificationScope'] and current['inputs']==old['inputs']
assert sha(root/current['inputs']['reviews']['path'])==old['inputs']['reviews']['sha256'] and len(load(root/current['inputs']['reviews']['path'])['reviews'])==165
source=load(ev/'source-manifest.json');sp={'path':str((ev/'source-manifest.json').relative_to(root)),'sha256':sha(ev/'source-manifest.json'),'role':'source-manifest'}
for identity,before in old['reviewContexts'].items():
 after=current['reviewContexts'][identity];expected=copy.deepcopy(before)
 if identity in changed:
  for ref in expected['implementation']:
   if ref['path']=='clients/godot/scripts/Main.cs':ref['sha256']=sha(root/ref['path'])
  expected.update(currentImplementationCommit='267ab4f0690436d57cd45277e44c8bb5ee5ef6f0',currentImplementationIncludesUncommittedChanges=True,ciState='PENDING',currentInitialSnapshotInputRefresh={'sourceManifest':sp,'onlyCurrentImplementationPinsAndExplicitPendingStateRefreshed':True,'allOriginalSourceEvidenceBranchesAndVerificationRefsRetained':True,'newFixedFullAndCurrentProducts':'OPEN','noAdditionalPriorCardOrRuleCertification':True})
 assert expected==after
 assert ('verificationSources' in before)==('verificationSources' in after) and before.get('verificationSources')==after.get('verificationSources')
assert sum(before==current['reviewContexts'][identity] for identity,before in old['reviewContexts'].items())==76
new_gate=current['acceptanceGates'][-1];assert new_gate['id']=='NATIVE_INITIAL_ROOM_SNAPSHOT_RECONNECT_RACE' and new_gate['state']=='OPEN' and len(new_gate['behaviorCases'])==13 and not new_gate['wholePrintingCertified']
check=load(w/'final-only-check-original-retention-v1.json');assert {b['actualExecutedCase'] for b in new_gate['behaviorCases']}==set(check['newCaseNames'])
for ref in new_gate['implementation']+new_gate['verificationSources']:assert sha(root/ref['path'])==ref['sha256']
oldgov=json.loads(gzip.decompress((root/'docs/evidence/lab-start-you-current-cn16-2026-10-22/final-governance.json.gz').read_bytes()));gov=load(ev/'final-governance.json')
assert gov['governanceValid'] and gov['errors']==[] and not gov['semanticCoverageCertified'] and gov['currentReviewCount']==165
oldids={r['id'] for r in oldgov['remainingObligations']};newids={r['id'] for r in gov['remainingObligations']};assert len(oldids)==17181 and len(newids)==17182 and oldids<=newids and newids-oldids=={'GATE:NATIVE_INITIAL_ROOM_SNAPSHOT_RECONNECT_RACE'}
assert all(gov['counts'][k]==oldgov['counts'][k] for k in ['combinedExactPrintingNumbers','sourceBlocks','cnOfficialPrintings','enReturnedPrintings','enReportedTotal','enSourceCountGap','activeCatalogEntries','outsideActiveCatalog'])
archive_map=load(ev/'archive-map.json');archive=root/archive_map['archive']['path'];assert sha(archive)==archive_map['archive']['sha256']
with tarfile.open(archive,'r:gz') as tar:
 assert set(tar.getnames())=={r['archiveMember'] for r in archive_map['originalPathsWithExactCanonicalMember']}
 for row in archive_map['originalPathsWithExactCanonicalMember']:
  raw=tar.extractfile(row['archiveMember']).read();assert len(raw)==row['bytes'] and hashlib.sha256(raw).hexdigest()==row['sha256']
cleanup=json.loads(gzip.decompress((w/'completed-old-fixed-worktree-retirement-result-v1.json.gz').read_bytes()));assert len(cleanup['removed'])==10 and cleanup['all3344CurrentCheckSourceHashesStable'] and all(not Path(r['path']).exists() for r in cleanup['removed'])
proof={'atUtc':now(),'status':'PUBLISHED_CURRENT_MAIN_SNAPSHOT_TRANSPORT_GOVERNANCE_VALID_ALL_17181_ORIGINAL_IDS_RETAINED_PLUS_ONE_PRODUCT_GATE','governanceValid':True,'semanticCoverageCertified':False,'reviews':165,'contexts':80,'gates':251,'sourceDeltas':7,'remainingObligations':17182,'allOriginal165ReviewsBytes250GateObjectsSevenDeltasCertificationScopeAndInputsExact':True,'fourCurrentMainImplementationPinsExplicitlyRefreshed76WholeContextsExact':True,'allOldOptionalVerificationSourcePresenceAndValuesExact':True,'newTransportGate13ActualCaseIdentitiesNotCardOrClauseReview':True,'allOriginalTestMethodsTheoryRows143Full151Cases110Pairs11Decks424242AssertionsRetained':True,'onlyCheckCases':13449,'onlyCheckMethods':7550,'newCases':13,'newMethods':7,'all3344Sources1419Conformance23NativeDebugStableDuringCheck':True,'allArchiveRowsAndMemberHashesIndependentlyVerified':True,'toolTests':{'actualTests':74,'actualTerminalExitCode':0,'allPassed':True,'gameplayInputChangesAfterOnlyCheck':False},'tenClosedOldWorktreesOriginalGitSourceAndOriginalExecutionRecordsPreservedBeforeRetirement':True,'oldRuntimeDirectoriesNowRetiredNotClaimedStillDirectlyExecutable':True,'latestNativePairAndOneInstallerPerOsOriginalDbAndAllSourceUniverseRetained':True,'newFixedFullCiDefaultMainColdRepairVisibleAndWindows':'OPEN','wholePrintingOrGoalComplete':False}
output=ev/'independent-final-publication-and-retention.json';assert not output.exists();output.write_text(json.dumps(proof,ensure_ascii=False,indent=2))
raw_path=ev/'final-governance.json';raw=raw_path.read_bytes();compressed=ev/'final-governance.json.gz';assert not compressed.exists();compressed.write_bytes(gzip.compress(raw,mtime=0));assert gzip.decompress(compressed.read_bytes())==raw
retirement={'atUtc':now(),'originalRawBytes':len(raw),'originalRawSha256':hashlib.sha256(raw).hexdigest(),'canonicalCompressedPath':str(compressed.relative_to(root)),'canonicalCompressedBytes':compressed.stat().st_size,'canonicalCompressedSha256':sha(compressed),'fullContentByteEqualityVerifiedBeforeRawDuplicateRetirement':True,'all17182OriginalObligationObjectsRemainInCanonicalGzip':True};raw_path.unlink();(w/'final-governance-exact-large-raw-duplicate-retirement-v1.json').write_text(json.dumps(retirement,indent=2));print('PASS current165reviews80contexts251gates7deltas17182IDs; all17181original+oneproductgate;4explicitMainpins76wholecontexts;13actualcaseIDs;74tools;allarchivebytes verified;large rawgov exactgzip retained;newfixedproductsOPEN',flush=True)
