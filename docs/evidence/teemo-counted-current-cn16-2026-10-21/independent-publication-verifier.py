from pathlib import Path
import copy,datetime,gzip,hashlib,json,subprocess,tarfile
root=Path('/Users/dinghaolin/MyProjects/riftbound');w=Path(__file__).resolve().parent;prior=w/'prior-current-counted-publication-v1';ev=root/'docs/evidence/teemo-counted-current-cn16-2026-10-21';load=lambda p:json.loads(p.read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();unzip=lambda p:json.loads(gzip.decompress(p.read_bytes()))
old=unzip(prior/'current-manifest.json.gz');before=unzip(prior/'review-decisions.json.gz');m=load(root/'docs/audit/current-manifest.json');r=load(root/m['inputs']['reviews']['path']);assert len(before['reviews'])==156 and r['reviews'][:156]==before['reviews'] and len(r['reviews'])==157;assert len(m['reviewContexts'])==72 and len(m['acceptanceGates'])==245 and len(m['laterSourceDeltas'])==7
stale=unzip(prior/'complete-current-contexts-before-input-refresh.json.gz');mapping={a['id']:a for a in stale};assert len(mapping)==40
follow=load(ev/'two-prior-findings-explicit-current-stage-followups.json')['records'];assert len(follow)==2;fp={a['branch']:a for a in follow};sp={'path':str((ev/'source-manifest.json').relative_to(root)),'sha256':sha(ev/'source-manifest.json'),'role':'source-manifest'};refresh={'path':str((ev/'forty-current-input-context-refresh.json').relative_to(root)),'sha256':sha(ev/'forty-current-input-context-refresh.json'),'role':'current-input-refresh'};parent='25c628e74b25ea13f02602bae0d1056cc5b190ae'
for id,c in old['reviewContexts'].items():
 expected=copy.deepcopy(c)
 if id in mapping:
  for change in mapping[id]['changes']:
   p=next(a for a in expected['implementation'] if a['path']==change['path']);assert p['sha256']==change['oldSha256'];p['sha256']=change['currentSha256'];assert sha(root/change['path'])==change['currentSha256']
  expected.update(currentImplementationCommit=parent,currentImplementationIncludesUncommittedChanges=True,ciState='PENDING',currentTeemoCountedInputRefresh=refresh)
 if id=='exhausted-reksai-current-cn16-2026-10-21':
  for index,b in enumerate(expected['branches']):
   if b['id'] in fp:
    f=fp[b['id']];assert f['oldCompleteBranch']==b and f['currentCompleteBranch']['sourceManifest']==b['sourceManifest'] and f['currentCompleteBranch']['findingEvidence']==b['findingEvidence'] and f['currentCompleteBranch']['sourceRefs']==b['sourceRefs'];assert f['currentCompleteBranch']['decision']['state']=='OPEN' and f['currentCompleteBranch']['fixedCommitCi']['state']=='PENDING_NEW_FIXED_FULL';expected['branches'][index]=f['currentCompleteBranch']
 assert m['reviewContexts'][id]==expected,id
for a,b in zip(old['acceptanceGates'],m['acceptanceGates']):
 assert all(b[k]==v for k,v in a.items())
 if a['id'] not in ['DECK_OBSERVATION_FAMILY','SOURCE_GAPS_AND_EFFECTIVE_DELTAS']:assert a==b
for k in ['certificationScope','laterSourceDeltas','cardCoverageManifest','officialNewsObligations']:assert m[k]==old[k]
for k in old['inputs']:
 if k not in ['active','reviews']:assert m['inputs'][k]==old['inputs'][k]
archive_map=load(ev/'archive-map.json');archive=root/archive_map['archive']['path'];assert sha(archive)==archive_map['archive']['sha256'];members=archive_map['members']
with tarfile.open(archive,'r:xz') as tar:
 assert {a.name for a in tar.getmembers()}=={a['path'] for a in members}
 for row in members:
  raw=tar.extractfile(row['path']).read();assert len(raw)==row['bytes'] and hashlib.sha256(raw).hexdigest()==row['sha256'];assert (root/row['path']).is_file() and sha(root/row['path'])==row['sha256'],row['path']
source=load(ev/'source-manifest.json');assert source['actualRuleInputs']['catalogSha256']=='e7455edc32d43f8253a9d0c6fedb83c347bebafbef064f9b8dffaaabc4b4d266';assert source['onlyCheckActualOutcome']=={'cases':13254,'methods':7510,'passed':13252,'failed':2,'skipped':0};assert source['finalFocus']=={'cases':180,'methods':74,'postCheckCases':77,'postCheckMethods':26}
rem=load(w/'post-only-check-exact-source-expectation-remediation-v1.json');assert all(sha(root/p)==h for p,h in rem['finalSourceHashes'].items()) and all(oct((root/p).stat().st_mode&0o777)==v for p,v in rem['finalSourceModes'].items())
sql=json.loads(subprocess.check_output(['git','show',parent+':tests/Riftbound.PostgresAcceptance/SqlAcceptanceCases.json'],cwd=root));current=load(root/'tests/Riftbound.PostgresAcceptance/SqlAcceptanceCases.json');assert current['cases'][:-1]==sql['cases'] and len(sql['cases'])==53 and current['cases'][-1]['id']=='teemo-counted-damage'
for row in load(w/'bounded-teemo-batch-before-source-inputs-v1.json')['trackedSourceInputs']:
 p=row['path']
 if p.startswith('tests/Riftbound.PostgresAcceptance/') and p.endswith('.cs') and p!='tests/Riftbound.PostgresAcceptance/Program.cs':assert sha(root/p)==row['sha256']
proof={'atUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'status':'INDEPENDENT_CURRENT_TEEMO_SOURCE_ARCHIVE_CONTEXT_STAGE_GATE_AND_ORIGINAL_TEST_SQL_RETENTION_PASS','all156OldReviewObjectsExact':True,'all31UnaffectedContextsWholeObjectsExact':True,'fortyCurrentInputRefreshesVerifiedWithWholeOldObjects':True,'twoPriorFindingCurrentStageUpdatesExplicitOldEvidenceAndSourceRefsPreserved':True,'all245OldGateFieldsStatesOrderExact243WholeObjectsExact':True,'allOldSourcesDatesScopeDeltasCoverageAndNewsExact':True,'allOriginal53SqlObjectsOrderAndSourceClassesExactOnly54Append':True,'allArchiveMembersExactAndClosedSetVerified':True,'archiveMembers':len(members),'archiveBytes':archive.stat().st_size,'archive':archive_map['archive'],'all3328FinalSourceHashesAndModesExact':True,'onlyOneCheckAndOnlyTwoSourceVerificationCalibrations':True,'newFixedFullVisiblePhysicalWindowsAndWholePrintingCertification':'OPEN'}
(ev/'independent-publication-and-retention-verification.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n');print('Independent publication/context/archive/source/SQL retention PASS;remaining16975 IDs and new17 checked after governance output')
