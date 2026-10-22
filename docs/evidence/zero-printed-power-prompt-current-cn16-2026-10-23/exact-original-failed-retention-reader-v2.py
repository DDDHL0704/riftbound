from pathlib import Path
import gzip,hashlib,json
root=Path('/Users/dinghaolin/MyProjects/riftbound');z=Path(__file__).resolve().parent;ev=root/'docs/evidence/zero-printed-power-prompt-current-cn16-2026-10-23'
load=lambda p:json.loads(p.read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
current=load(z/'final-coherent-current-governance-v2.json');previous=load(root/'var/qa/deck-look-draw-graveyard-current-cn16-20261022/final-coherent-current-governance-v2.json')
assert current['governanceValid']and current['semanticCoverageCertified']is False and not current['errors']
assert current['currentReviewCount']==169 and len(current['remainingObligations'])==17390
old_ids={r['id']for r in previous['remainingObligations']};new_ids={r['id']for r in current['remainingObligations']};assert len(old_ids)==17384 and len(new_ids)==17390 and old_ids<=new_ids
identity='zero-printed-power-prompt-current-cn16-2026-10-23';branches=load(ev/'five-exact-behavior-branches.json')['branches'];assert len(branches)==5
expected={'BRANCH:'+identity+':'+b['id']for b in branches}|{'GATE:ZERO_PRINTED_POWER_PROMPT_CURRENT_FIXED_MAIN_AND_PRODUCTS'};assert new_ids-old_ids==expected
m=load(root/'docs/audit/current-manifest.json');prior=z/'prior-coherent-publication-inputs-v1';old=json.loads(gzip.decompress((prior/'current-manifest.json.gz').read_bytes()));old_reviews=json.loads(gzip.decompress((prior/'review-decisions.json.gz').read_bytes()));reviews=load(root/m['inputs']['reviews']['path'])
assert len(m['reviewContexts'])==84 and len(reviews['reviews'])==169 and reviews['reviews'][:-1]==old_reviews['reviews']and len(old_reviews['reviews'])==168
assert len(m['acceptanceGates'])==270 and m['acceptanceGates'][:-1]==old['acceptanceGates']and m['acceptanceGates'][-1]['id']=='ZERO_PRINTED_POWER_PROMPT_CURRENT_FIXED_MAIN_AND_PRODUCTS'
assert m['laterSourceDeltas']==old['laterSourceDeltas']and len(m['laterSourceDeltas'])==7 and m['certificationScope']==old['certificationScope']and m['historicalReviewPin']==old['historicalReviewPin']
assert json.loads(gzip.decompress((ev/'all-original83-complete-contexts.json.gz').read_bytes()))==old['reviewContexts']
refreshed=json.loads(gzip.decompress((ev/'thirty-eight-input-refreshes-complete-old-contexts.json.gz').read_bytes()));ids={r['id']for r in refreshed};assert len(ids)==38
for r in refreshed:
 assert r['oldCompleteContext']==old['reviewContexts'][r['id']]and len(r['changes'])==1 and r['changes'][0]['path']=='src/Riftbound.Engine/MatchSession.cs'
for k,c in old['reviewContexts'].items():
 now=m['reviewContexts'][k];assert now.get('verificationSources')==c.get('verificationSources')and('verificationSources'in now)==('verificationSources'in c)
 if k not in ids:assert now==c
assert sum(k not in ids for k in old['reviewContexts'])==45
assert all(m['inputs'][k]==old['inputs'][k]for k in old['inputs']if k!='reviews')
old_coverage=load(root/old['cardCoverageManifest']['path']);coverage=load(root/m['cardCoverageManifest']['path']);assert all(coverage[k]==v for k,v in old_coverage.items()if k!='currentScopedReviewDecisions')and coverage['currentScopedReviewDecisions']==m['inputs']['reviews']
assert coverage['scopedReviewDecisions']==old_coverage['scopedReviewDecisions']and coverage['counts']==old_coverage['counts']
assert current['counts']['combinedExactPrintingNumbers']==1740 and current['counts']['activeCatalogEntries']==1037 and current['counts']['enSourceCountGap']==8
assert all(b['decision']['state']=='OPEN'and b['wholePrintingCertified']is False for b in branches)
result=dict(status='PASS_ALL17384_OLD_OBLIGATION_IDS_PLUS_FIVE_BEHAVIORS_AND_ONE_PRODUCT_GATE_ONLY',oldRemainingObligations=17384,newBehaviorBranches=5,newProductGates=1,currentRemainingObligations=17390,currentReviews=169,currentContexts=84,currentGates=270,existingSevenVersionDeltasExact=True,allOldReviewObjectsGatesDatesAndScopeExact=True,all83CompleteOldContextsArchived=True,only38CurrentImplementationInputPinsRefreshed=True,other45WholeContextObjectsAndAllOptionalVerificationRefsExact=True,original1740Cards14293Rules1037RuntimeAndAllSourceCoverageCountsUnchanged=True,allNewBehaviorDecisionStatesOpen=True,semanticCoverageCertified=False,newFixedFullAndNativeProductsAndWholeGoalRemainOpen=True,governanceOutputSha256=sha(z/'final-coherent-current-governance-v2.json'))
out=z/'final-publication-complete-obligation-and-original-context-retention-v2.json';assert not out.exists();out.write_text(json.dumps(result,indent=2)+'\n');print(result['status'],flush=True)
