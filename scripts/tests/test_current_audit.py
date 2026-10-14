import copy
import hashlib
import json
from pathlib import Path
import unittest

import test_project_governance as baseline

gov = baseline.gov


class CurrentAuditTests(unittest.TestCase):
    def setUp(self):
        fixture = baseline.GovernanceTests()
        fixture.setUp()
        self.addCleanup(fixture.temp.cleanup)
        self.root = fixture.root
        self.data = copy.deepcopy(fixture.data)
        for name in gov.CANONICAL_DOCS:
            path = self.root/name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('# policy\n')
        self.inputs = {}
        for key, value in self.data.items():
            if key == 'qa':
                continue
            self.inputs[key] = self.save(key+'.json', value)
        self.data['qa'] = dict(catalogSha256=self.inputs['cn']['sha256'], entries=[
            dict(id=c['id'], cardNo=c['cardNo'], cardQaList=None) for c in self.data['cn']['cards']])
        self.inputs['qa'] = self.save('qa.json', self.data['qa'])
        self.historical = self.save('historical.json', self.data['reviews'])
        self.coverage = self.save('coverage.json', dict(counts=self.data['summary'],
            scopedReviewDecisions=dict(sha256=self.historical['sha256'])))
        self.manifest = dict(schemaVersion=1, semanticCoverageCertified=False,
            certificationScope=dict(region='CN', language='zh-CN', applicableAsOf='2026-10-14',
                allOfficialObligationsRetained=True, productPlayers=2,
                sourceDates={k: self.data[k]['date'] for k in ['cards','rules','corpus','summary']}),
            inputs=self.inputs, historicalReviewPin=self.historical, cardCoverageManifest=self.coverage,
            laterSourceDeltas=[], reviewContexts={},
            acceptanceGates=[dict(id='PAIR_SQL_NETWORK', state='OPEN', requirement='Fixed combined product evidence')])

    def save(self, name, value):
        p=self.root/name
        p.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n')
        return dict(path=name, sha256=hashlib.sha256(p.read_bytes()).hexdigest())

    def check(self):
        return gov.current_audit(self.root,self.manifest)

    def test_mixed_original_dates_are_explicit_and_current_universe_stays_complete(self):
        r=self.check()
        self.assertTrue(r['governanceValid'],r['errors'])
        self.assertEqual('2026-10-11',r['snapshotDate'])
        self.assertEqual('2026-10-14',r['certificationScope']['applicableAsOf'])
        self.assertEqual(2,r['counts']['combinedExactPrintingNumbers'])
        self.assertEqual({'CARD:ARC-001/006','CARD:ARC-001A/006'},
            {o['id'] for o in r['remainingObligations'] if o['kind']=='PRINTING'})
        self.assertEqual(5,len(r['remainingObligations']))
        self.assertFalse(r['semanticCoverageCertified'])
        self.assertIsNone(r['completionRate'])

    def test_declared_date_or_dropped_input_role_cannot_pass(self):
        self.manifest['certificationScope']['sourceDates']['corpus']='2026-10-12'
        self.assertFalse(self.check()['governanceValid'])
        del self.manifest['inputs']['active']
        with self.assertRaisesRegex(ValueError,'input roles differ'):
            self.check()

    def test_source_mutation_and_escape_are_rejected_even_before_projection(self):
        (self.root/'cn.json').write_text('{}')
        with self.assertRaisesRegex(ValueError,'hash mismatch'):
            self.check()
        self.manifest['inputs']['cn']=dict(path='../outside.json',sha256='0'*64)
        with self.assertRaisesRegex(ValueError,'escaped pinned'):
            self.check()

    def test_rehashed_qa_and_historical_pin_forgery_still_fail(self):
        qa=copy.deepcopy(self.data['qa']);qa['entries'][0]['cardNo']='ARC-001A/006'
        self.manifest['inputs']['qa']=self.save('qa.json',qa)
        self.assertTrue(any('QA identity/content' in e for e in self.check()['errors']))
        self.manifest['historicalReviewPin']=self.save('different-history.json',dict(reviews=[]))
        self.assertTrue(any('Historical review pin' in e for e in self.check()['errors']))

    def test_green_legacy_label_has_no_effect_on_separate_states_or_remaining_set(self):
        reviews=copy.deepcopy(self.data['reviews']);reviews['reviews'][0]['status']='GREEN_COMPLETE_ALREADY'
        self.manifest['inputs']['reviews']=self.save('reviews.json',reviews)
        r=self.check();view=r['reviewView'][0]
        self.assertEqual('SCOPED_RECORD',view['semanticReview']['state'])
        self.assertFalse(view['semanticReview']['wholeCardCertified'])
        self.assertEqual('REQUIRES_EVIDENCE_ADAPTER',view['testExecution']['state'])
        self.assertEqual('REQUIRES_EXACT_COMMIT_RECEIPT',view['fixedCommitCi']['state'])
        self.assertEqual('OPEN',view['productAcceptance']['state'])
        self.assertEqual(5,len(r['remainingObligations']))

    def test_structured_whole_card_claim_and_manifest_certification_are_rejected(self):
        reviews=copy.deepcopy(self.data['reviews']);reviews['reviews'][0]['semanticReview']=dict(wholeCardCertified=True)
        self.manifest['inputs']['reviews']=self.save('reviews.json',reviews)
        self.assertTrue(any('Structured scoped review' in e for e in self.check()['errors']))
        self.manifest['semanticCoverageCertified']=True
        with self.assertRaisesRegex(ValueError,'cannot certify semantics'):
            self.check()

    def test_source_delta_retains_raw_version_change_without_inventing_semantic_change(self):
        before=self.data['global'];after=copy.deepcopy(before);after['records'][0]['sha256']='c'*64
        self.manifest['laterSourceDeltas']=[dict(kind='rules',before=self.save('before.json',before),after=self.save('after.json',after))]
        r=self.check();delta=r['laterSourceDeltas'][0]
        self.assertEqual([],delta['addedIds']);self.assertEqual([],delta['removedIds'])
        self.assertTrue(delta['changed'][0]['extractedTextIdentical'])
        self.assertFalse(delta['semanticCertification'])
        self.assertEqual(6,len(r['remainingObligations']))

    def test_branch_mapping_requires_real_sources_implementation_and_evidence_pins(self):
        code=self.root/'implementation.cs';code.write_text('class Example {}')
        impl=dict(path='implementation.cs',sha256=hashlib.sha256(code.read_bytes()).hexdigest())
        proof=self.save('receipt.json',dict(status='SCOPED_PASS'))
        proof['role']='check'
        self.manifest['reviewContexts']={'partial':dict(implementation=[impl],evidence=[proof],
            branches=[dict(id='one',sourceRefs=['ARC-001/006','DOC-01-P001-B001'])], ciState='PENDING',ciReceipts=[],
            evidenceCommit='a'*40,currentImplementationCommit='a'*40)}
        r=self.check();self.assertTrue(r['governanceValid'],r['errors'])
        self.assertEqual(1,len(r['reviewView'][0]['branches']))
        self.manifest['reviewContexts']['partial']['branches'][0]['sourceRefs']=['MISSING']
        self.assertFalse(self.check()['governanceValid'])
        code.write_text('class Forged {}')
        self.assertTrue(any('implementation pin mismatch' in e for e in self.check()['errors']))

    def test_ci_receipt_cannot_verify_another_commit_or_the_current_implementation(self):
        code=self.root/'src/Riftbound.Engine/Example.cs';code.parent.mkdir(parents=True);code.write_text('class Example {}')
        impl=dict(path='src/Riftbound.Engine/Example.cs',sha256=hashlib.sha256(code.read_bytes()).hexdigest())
        receipt=self.save('ci.json',dict(status='FIXED_COMMIT_FULL_CI_AND_PARITY_VERIFIED',
            commit='a'*40,runId=123,jobs={'rules':'success'},
            gameplaySourceInputs=dict(commit='a'*40,files=gov.gameplay_inputs(self.root))))
        receipt.update(commit='a'*40,runId=123)
        self.manifest['reviewContexts']={'partial':dict(implementation=[impl],evidence=[],
            branches=[dict(id='one',sourceRefs=['ARC-001/006'])],ciState='VERIFIED',ciReceipts=[receipt],
            evidenceCommit='a'*40,currentImplementationCommit='b'*40)}
        result=self.check();self.assertTrue(result['governanceValid'],result['errors'])
        self.assertFalse(result['reviewView'][0]['fixedCommitCi']['currentImplementationCovered'])
        context=self.manifest['reviewContexts']['partial']
        context['evidenceCommit']='b'*40
        result=self.check()
        self.assertFalse(result['governanceValid'])
        self.assertFalse(result['reviewView'][0]['fixedCommitCi']['currentImplementationCovered'])
        context['evidenceCommit']=context['currentImplementationCommit']='a'*40
        self.assertTrue(self.check()['reviewView'][0]['fixedCommitCi']['currentImplementationCovered'])
        del context['evidenceCommit'];del context['currentImplementationCommit']
        self.assertFalse(self.check()['reviewView'][0]['fixedCommitCi']['currentImplementationCovered'])
        context['evidenceCommit']=context['currentImplementationCommit']='a'*40
        receipt['commit']='b'*40
        self.assertTrue(any('does not match' in e for e in self.check()['errors']))
        self.manifest['reviewContexts']['partial']['ciReceipts']=[]
        self.assertTrue(any('no exact commit receipt' in e for e in self.check()['errors']))

    def verified_branch(self):
        code=self.root/'src/Riftbound.Engine/Example.cs';code.parent.mkdir(parents=True);code.write_text('class Example {}')
        impl=dict(path='src/Riftbound.Engine/Example.cs',sha256=hashlib.sha256(code.read_bytes()).hexdigest())
        receipt=self.save('ci.json',dict(status='FIXED_COMMIT_FULL_CI_AND_PARITY_VERIFIED',
            commit='a'*40,runId=123,jobs={'rules':'success'},
            gameplaySourceInputs=dict(commit='a'*40,files=gov.gameplay_inputs(self.root))))
        receipt.update(commit='a'*40,runId=123)
        check=self.save('check.json',dict(status='CHECK_AND_EXACT_CASE_METHOD_PARITY_PASS',
            oldCasesPreserved=True,oldMethodsPreserved=True,frozenInputsUnchanged=True,failed=0,skipped=0))
        focus=self.save('focus.json',dict(newMethodNames=['IndependentOfficialExpectedCase']))
        check['role']='check';focus['role']='focus'
        source=self.data['cards']['records'][0]['sources']
        fingerprint=hashlib.sha256(json.dumps(source,ensure_ascii=False,sort_keys=True).encode()).hexdigest()
        branch=dict(id='one',sourceRefs=['ARC-001/006'],decision=dict(state='VERIFIED',reason='Independent exact official branch assertions',
            sourceFingerprints={'ARC-001/006':fingerprint},requiredMethods=['IndependentOfficialExpectedCase'],requiredNativeBranches=[]))
        context=dict(implementation=[impl],evidence=[check,focus],branches=[branch],ciState='VERIFIED',ciReceipts=[receipt],
            evidenceCommit='a'*40,currentImplementationCommit='a'*40)
        self.manifest['reviewContexts']={'partial':context}
        return branch,context,code

    def test_finite_branch_closes_without_dropping_identities_or_certifying_whole_card(self):
        branch,_,_=self.verified_branch()
        result=self.check();self.assertTrue(result['governanceValid'],result['errors'])
        self.assertEqual({'VERIFIED':1},result['branchObligationStates'])
        self.assertEqual([],result['unresolvedBehaviorBranches'])
        self.assertEqual(6,len(result['remainingObligations']))
        self.assertEqual(2,len([r for r in result['remainingObligations'] if r['kind']=='PRINTING']))
        self.assertFalse(result['semanticCoverageCertified'])
        self.assertFalse(result['reviewView'][0]['branches'][0]['wholePrintingCertified'])

    def test_missing_proof_changed_source_and_changed_actual_inputs_reopen_branch(self):
        branch,context,code=self.verified_branch()
        original_evidence=copy.deepcopy(context['evidence'])
        original_sources=copy.deepcopy(branch['decision']['sourceFingerprints'])
        context['evidence']=[]
        self.assertEqual({'OPEN':1},self.check()['branchObligationStates'])
        context['evidence']=original_evidence
        branch['decision']['sourceFingerprints']={'ARC-001/006':'0'*64}
        self.assertEqual({'OPEN':1},self.check()['branchObligationStates'])
        branch['decision']['sourceFingerprints']=original_sources
        code.write_text('class Changed {}');context['implementation'][0]['sha256']=hashlib.sha256(code.read_bytes()).hexdigest()
        self.assertFalse(self.check()['reviewView'][0]['fixedCommitCi']['currentImplementationCovered'])
        self.assertEqual({'OPEN':1},self.check()['branchObligationStates'])

    def test_changed_persistence_sql_reopens_previously_verified_ci_coverage(self):
        sql=self.root/'src/Riftbound.Persistence/Sql/009_runtime_inputs.sql'
        sql.parent.mkdir(parents=True)
        sql.write_text('alter table matches add column rule_inputs jsonb;')
        self.verified_branch()
        self.assertTrue(self.check()['reviewView'][0]['fixedCommitCi']['currentImplementationCovered'])
        sql.write_text('alter table matches drop column rule_inputs;')
        result=self.check()
        self.assertFalse(result['reviewView'][0]['fixedCommitCi']['currentImplementationCovered'])
        self.assertEqual({'OPEN':1},result['branchObligationStates'])

    def test_not_applicable_is_scoped_and_reopens_when_product_scope_changes(self):
        branch,context,_=self.verified_branch()
        branch['decision'].update(state='NOT_APPLICABLE',minimumPlayers=3,reason='Branch requires more than the explicit two-player product')
        context['ciState']='PENDING';context['ciReceipts']=[]
        self.assertEqual({'NOT_APPLICABLE':1},self.check()['branchObligationStates'])
        self.manifest['certificationScope']['productPlayers']=4
        self.assertEqual({'OPEN':1},self.check()['branchObligationStates'])


if __name__=='__main__':
    unittest.main()
