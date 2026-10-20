from pathlib import Path
import copy,datetime,gzip,hashlib,importlib.util,json,re
root=Path('/Users/dinghaolin/MyProjects/riftbound');w=Path(__file__).resolve().parent;out=root/'docs/evidence/spell-remake-single-friendly-unit-2026-10-21';base='a56137a93d48a38d6e2e62b5b7195aa03412577f';identity='spell-remake-single-friendly-unit-no-complete-choice-2026-10-21'
load=lambda p:json.loads(p.read_bytes());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
def write(p,d):
 assert not p.exists(),str(p);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
def pin(p,role):return {'path':str(p.relative_to(root)),'sha256':sha(p),'role':role}
manifest_path=root/'docs/audit/current-manifest.json';reviews_path=root/'docs/evidence/rules-reaudit-2026-10-11/review-decisions.json';prior=load(manifest_path);prior_reviews=load(reviews_path)
assert len(prior_reviews['reviews'])==149 and len(prior['reviewContexts'])==64 and len(prior['acceptanceGates'])==245 and len(prior['laterSourceDeltas'])==7
assert manifest_path.read_bytes()==gzip.decompress((w/'prior-entire-current-manifest.json.gz').read_bytes()) and reviews_path.read_bytes()==gzip.decompress((w/'prior-entire-review-decisions.json.gz').read_bytes())
check=load(w/'only-check-independent-case-method-and-input-retention-v1.json');assert check['actualCases']==13214 and check['newCases']==38 and check['onlyOneCheckExecuted']
sql=load(w/'actual-ten-and-clairvoyance-owner-v2.json');assert sql['status']=='PASS_CURRENT_TEN_AND_FIVE_REAL_SQL_PLUS_INDEPENDENT_HISTORY_REPLAY' and sql['ownPostgresSmartStopped']
parent=load(root/'docs/evidence/spell-remake-declaration-scope-2026-10-21/source-manifest.json');consumers=load(w/'finite-friendly-unit-consumer-source-and-scope-review-v1.json');eligible=[r for r in consumers['records'] if r['declarationScope']!='EXCLUDED_MOVEMENT_DECLARATION_REMAINS_OPEN'];assert len(eligible)==8
scope='Rule753.2 finite hand-cast single friendly-unit target declarations: no complete legal new target under the new controller, with one execution/one target slot and all other declarations fixed. Preserve original choices and finish control. No general reachability, other targets, repeats, paid optional costs, standby or movement/mode/destination certification.'
impl=['src/Riftbound.Engine/CoreRuleEngine.SpellChoiceControl.cs'];verification=['tests/Riftbound.ConformanceTests/SpellRemakeSingleTargetCompletionTests.cs','tests/Riftbound.PostgresAcceptance/SpellRemakeDeclarationSqlAcceptance.cs']
source={'recordedAtUtc':now(),'region':'CN','language':'zh-CN','applicableAsOf':'2026-10-21','adoptedCoreEffectiveDate':'2026-10-16','baseCommit':base,'includesDeclaredUncommittedChanges':True,'scope':scope,
 'currentEntireOfficialSourceRound':prior['currentSourceRound20261021'],'exactCoreClauseBindings':parent['coreExactClauseBindings'],'exactClauseRecord':'CN-d6fd7140f106f9b5-P111-B022','coreClause':'753.2',
 'sourceCoverageUnchanged':{'originalPrintingIdentities':1740,'originalRuleBlocks':14293,'cnFullRows':1504,'enReported':1416,'enReturned':1408,'enUnknownGap':8,'cnPriorSourceDispositions':236,'currentNewsIdentities':139,'sourceDeltaCount':7},
 'exactEightConsumers':eligible,'twoMovementConsumersRemainOpen':[r for r in consumers['records'] if r not in eligible],
 'nonGameplaySourceAttributionDisposition':{'apiId':31265,'rawCardNo':'OGN·053/298','apiArtist':'Kudos Productions','imageArtist':'Six More Vodka','state':'BOTH_LITERAL_OFFICIAL_VALUES_RETAINED_NON_GAMEPLAY_METADATA_NO_PRINTING_OR_RULE_CHANGE_INFERRED','fieldMeaningNotAssumed':'API credit may refer to a different attribution role; no automatic correction or new gameplay condition.','wholePrintingCertification':False},
 'implementation':[pin(root/p,'implementation') for p in impl],'verificationSources':[pin(root/p,'verification-source') for p in verification],
 'actualControlledRed':{'cases':4,'methods':2,'failed':2,'passed':2,'status':'ORIGINAL_DEFECT_REPRODUCTION_NOT_GAMEPLAY_PASS'},
 'actualCurrentTests':{'newCases':38,'newMethods':5,'finiteConsumerFocusCases':134,'finiteConsumerFocusMethods':42,'earlierBoundedFocusCases':162,'earlierBoundedFocusMethods':67,'originalFullGameSmokeMethods':2,'additionalOriginalAffectedPairHelpers':38,'actualOnlyCheckCases':13214,'actualOnlyCheckMethods':7499,'failed':0,'skipped':0,'allOriginal13176CaseMultiplicitiesAnd7494MethodsRetained':True,'original143FullMethods110Pairs11Decks424242AndAssertionsRetained':True},
 'actualSql':{'defaultIdentitiesAndOrder':48,'originalEightBranchesPreserved':True,'currentControlledScenarios':10,'commands':84,'events':175,'coldRecoveries':84,'directAtomicRejects':20,'independentAuthorityStates':84,'bothPlayersCombinedHistoricalSnapshots':168,'bothPlayersCombinedHistoricalPrompts':168,'originalClairvoyanceBranches':5,'clairCommands':27,'clairEvents':64,'clairColdRecoveries':27,'clairStructuralTamperRejects':15,'oldClairIntermediateRowsStillCleanedByUnchangedHelperNotClaimedRetained':True,'original221Audit47NativeSevenTablesExact':True,'newAuditRoomsRetained':10,'resultingAuditRooms':231,'ordinaryOpeningOrWholeCardClaim':False},
 'actualN28ReleaseRuleInputs':load(w/'current-release-sql-frozen-publication-v1.json')['actualRuleInputs'],
 'checkOriginalMethodCaseAndInputRetention':pin(out/'check-original-method-case-and-input-retention.json','check'),'actualSqlTerminal':pin(out/'real-sql-ten-plus-clair-five-terminal.json','sql-actual-terminal'),'independentSqlHistory':pin(out/'real-sql-independent-original-history-readback.json','independent-sql-readback'),'originalAffected38Pairs':pin(out/'original38-affected-pair-helpers-terminal.json','original-complete-game-helpers'),
 'precedingFixedA56137ProductAndCi':{'fixedCommit':base,'localCleanApiRuleInputs':load(root/'var/qa/fixed-a56137-native-locked-20261021/fixed-clean-api-runtime-source-and-identity-v3.json')['actualRuleInputs'],'actualCurrentCiSourcePart3AndNetworkOnly':pin(out/'fixed-a56137-actual-part3-and-network-originals.json','preceding-fixed-ci-partial-only'),'actualExportedDefaultMainZeroTargetAndSyntheticQuoteGuard':pin(out/'fixed-a56137-exported-zero-target-native-controls.json','preceding-fixed-native-bounded-only'),'actualNativeRestoreFailure':pin(out/'native-handle-case-original-failure.json','native-case-source-failure'),'issue77Readback':pin(out/'native-handle-case-issue77-readback.json','native-case-issue'),'sameOriginalRoomGatewayContinuation':pin(out/'fixed-a56137-original-room-gateway-normal-score-terminal.json','gateway-only-normal-score'),'independentOriginalRoom48States92Snapshots92PromptsAndReplay':pin(out/'fixed-a56137-original-room-independent-sql-and-full-replay.json','independent-gateway-original-history'),'nativeAcceptedGameplayCommands':0,'normalGatewayTotalCommands':48,'normalGatewayEvents':259,'winningScore':8,'losingScore':0,'finalTurn':18,'privateStorageContract':'Latest projection per viewer/tick; all48 authoritative tick/sequence states retained. Earlier same-tick private rows at opening are superseded and not claimed retained.','normalGatewayDoesNotRelabelNativeFailure':True,'entireCurrentFullCiStillNotIndependentlyVerified':True,'physicalWindowsAndVisibleCurrentPair':'OPEN'},
 'existingAcceptanceGateDependencies':prior['reviewContexts'][list(prior['reviewContexts'])[-1]]['existingAcceptanceGateDependencies'],
 'remaining':['All other target scopes and behavior constraints','More than one target or execution; external repeats and paid optional costs','Standby declarations, modes, destinations and movement consumers','All other printings and aliases; complete effects of the eight cards beyond this declaration decision','Current final fixed full and ordinary-product acceptance, including issue77, visible pair and physical Windows','All original source gaps and full-source obligations'],
 'originalArchive':pin(out/'archive-map.json','original-byte-archive-map'),'publicOpeningKeyDiagnosticSupplement':pin(out/'explicit-public-opening-key-diagnostic-supplement.json','read-only-diagnostic-supplement'),'latestOnlyCleanup':pin(out/'latest-only-native-app-and-installer-retirement.json','latest-only-cleanup'),'noNewSdkCheckoutDatabaseOrNativeInstallerBeyondReplacement':True,'wholePrintingAndGlobalSemanticCertification':False}
write(out/'source-manifest.json',source);source_ref=pin(out/'source-manifest.json','source-manifest')
doc=root/'docs/SPELL_REMAKE_SINGLE_FRIENDLY_UNIT_COMPLETION_AUDIT_2026-10-21.md';assert not doc.exists()
rows='\n'.join('| '+r['cardNo']+' | '+str(r['apiId'])+' | 仅此手牌打出的一目标重选分支 |' for r in eligible)
doc.write_text('''# 单个友方单位的完整重选检查

本轮范围 CN / zh-CN，观察日期2026-10-21，采用10-16生效核心规则；原认证范围、来源日期、1740印次、14293块、CN236处置、EN8未知身份和139公告全部保留。来源新变化仍单列，最终候选前重新检查已生效变化。本批不是整卡、全规则族或全量完成。

753.2要求无法做出完整合法新选择时保留原选择。原四例中两个“新控制者没有友方单位”反例失败、两个有合法替代的正例通过；实际原版本的19指令/34事件及十个完整检查点、部分编辑后仍无法确认的诊断回放保留。这是原缺陷证据。

共享流程只预检一次执行、一个友方单位目标，其他声明固定且未支付额外可选费用。使用原服务端完整合法性校验检查原声明、空声明和全部当前对象候选；没有合法组合时完成控制并保留全部原目标，仍有合法选择时保留原重选窗口。没有通用组合搜索、DSL扩张或旧局迁移。独立恢复与拒绝不变性保留。

| 精确当前印次 | API ID | 限定处置 |
|---|---:|---|
'''+rows+'''

八张当前卡面已逐项查看。暗影的召唤额外验证未拥有瞬息；手牌及坟场单位不构成在场合法目标。两张移动声明消费者OGN173/UNL101继续OPEN；待命打出、回响及其他重复、额外费用、模式/目的地、多目标、其他作用域、其他印次和完整结算效果逐项保留。登记和本分支通过均不提升整卡认证。

OGN053/API31265的API署名为Kudos Productions，图像署名为Six More Vodka；两原值均保留，未推定字段角色或自动纠正。这是非游戏元数据处置，不据此新增目标限制或推断换版。单印次首次发布/独立生效日期未知，不以取得时间或版权年替代。

新增38例/5方法，原四例/两方法完整保留。有限消费者focus134例/42方法通过，较早收窄后focus162例/67方法、两条原完整得分/回放以及38个原受影响配对助手通过。38方向来自原11预组中含OGN104的两个牌组，仍执行原牌组、房间/意图、424242种子和胜利/完整回放断言；不替代完整110公开矩阵方法。

唯一check实际13214例/7499方法全过，零失败/跳过；原13176用例名称/次数多重集合及7494方法逐一保留，只增38/5。原基线的一次旧驱动失败不改写。检查全程3317源码/执行位、1419Conformance和23原生载荷稳定，无第二次check；全部原143完整方法/110配对/11牌组/种子和断言保留。

真实PostgreSQL在原48身份/顺序和原八分支上追加两分支：10受控场景84命令175事件、84冷恢复/20直接原子拒绝；旧千里眼5分支27命令64事件、27冷恢复/15结构篡改拒绝保留。独立84历史状态、双方合计168快照/168提示、全部原动作/事件/终态回放通过。原221审计/47原生七表不变，新10局留库至231；千里眼中间行仍由原助手清理，不称留库。专用库和防休眠正常结束。这些是明确受控初态，不称普通开局或十张整卡认证。

此前固定a56137的两平台包和三架构四个SignalR8.0.32程序集、默认启动、签名及373托管/PCK文件已核验；仅最新A/B和每平台一个ZIP保留，旧564两应用/两包退役约946MB逻辑内容，APFS实际释放量不据此推定。无新SDK、checkout或数据库。

固定a56137实际CI第3 SQL片12身份/120原输出、新八场69/145、网络15拒绝/零泄露及真实连接恢复原件已独立读回；整次full和其他片仍分别等待，不能称完整通过。该CI使用其ff632实现身份，本地干净a56137 API为bde6，本批未提交Release为210fc；来源、执行文件和实现身份分别绑定，不跨版本改写。

锁屏下，真实CI导出客户端通过已有Main证明入口验证当前SFD080零目标和原合成报价过期保护；它不是HTTP报价/提交或可见验收。命令行切换场景被导出程序拒绝的原失败保留，退出0未被当作通过。实际双端随后认证成功却因房间保留混合大小写、认证转小写而恢复被拒，见[#77](https://github.com/DDDHL0704/riftbound/issues/77)。原生接受游戏命令0；原失败不改写。

保留同一原28命令局，通过原版本和原身份的生产网关继续20命令，正常48命令259事件、8:0第18回合，一征服七驻守、双方牌堆未空，无投降；48历史状态、92快照/92提示全部字段/null/隐私和完整原回放独立通过。私有表按玩家和tick保留最新事件序列，开局同tick先前私有行已被原存储契约覆盖，不称全部中间私有行留库；全部48权威历史保留。原历史五表行字节/重数、原231审计/47旧原生七表均不变，同一新原生局留库至48。该网关结果不计原生恢复成功，可见双端和Windows实机继续OPEN，服务正常停止。

八个限定分支分别记录来源语义、实现、测试、固定CI及产品状态。新固定完整CI及当前产品仍待闭合；全量Goal active / NOT READY。原全部剩余义务和来源缺口保留。

[来源、身份和范围manifest](evidence/spell-remake-single-friendly-unit-2026-10-21/source-manifest.json) · [全部原始记录和私有排除清单](evidence/spell-remake-single-friendly-unit-2026-10-21/archive-map.json)
''')
reviews=copy.deepcopy(prior_reviews);subjects=[r['cardNo'].replace('·','-') for r in eligible]+['OGN-080/298','VEN-152','VEN-152/166'];rule='CN-d6fd7140f106f9b5-P111-B022'
reviews['reviews'].append({'id':identity,'scope':scope,'subjects':subjects,'ruleRecords':[rule],'pendingRuleRecords':[],'status':'BOUNDED_EIGHT_DECLARATION_CONSUMERS_FOCUS134_SQL10_OLD_CLAIR5_ORIGINAL38_HELPERS_ONLY_CHECK13214_PASS_FIXED_FULL_AND_PRODUCT_OPEN','sourceManifest':source_ref,'auditDocument':str(doc.relative_to(root)),'validation':source['actualCurrentTests']|{'actualControlledSql':source['actualSql'],'currentFixedCi':'PENDING','productAcceptance':'OPEN_WITH_EXACT_PRIOR_NATIVE_CASE_FAILURE_ISSUE77','allOriginalSourceUniversesAndObligationIdentitiesRetained':True},'exclusions':source['remaining']})
reviews_temp=w/'planned-entire-review-decisions-v1.json';write(reviews_temp,reviews)
spec=importlib.util.spec_from_file_location('gov',root/'scripts/audit/check-project-governance.py');checker=importlib.util.module_from_spec(spec);spec.loader.exec_module(checker)
old_result=checker.current_audit(root,prior);assert old_result['governanceValid'];old_ids={r['id'] for r in old_result['remainingObligations']};assert len(old_ids)==16936
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
