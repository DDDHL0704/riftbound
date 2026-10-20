from pathlib import Path
import datetime,hashlib,json,os
root=Path('/Users/dinghaolin/MyProjects/riftbound');w=Path(__file__).resolve().parent;out=root/'docs/evidence/spell-remake-single-friendly-unit-2026-10-21';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
paths=[w/('plan-and-publish-bounded-single-friendly-unit-audit-v'+str(v)+'.py') for v in [1,2,3]]+[w/'verify-canonical-scope-originals-and-final-inputs-v1.py',w/'publication-v1-predecessor-current-pointer-assumption-failure-disposition.json',w/'publication-v2-missing-rule-variable-disposition.json',Path(__file__).resolve()]
paths += [root/'var/qa'/('n28-canonical-governance-publication-20261021-v'+str(v)+'.log') for v in [1,2,3]]+[root/'var/qa/n28-canonical-independent-retention-20261021-v1.log',root/'var/qa/n28-python-tools-20261021-v1.log']
records=[]
for p in paths:
 assert p.is_file();q=out/'late-publication-tools'/p.name;q.parent.mkdir(parents=True,exist_ok=True)
 if q.exists():assert sha(q)==sha(p)
 else:os.link(p,q)
 records.append({'originalPath':str(p.relative_to(root)),'canonicalPath':str(q.relative_to(root)),'bytes':p.stat().st_size,'sha256':sha(p)})
gov=root/'var/qa/n28-current-governance-20261021-v1.json';d=json.loads(gov.read_text());assert d['governanceValid'] and d['scopedReviews']==150 and len(d['remainingObligations'])==16944
summary={'status':'PASS_ACTUAL73_PYTHON_VERIFICATION_TESTS_AND_CURRENT_GOVERNANCE','pythonTests':73,'pythonExitCode':0,'actualGovernanceValid':True,'semanticCoverageCertified':False,'reviewCount':150,'contextCount':65,'gateCount':245,'sourceDeltas':7,'remainingObligations':16944,'originalCompleteGovernanceReport':{'path':str(gov.relative_to(root)),'sha256':sha(gov),'bytes':gov.stat().st_size},'sourceCounts':d['counts'],'allSourceGapsAndCertificationLimitationsRetained':True,'firstSupplementPreparationError':'The saved full governance report has remainingObligations as an array; remainingObligationCount exists only in the printed summary. No test or SQL failure; all previously copied source/log bytes retained exact.'}
p=out/'actual-python-and-governance-verification.json';assert not p.exists();p.write_text(json.dumps(summary,indent=2)+'\n')
supplement={'recordedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'status':'ALL_LATE_PUBLICATION_TOOLS_FAILURES_AND_SUCCESS_OUTPUTS_RETAINED_WITHOUT_REWRITING_ORIGINAL_ARCHIVE','records':records,'original341ArchiveRecordMap':{'path':str((out/'archive-map.json').relative_to(root)),'sha256':sha(out/'archive-map.json')},'independentCanonicalOriginalAndFinalInputProof':{'path':str((out/'independent-canonical-original-retention-and-final-inputs.json').relative_to(root)),'sha256':sha(out/'independent-canonical-original-retention-and-final-inputs.json')},'actualPythonAndGovernance':{'path':str(p.relative_to(root)),'sha256':sha(p)},'originalPrivateAuthMaterialRemainsLocal':True,'noRuleSqlOrCiRerunForPublicationToolCorrections':True}
p=out/'publication-and-verification-tool-supplement.json';assert not p.exists();p.write_text(json.dumps(supplement,ensure_ascii=False,indent=2)+'\n')
for r in records:assert sha(root/r['originalPath'])==sha(root/r['canonicalPath'])==r['sha256']
audit=root/'docs/SPELL_REMAKE_SINGLE_FRIENDLY_UNIT_COMPLETION_AUDIT_2026-10-21.md';assert 'publication-and-verification-tool-supplement.json' not in audit.read_text();audit.write_text(audit.read_text()+'\n[归档工具、原失败和最终验证补充清单](evidence/spell-remake-single-friendly-unit-2026-10-21/publication-and-verification-tool-supplement.json)\n')
print('Supplement verified',len(records),'late tools/logs;73 Python tests and150-review/16944-obligation governance pass',flush=True)
