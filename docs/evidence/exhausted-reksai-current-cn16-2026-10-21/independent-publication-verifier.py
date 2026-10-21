from pathlib import Path
import datetime,gzip,hashlib,json,re,subprocess,tarfile
root=Path('/Users/dinghaolin/MyProjects/riftbound');w=Path(__file__).resolve().parent
ev=root/'docs/evidence/exhausted-reksai-current-cn16-2026-10-21'
load=lambda p:json.loads(p.read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def pin(p):return {'path':str(p.relative_to(root)),'sha256':sha(p)}
prior=w/'prior-coherent-publication-governance-v1'
old=json.loads(gzip.decompress((prior/'current-manifest.json.gz').read_bytes()))
new=load(root/'docs/audit/current-manifest.json')
before=json.loads(gzip.decompress((prior/'review-decisions.json.gz').read_bytes()))['reviews']
after=load(root/new['inputs']['reviews']['path'])['reviews']
assert len(before)==155 and len(after)==156 and after[:155]==before
assert len(new['reviewContexts'])==71 and all(new['reviewContexts'][k]==v for k,v in old['reviewContexts'].items())
for a,b in zip(old['acceptanceGates'],new['acceptanceGates']):
    assert a['id']==b['id'] and all(b[k]==v for k,v in a.items())
    expected={'currentExhaustedReksaiFourIntegratedSqlBranches'} if a['id']=='DECK_OBSERVATION_FAMILY' else {'currentSupportingTeemoTwoSeparateOpenSourceDamageFindings'} if a['id']=='SOURCE_GAPS_AND_EFFECTIVE_DELTAS' else set()
    assert set(b)-set(a)==expected
for k in ['certificationScope','laterSourceDeltas','cardCoverageManifest','officialNewsObligations']:assert new[k]==old[k]
old_status=gzip.decompress((prior/'PROJECT_STATUS.md.gz').read_bytes()).decode()
assert (root/'docs/PROJECT_STATUS.md').read_text().endswith(old_status[len('# 当前项目状态\n\n'):])
gp=w/'postpublication-current-governance-v1.json';governance=load(gp)
assert governance['governanceValid'] and governance['semanticCoverageCertified'] is False and not governance['errors']
assert governance['currentReviewCount']==156 and len(governance['remainingObligations'])==16975
old_ids=set(json.loads(gzip.decompress((prior/'remaining-obligation-ids.json.gz').read_bytes())))
ids=[o['id'] for o in governance['remainingObligations']];assert len(ids)==len(set(ids)) and len(old_ids)==16969 and old_ids<set(ids)
added=set(ids)-old_ids;assert len(added)==6 and all(x.startswith('BRANCH:exhausted-reksai-current-cn16-2026-10-21:') for x in added)
context=new['reviewContexts']['exhausted-reksai-current-cn16-2026-10-21']
assert len(context['branches'])==6 and sum(b['state']=='OPEN' for b in context['branches'])==2
for role in ['implementation','verificationSources']:
    assert all(sha(root/r['path'])==r['sha256'] for r in context[role])
mapping=load(ev/'archive-map.json');archive=root/mapping['archive']['path']
assert sha(archive)==mapping['archive']['sha256'] and len(mapping['members'])==332
with tarfile.open(archive,'r:xz') as tar:
    assert {p.name for p in tar.getmembers()}=={r['path'] for r in mapping['members']}
    for row in mapping['members']:
        raw=tar.extractfile(row['path']).read();assert len(raw)==row['bytes'] and hashlib.sha256(raw).hexdigest()==row['sha256']
        plain=gzip.decompress(raw) if row['path'].endswith('.gz') else raw
        assert not re.search(rb'"(?:playerKey|reconnectToken|sessionToken|password|accessToken)"\s*:\s*"[^"\s]+"',plain,re.I)
source=load(w/'current-integrated-exhausted-sql-frozen-inputs-v1.json')
assert len(source['sourceInputHashes'])==3326 and all(sha(root/p)==h for p,h in source['sourceInputHashes'].items())
retention=load(w/'current-root-integration-exact-source-transfer-v1.json')
assert all(sha(root/p)==h for p,h in retention['allOriginalSqlSourceExceptDeclaredProgramEntryUnchanged'].items())
name='tests/Riftbound.PostgresAcceptance/SqlAcceptanceCases.json'
old_cases=json.loads(subprocess.check_output(['git','show','HEAD:'+name]))['cases'];new_cases=load(root/name)['cases']
assert len(old_cases)==52 and len(new_cases)==53 and new_cases[:52]==old_cases and new_cases[-1]['id']=='exhausted-legend-confirmation'
python_log=w/'postpublication-all-original-python-tests-v1.log'
assert re.search(r'Ran 74 tests in [0-9.]+s\s+OK\s*$',python_log.read_text())
for filename,p in [('publication-final-stdout.log',w/'publish-current-exhausted-owner-v1.log'),('postpublication-all-python-tests.log',python_log),('postpublication-governance.log',w/'postpublication-current-governance-v1.log')]:
    (ev/filename).write_bytes(p.read_bytes())
verifier=ev/'independent-publication-verifier.py';verifier.write_bytes(Path(__file__).read_bytes())
raw=gp.read_bytes();zipped=ev/'postpublication-current-governance-original.json.gz'
zipped.write_bytes(gzip.compress(raw,mtime=0));assert gzip.decompress(zipped.read_bytes())==raw
result={'atUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'status':'INDEPENDENT_SOURCE_ARCHIVE_CLOSED_SET_OLD_REVIEW_CONTEXT_GATE_OBLIGATION_IDS_AND_FINAL_PYTHON_PASS','all155OldReviewsAnd70WholeContextsExact':True,'all245OldGateFieldsStatesAndOrderExact243WholeGateObjectsExact':True,'allOldSourceUniverseDatesDeltasCoverageRetained':True,'all16969OriginalObligationIdsRetained':True,'newObligations':sorted(added),'newBoundedSqlBranches':4,'newSeparateOpenSourceDamageFindings':2,'currentObligations':16975,'semanticCoverageCertified':False,'all332ArchiveMembersExactAndClosedSetVerified':True,'archive':pin(archive),'all3326ActualSourceInputsStillExact':True,'all52OriginalSqlIdentitiesOrderAndSourceClassesExactOnly53rdAppend':True,'toolTests':74,'toolTestLog':pin(ev/'postpublication-all-python-tests.log'),'originalGovernanceOutput':{'rawBytes':len(raw),'rawSha256':hashlib.sha256(raw).hexdigest(),'losslessGzip':pin(zipped),'plainRetiredAfterExactLosslessVerification':True},'verifierSource':pin(verifier),'onlyOneCheckAndNoPostCheckGameplayOrVerificationInputChanges':True,'newFixedFullVisiblePhysicalWindowsAndWholePrintingCertification':'OPEN'}
with (ev/'independent-publication-and-retention-verification.json').open('x') as f:json.dump(result,f,indent=2);f.write('\n')
gp.unlink()
print('Independent publication PASS:155 old reviews/70contexts/245states/16969 IDs exact;4bounded+2OPEN=>16975;332 archive members;3326 source inputs;52SQL prefix+53rdonly;74 tools;governance lossless compressed',len(raw),'to',zipped.stat().st_size)
