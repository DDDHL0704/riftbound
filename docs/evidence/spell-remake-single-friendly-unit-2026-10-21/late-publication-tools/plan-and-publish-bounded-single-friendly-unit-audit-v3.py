from pathlib import Path
import copy,datetime,gzip,hashlib,importlib.util,json,re
root=Path('/Users/dinghaolin/MyProjects/riftbound');w=Path(__file__).resolve().parent;out=root/'docs/evidence/spell-remake-single-friendly-unit-2026-10-21';base='a56137a93d48a38d6e2e62b5b7195aa03412577f';identity='spell-remake-single-friendly-unit-no-complete-choice-2026-10-21'
load=lambda p:json.loads(p.read_bytes());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
def write(p,d):
 assert not p.exists(),str(p);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
def pin(p,role):return {'path':str(p.relative_to(root)),'sha256':sha(p),'role':role}
manifest_path=root/'docs/audit/current-manifest.json';reviews_path=root/'docs/evidence/rules-reaudit-2026-10-11/review-decisions.json';prior=load(manifest_path);prior_reviews=load(reviews_path)
assert manifest_path.read_bytes()==gzip.decompress((w/'prior-entire-current-manifest.json.gz').read_bytes()) and reviews_path.read_bytes()==gzip.decompress((w/'prior-entire-review-decisions.json.gz').read_bytes())
source=load(out/'source-manifest.json');source_ref=pin(out/'source-manifest.json','source-manifest');eligible=source['exactEightConsumers'];scope=source['scope'];doc=root/'docs/SPELL_REMAKE_SINGLE_FRIENDLY_UNIT_COMPLETION_AUDIT_2026-10-21.md';assert doc.is_file()
reviews_temp=w/'planned-entire-review-decisions-v1.json';reviews=load(reviews_temp);assert reviews['reviews'][:149]==prior_reviews['reviews'] and reviews['reviews'][-1]['id']==identity
old_ids=set(load(root/'docs/evidence/spell-remake-declaration-scope-2026-10-21/remaining-obligation-ids.json'));assert len(old_ids)==16936
spec=importlib.util.spec_from_file_location('gov',root/'scripts/audit/check-project-governance.py');checker=importlib.util.module_from_spec(spec);spec.loader.exec_module(checker)
old_result=checker.current_audit(root,prior)
assert (w/'publication-v1-predecessor-current-pointer-assumption-failure-disposition.json').is_file()
rule=source['exactClauseRecord']
manifest=copy.deepcopy(prior);revision=checker.digest(checker.gameplay_inputs(root));prior_ref=pin(w/'prior-entire-current-manifest.json.gz','prior-entire-manifest-gzip');updates=[]
for key,context in manifest['reviewContexts'].items():
 changes=[]
 for field in ['implementation','verificationSources']:
  for ref in context.get(field,[]):
   h=sha(root/ref['path'])
   if h!=ref['sha256']:changes.append({'field':field,'path':ref['path'],'previousSha256':ref['sha256'],'currentSha256':h});ref['sha256']=h
 meta={f:context.get(f) for f in ['currentInputRevision','currentImplementationCommit','currentImplementationIncludesUncommittedChanges','ciState']}
 context.setdefault('implementationPinUpdates',[]).append({'batch':identity,'changes':changes,'previousCurrentMetadata':meta,'previousEntireContextPreservedIn':prior_ref,'scope':'Only current declared single-friendly-unit preflight and append-only SQL helper verification. Every old scope, source date and evidence remains; current fixed full and product still required.'})
 context.update(currentInputRevision=revision,currentImplementationCommit=base,currentImplementationIncludesUncommittedChanges=True,ciState='PENDING',revalidationReason='New bounded shared single-friendly-unit preflight; prior fixed CI does not cover current input. Original scopes/evidence retained, current full and product remain required.')
 if changes:updates.append({'review':key,'changes':changes})
branches=[]
for row in eligible:
 card=row['cardNo'].replace('·','-');key='SINGLE_FRIENDLY_NO_COMPLETE_'+re.sub('[^A-Za-z0-9]+','_',card)
 branches.append({'id':key,'scope':'Exact API'+str(row['apiId'])+' '+card+': hand-cast one fixed friendly-unit target declaration; either current control grant, no repeats/paid optional cost/standby/movement/mode changes. Parent effects beyond the decision remain separate.','sourceRefs':[card,'OGN-080/298','VEN-152','VEN-152/166',rule],'state':'RECORDED','sourceManifest':source_ref,'decision':{'state':'OPEN','reason':'Bounded current source, implementation, conformance and shared controlled-SQL evidence pass; final fixed full, exact product and other semantic obligations remain.'},'semanticReview':'BOUNDED_CURRENT_CLAUSE_AND_EXACT_DECLARATION_ONLY','implementation':'CURRENT_SHARED_PREFLIGHT_WITH_EXISTING_AUTHORITATIVE_WHOLE_DECLARATION_VALIDATOR','testExecution':'ACTUAL38_NEW_CASES_FOCUS134_SQL10_SHARED_SPINE_CLAIR5_ORIGINAL38_PAIR_HELPERS_ONLY_CHECK13214_PASS','fixedCommitCi':'PENDING','productAcceptance':'OPEN_WITH_EXACT_PRIOR_NATIVE_CASE_FAILURE_ISSUE77','wholePrintingCertified':False,'existingAcceptanceGateDependencies':source['existingAcceptanceGateDependencies']})
manifest['reviewContexts'][identity]={'evidenceCommit':base,'currentImplementationCommit':base,'currentImplementationIncludesUncommittedChanges':True,'currentInputRevision':revision,'ciState':'PENDING','ciReceipts':[],'region':'CN','language':'zh-CN','applicableAsOf':'2026-10-21','adoptedCoreEffectiveDate':'2026-10-16','sourceManifest':source_ref,'implementation':source['implementation'],'verificationSources':source['verificationSources'],'evidence':[source['checkOriginalMethodCaseAndInputRetention'],source['actualSqlTerminal'],source['independentSqlHistory'],source['originalAffected38Pairs']],'branches':branches,'existingAcceptanceGateDependencies':source['existingAcceptanceGateDependencies'],'currentDefaultSqlExecutionIdentities':48,'semanticWholePrintingCertified':False}
manifest['inputs']['reviews']=pin(reviews_temp,prior['inputs']['reviews'].get('role','reviews'));manifest['currentSingleFriendlyUnitCompleteRemakeInputChange']=source_ref;manifest['currentFixedA56137NativeHandleCaseFinding']=pin(out/'native-handle-case-original-failure.json','exact-native-product-source-failure-issue77')
result=checker.current_audit(root,manifest);assert result['governanceValid'],result['errors'];ids={r['id'] for r in result['remainingObligations']};assert old_ids<=ids and len(ids)==16944,(len(ids),sorted(old_ids-ids))
assert reviews['reviews'][:149]==prior_reviews['reviews'] and manifest['acceptanceGates']==prior['acceptanceGates'] and manifest['laterSourceDeltas']==prior['laterSourceDeltas'] and manifest['certificationScope']==prior['certificationScope']
write(out/'prior-scope-and-obligation-identity-retention.json',{'atUtc':now(),'status':'EXACT_ALL_PRIOR149_REVIEWS64_CONTEXT_SCOPES245_GATES7_DELTAS16936_IDS_RETAINED_PLUS_EIGHT_SCOPED_BRANCHES','reviewCount':150,'contextCount':65,'gateCount':245,'sourceDeltaCount':7,'remainingObligations':16944,'allOriginal16936IdsRetained':True,'addedIds':sorted(ids-old_ids),'originalEntireManifestAndReviewsByteSnapshotsInArchive':True,'onlyCurrentImplementationBindingUpdates':updates,'wholePrintingAndGlobalSemanticCertification':False,'currentFixedFullAndProduct':'OPEN'})
write(out/'remaining-obligation-ids.json',sorted(ids));reviews_path.write_bytes(reviews_temp.read_bytes());manifest['inputs']['reviews']=pin(reviews_path,prior['inputs']['reviews'].get('role','reviews'));manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
status_path=root/'docs/PROJECT_STATUS.md';old=status_path.read_text();intro='''# 当前项目状态

2026-10-21 本批：[单个友方单位的完整重选检查](SPELL_REMAKE_SINGLE_FRIENDLY_UNIT_COMPLETION_AUDIT_2026-10-21.md)。CN/zh-CN、核心10-16生效；753.2只收敛一次执行/一个友方单位目标、其他声明固定的八个精确消费者。无完整合法新选择时保留原选择并完成控制，仍有合法替代则保留窗口；原服务端校验和独立安全验证不变，无通用搜索/DSL/旧局迁移。其他目标、重复、额外费用、待命、移动/模式/目的地、其他印次和完整效果继续OPEN。

新增38例/5方法，focus134/42、较早162/67、两条原完整对局及38个原受影响方向通过。唯一check13214例/7499方法全过，原13176用例名称/次数和7494方法完整保留，只增38/5；3317源码/执行位、1419Conformance和23原生载荷全程稳定，原143完整方法/110配对/11牌组/424242及胜利/回放断言不变，无第二次check。原48 SQL身份/顺序及原八分支保留，只追加两场；10受控场景84命令175事件、84冷恢复/20直接拒绝、84状态/双方合计168快照提示及全原回放通过。旧Clair5分支27/64/27冷恢复/15结构拒绝保留；中间行仍由旧助手清理，不称留库。原221审计47原生七表不变，新10审计局留库至231，服务正常停止。

上一固定a56137的包、三架构四个SignalR8.0.32载荷、默认启动和373CI托管/PCK字节/签名核验，当前只留最新A/B与每平台一包；退役旧564约946MB逻辑内容，无新SDK/checkout/数据库。真实CI导出客户端的零目标/合成报价保护在锁屏下通过；命令行切场景被拒的失败保留。实际双端因认证小写与房间保留原大小写而无法恢复，见[#77](https://github.com/DDDHL0704/riftbound/issues/77)，接受原生游戏命令0。保留同一原28命令局，用原版本/原身份网关续20命令正常48命令259事件、8:0第18回合，一征服七驻守、非投降/耗尽；48状态92快照92提示、全部原字段/null/隐私和全原回放通过。私有表按玩家/tick保留最新事件序列，开局先前同tick私有行不称留存；全部48权威历史和开始前五表行重数保留。原231审计47旧原生七表不变，同一新原生局留库至48。网关通过不改写原生失败，可见双端、Windows实机和最终候选产品验收仍OPEN，API/库正常停止。

父提交a56137实际CI第3片12身份/120输出、新八场69/145及网络15拒绝0泄露/真实恢复原件独立核实，整次full仍待独立终态和全部产物，不能称全绿。旧564全CI只属旧范围，旧0366整次失败保留。本批当前Release210fc、本地干净a56137 API bde6与父CI ff632身份分别记录，不跨构建改写。

150限定审查/65上下文/245门禁/7增量/16944统一义务；原149审查、64上下文范围证据、全部245门禁和7增量原值、16936义务身份及1740印次14293块/CN236处置/EN8缺口/139公告全部保留，仅增八限定分支。OGN053两种官方署名作为非游戏元数据同时保留，不推定字段角色、换版或规则变化。当前认证日期和原来源日期不静默更新，最终验收前重查已生效来源。限定通过不提升整卡或全量，Goal active / NOT READY。

以下保留此前各批的原始限定证据与终态；当前范围以本批精确收据为准。

'''
assert old.startswith('# 当前项目状态\n');status_path.write_text(intro+old.removeprefix('# 当前项目状态\n\n'))
print('BOUNDED_CANONICAL_PUBLISHED',150,65,245,7,16944,'only8 new branches; all old scopes and identities retained',flush=True)
