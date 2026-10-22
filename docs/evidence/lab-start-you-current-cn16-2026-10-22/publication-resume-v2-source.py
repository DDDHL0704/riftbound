# -*- coding: utf-8 -*-
from pathlib import Path
import copy,datetime,gzip,hashlib,io,json,re,tarfile
root=Path('/Users/dinghaolin/MyProjects/riftbound');w=Path(__file__).resolve().parent
gallery=root/'var/qa/global-gallery-cross-locale-20261022';native=root/'var/qa/fixed3f2dc-early-native-20261022'
load=lambda p:json.loads(p.read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
def write(path,value):
 with path.open('x') as f:json.dump(value,f,ensure_ascii=False,indent=2)
def pin(path,role=None):
 value={'path':str(path.relative_to(root)),'sha256':sha(path)}
 if role:value['role']=role
 return value
final=load(w/'final-only-check-original-retention-v1.json');start=load(w/'only-check-start-inputs-v1.json')
assert (final['actualCases'],final['actualMethods'],final['newCases'],final['newMethods'])==(13436,7543,25,3) and final['outcomes']=={'Passed':13436}
assert all(sha(root/p)==h for p,h in start['sourceInputHashes'].items())
full=load(w/'ci3f2dc-entire-full-independent-verification-v1.json');assert (full['actualCases'],full['actualMethods'],full['sql']['caseCount'])==(13560,7681,58)
parent_full=root/'var/qa/battlefield-start-controller-20261022/ci4014-entire-full-independent-verification-v1.json';assert load(parent_full)['actualCases']==13528
source=load(w/'34762-source-intake-v1.json');assert source['exactId']==34762 and source['allKeysValuesNullsAndQaExactLatestRegistered'] and source['allKeysValuesNullsAndQaExactCn16']
rule_source=load(w/'exact-lab-source-and-core-you-readback-v1.json');assert rule_source['exactApiIds']==[34762] and rule_source['currentCoreOriginalReadback']['sha256']=='d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11'
sql=load(w/'independent-lab-you-sql-result-v1.json');assert [sql[k] for k in ['histories','snapshots','prompts','eventCount','refused']]==[32,58,58,91,4]
branch_prep=load(w/'twenty-five-exact-lab-you-behavior-branch-preparation-v2.json');assert len(branch_prep['cases'])==25 and len(branch_prep['sourceRuleRecordIds'])==10
mp=root/'docs/audit/current-manifest.json';m=load(mp);rp=root/m['inputs']['reviews']['path'];reviews=load(rp);oldm=copy.deepcopy(m);oldreviews=copy.deepcopy(reviews)
assert (len(reviews['reviews']),len(m['reviewContexts']),len(m['acceptanceGates']),len(m['laterSourceDeltas']))==(164,79,250,7)
pin_change=load(w/'current-existing-context-pin-change-readback-before-publication-v2.json');assert pin_change['currentManifestSha256']==sha(mp)
original_contexts=json.loads(gzip.decompress((w/'complete-original79-contexts-before-lab-you-v1.json.gz').read_bytes()))
assert any(v==m['reviewContexts'] for v in original_contexts.values()) or original_contexts==m['reviewContexts']
baseline=root/'docs/evidence/battlefield-start-controller-current-cn16-2026-10-22/final-governance.json.gz';oldgov=json.loads(gzip.decompress(baseline.read_bytes()))
assert oldgov['governanceValid'] and len(oldgov['remainingObligations'])==17156
identity='lab-start-you-current-cn16-2026-10-22';document='docs/LAB_START_YOU_CURRENT_CN16_AUDIT_2026-10-22.md';ev=root/'docs/evidence/lab-start-you-current-cn16-2026-10-22';prior=w/'prior-lab-you-publication-v1'
for path in [mp,rp,root/'docs/PROJECT_STATUS.md']:assert gzip.decompress((prior/(path.name+'.gz')).read_bytes())==path.read_bytes()
source_manifest=load(ev/'source-manifest.json');sp=pin(ev/'source-manifest.json','source-manifest');rule_refs=branch_prep['sourceRuleRecordIds'];branches=load(ev/'twenty-five-exact-lab-you-behavior-branches.json')['branches']
assert len(branches)==25 and source_manifest['exactApiIds']==[34762]
refresh=[]
for review_id,context in m['reviewContexts'].items():
 changes=[]
 for ref in context['implementation']:
  current=sha(root/ref['path'])
  if current!=ref['sha256']:changes.append({'path':ref['path'],'before':ref['sha256'],'after':current});ref['sha256']=current
 if changes:
  assert all(c['path']=='src/Riftbound.Engine/MatchSession.cs' for c in changes)
  refresh.append({'id':review_id,'changes':changes,'oldCompleteContext':oldm['reviewContexts'][review_id]})
  context.update(currentImplementationCommit='7a2fa5b1bfc2551466fc7464dc579f4baf807357',currentImplementationIncludesUncommittedChanges=True,ciState='PENDING',currentLabYouInputRefresh={'sourceManifest':sp,'onlyCurrentImplementationRefsAndExplicitPendingStateRefreshed':True,'allOriginalVerificationSourceReferencesAndEvidenceRetained':True,'newFixedFullAndProducts':'OPEN','noAdditionalWholePriorFindingSemanticCertification':True})
assert len(refresh)==34 and {r['id'] for r in refresh}=={r['contextId'] for r in pin_change['changes']}
assert all(('verificationSources' in c)==('verificationSources' in oldm['reviewContexts'][i]) and c.get('verificationSources')==oldm['reviewContexts'][i].get('verificationSources') for i,c in m['reviewContexts'].items())
assert sum(c==oldm['reviewContexts'][i] for i,c in m['reviewContexts'].items())==45
(ev/'complete-prior-contexts-current-input-refresh.json.gz').write_bytes(gzip.compress(json.dumps(refresh,ensure_ascii=False,indent=2).encode(),mtime=0))
implementation=['src/Riftbound.Engine/CoreRuleEngine.BattlefieldStart.cs','src/Riftbound.Engine/BattlefieldStartRecoveryRules.cs','src/Riftbound.Engine/MatchRecovery.cs','src/Riftbound.Engine/MatchSession.cs']
verification=['tests/Riftbound.ConformanceTests/DuskRoseLabControllerYouTests.cs','tests/Riftbound.ConformanceTests/DuskRoseLabStartTests.cs','tests/Riftbound.ConformanceTests/GameHubJoinTests.cs','tests/Riftbound.ConformanceTests/FullGameEndToEndTests.cs','tests/Riftbound.PostgresAcceptance/BattlefieldStartLabYouSqlAcceptance.cs','tests/Riftbound.PostgresAcceptance/Program.cs','tests/Riftbound.PostgresAcceptance/SqlAcceptanceCases.json','scripts/tests/test_sql_ci_gate.py']
m['reviewContexts'][identity]={'evidenceCommit':start['baseCommit'],'currentImplementationCommit':'7a2fa5b1bfc2551466fc7464dc579f4baf807357','currentImplementationIncludesUncommittedChanges':True,'ciState':'PENDING','ciReceipts':[],'region':'CN','language':'zh-CN','applicableAsOf':'2026-10-22','adoptedCoreEffectiveDate':'2026-10-16','sourceManifest':sp,'implementation':[pin(root/p,'implementation') for p in implementation],'verificationSources':[pin(root/p,'verification-source') for p in verification],'branches':branches,'evidence':[pin(ev/name,role) for name,role in [('final-original-test-and-input-retention.json','focus-check-and-retention'),('independent-current-real-sql-history-and-refusals.json','independent-sql-history-refusals'),('original-broken-fixed3f2dc-lab-sql-cold-refusal.json','original-counterexample')]]}
reviews['reviews'].append({'id':identity,'scope':source_manifest['scope'],'subjects':['UNL-209/219'],'ruleRecords':rule_refs,'status':'BOUNDED_CURRENT_CN_LAB_YOU_EXISTING_RULES_DEDICATED_EXECUTION_AND_RECORDED_SCENE_REPAIR_SOLE_CHECK_AND_SQL_PASS_NEW_FIXED_FULL_PRODUCTS_OPEN','sourceManifest':sp,'auditDocument':document,'validation':{'newCases':25,'newMethods':3,'finalAdjacentCases':392,'finalAdjacentMethods':285,'affectedOriginalFullGameMethods':2,'onlyCheckCases':13436,'onlyCheckMethods':7543,'originalOnlyCheckOutcomes':{'Passed':13436},'sqlCase59OnlySixNewControlledBranches':True,'sqlInputs':32,'sqlColdRecoveries':32,'independentAuthorityHistories':32,'independentPrivateSnapshots':58,'independentPrivatePrompts':58,'independentEvents':91,'actualCorruptOrWrongVersionRefusals':4,'wholePrintingCertified':False},'exclusions':['Specific chooser190.6.c, general last-known-information/copies and other consumers remain OPEN.','Recorded controlled scenes and prior fixed3f2dc native proof do not certify this new candidate or visible/Windows acceptance.','All source gaps, outside-active705, eight English gap,139news,KDA exact identities and whole1740 universe remain.','Original first compile failure,16pass9failure,three-input missing-field SQL room and all historical failed CIs remain unchanged.']})
assert reviews['reviews'][:164]==oldreviews['reviews'] and m['acceptanceGates']==oldm['acceptanceGates'] and m['laterSourceDeltas']==oldm['laterSourceDeltas'] and m['certificationScope']==oldm['certificationScope']
assert (len(reviews['reviews']),len(m['reviewContexts']),len(m['acceptanceGates']))==(165,80,250)
rp.write_text(json.dumps(reviews,ensure_ascii=False,indent=2)+'\n');m['inputs']['reviews']['sha256']=sha(rp)
m['currentLabStartYouInputChange']={'sourceManifest':sp,'auditDocument':document,'issue':'https://github.com/DDDHL0704/riftbound/issues/88','onlyNewBoundedBranches':25,'all164OldReviews250GatesAndSevenDeltasExact':True,'all79OldCompleteContextsArchived34CurrentImplementationRefreshes45WholeObjectsExact':True,'original17156RemainingIdsMustRemain':True,'expectedRemainingObligations':17181,'newFixedFullAndCurrentProducts':'OPEN','wholePrintingOrGoalComplete':False}
m['precedingFixed4014ActualCompleteFullCiReadback']=pin(ev/'preceding-fixed4014-complete-full.json')
m['precedingFixed3f2dcActualCompleteFullCiReadback']=pin(ev/'preceding-fixed3f2dc-complete-full.json')
m['precedingFixed3f2dcBoundedNativeMainFrostAndHistoryReplay']=pin(ev/'preceding-fixed3f2dc-native-main-frost-and-history-closure.json')
m['currentNineLocaleGalleryExistingIdentitySourceReadback']={'source':pin(ev/'current-nine-locale-gallery-source-readback.json'),'existingKaynDisposition':pin(ev/'existing-regional-kayn-and-eight-gap-disposition.json'),'allExistingGateObjectsSourceUniverseAndDatesRetained':True,'noEightGapClosureAndNoNewRegionalLegalityOrPrintingLanguageInference':True,'currentCnComplete1504':pin(ev/'current-complete-cn1504-readback.json')}
mp.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n')
write(ev/'publication-original-governance-object-retention.json',{'atUtc':now(),'oldReviews':164,'oldContexts':79,'oldGates':250,'oldDeltas':7,'oldRemainingIds':17156,'newBranches':25,'expectedRemainingObligations':17181,'explicitImplementationPinRefreshedContexts':34,'otherCompleteContextsExact':45,'allOldReviewsGatesDeltasAndCertificationScopeExact':True,'allOldCompleteContextsSavedBeforeRefresh':True,'originalVerificationSourceReferencesExact':True,'baselineGovernance':pin(baseline),'semanticCoverageCertified':False})
text='''# 暮色玫瑰实验室“你”与原始日志的限定审查

本批CN/zh-CN、适用2026-10-22，核心10-16生效。精确API34762、UNL-209/219和当前十条原文块见[来源manifest](evidence/lab-start-you-current-cn16-2026-10-22/source-manifest.json)。官方完整卡行、QA和16份已知文档与已登记版本一致，13次304、三份等值正文不另存PDF；原取得、发布和生效日期保留，未把抓取日期作为卡牌生效日。Goal active / NOT READY。

既有共享逻辑已限制“你的开始”：未受控制或控制者不是回合玩家时不触发；物理拥有者不决定技能的“你”。本批专门执行八个资格分支、八个实际控制者/拥有者/支付或拒绝分支和九个原技能/事件/历史伪造分支，共25例/3方法。选择摧毁是技能前置指令费用，实际技能控制者选择并支付后才进入后续响应，原独立出生事件及严格原始指令回放检查保留。指定选择者190.6.c、通用LKI、复制、其他消费者和整卡仍OPEN。

[#88](https://github.com/DDDHL0704/riftbound/issues/88)为既有开发场景缺陷：原场景的场外单位引用不存在的P2-OTHER战场，真实数据库写入三条原指令后冷恢复拒绝。原固定3f2dc执行文件、三条原指令、七表和拒绝原文全部保留。只补该场景实际物理战场及其区域/位置，原场地顺序保留；Hub只在原精确断言中追加此场地。未修改普通对局规则或迁移、重写旧局。

最初编译的局部标识符与C#查询关键字冲突，原源码及日志保留；改名后首轮25例实际16过9失败，九例均在原场景缺场地的正向前提被阻断，未把它们算成九个生产语义错误。补场地后84/26、最终392/285定向全过，含全部Hub、相关战场与原生相邻，以及两条原完整胜利/回放。74个独立工具测试在唯一check前全过。唯一check13436/7543全部通过零跳过，原13411/7540身份多重集合完整，新增25/3；3342源码/模式、1419Conformance和23原生Debug文件全程稳定。

原1802份Conformance文件中1801份逐字不变，仅Hub场地断言做上述追加；全部旧方法、Theory、断言保留，九份原完整源码逐字不变。143公共完整方法、110配对、十一牌组、种子424242与胜利/回放断言完整保留。check不代表执行了全部完整对局。测试期间另一个任务提交7a2fa5b网页客户端，全部20项在clients/web；原3342测试输入未变化，新网页及其并行未提交改动不冒称本轮check认证，未被本轮改动或暂存。

SQL清单只追加第59身份，原58对象、类与顺序保留。六新受控分支32原指令/32冷恢复，四实际篡改或错版拒绝；独立读者逐一核对32原始历史、58私有快照、58提示、91事件、原EMPTY/私有种子与完整原指令回放及七表正常/故意损坏状态。包括伪造控制者及投影、连原事件一起伪造、缺日志和自洽错实现身份。原345审计76原生七表均保持，专用库正常关闭；这些场景不充当普通完整对局。

此前4014固定CI实际11项成功，独立13528/7675、57SQL；3f2dc固定CI39700268504实际11项成功，独立13560/7681、58SQL、原13528/7675全留加32/6。两次143完整方法/151例、110配对/十一牌组/424242均完整，原15网络拒绝零泄露及双方无请求/无重试刷新到tick2通过。原9633整次13失败及N41唯一check的P79失败按原范围留存，不回填绿灯。本批新固定full仍独立待核。

3f2dc实际CI默认Main双端同进程冷重连，三条生产交互后在原同局完成正常8:0，共71命令389事件；独立71历史/137快照/137提示及完整原回放通过。首轮实际抽到Court ofUnity而助手只认另一战场，在42命令178事件停止的原失败保留；按该真实战场执行必要12次驻守响应，原开局、17个正常动作和三条原生输入均未重提。另受控冰霜同原四指令局修正验证器的来源命令标签读取后，默认Main两端实际响应，最终6指令23事件及独立6/11/11通过。两网络局各自完整历史保留。锁定边界按实际观察记录：首轮进程开始/结束观察为锁定，后续步骤根锁为false且会话值缺失，未声称全程锁定或可见/硬件/Windows通过；更不覆盖本批新候选。

官网九个语言视图完整窗口原响应重新取得，语言视图不等同印次语言或地区合法性。EN/KO返回1408而报1416；ES/DE/FR/IT/JA返回1409而报1417；ZH-CN/TW返回1408而报1415。英文1408完整行与最新已登记版本一致。额外ogn-189-000是既有#66凯隐身份冲突，五语言完整行与ogn-189-298共享图片对象及规则文本；旧两个门禁对象原样，印刷189/298是已有像素证据，本轮未冒称新看卡面。并集1409不替代1740总印次，八英文缺口未闭合。CN1504全行、字段、null与QA亦与最新登记一致，不改目录或新增有效日期推断。

原164审查、250门禁、七源增量与17156义务身份全部保留，79完整旧上下文已归档；34项只刷新当前MatchSession实现绑定并置新full待核，45项整对象原样，旧verificationSources引用不改。新增25限定分支至17181，165审查80上下文，semanticCoverageCertified=false。全1740/14293、目录外705、英文八缺口、139中文新闻及K/DA精确身份门禁不缩减。只留最新可用3f2dc A/B及每平台一个安装包；原始CI验证ZIP只留一套，不重复装包，已核验临时SQL解压文件按精确清单退役。新固定full、当前产品、可见双端、Windows实机和全量目标仍各自OPEN。
'''
(root/document).write_text(text)
status='2026-10-22 本批：[暮色玫瑰实验室“你”与原始日志](LAB_START_YOU_CURRENT_CN16_AUDIT_2026-10-22.md)。CN当前16文档与34762完整卡行/QA未换版；25/3专门执行原有“你的开始”资格、控制者/物理拥有者、支付/拒绝及原日志伪造分支。旧开发场景缺P2-OTHER的三指令真实SQL冷拒绝原局完整保留，#88追踪；只补该场景实际场地，原Hub断言追加一项，不改普通规则。\n\n首次编译与16过9失败原件保留；最终392/285定向含全部Hub及两原完整对局，74工具先于唯一check全过。唯一check13436/7543全过零跳过，原13411/7540全留；3342/1419/23输入稳定。原1802文件仅一Hub精确断言追加、其余1801及九原完整源码逐字不变，143/110/11/424242与Theory和断言完整。SQL原58身份顺序/类保留只加59，六分支32指令32冷，独立32历史58快照58提示91事件/四实际拒绝通过，原345审计76原生七表保持。并行提交7a2fa5b及未提交网页改动保留，未冒称属于此3342规则check认证。\n\n前4014完整CI独立13528/7675、57SQL；3f2dc完整CI39700268504实际11项success，独立13560/7681、58SQL、143完整151例/110配对/11牌组/424242及网络15拒绝0泄露和双方tick2推送通过。3f2dc CI默认Main双端冷重连、正常8:0共71/389及独立71/137/137原历史，受控冰霜6/23及独立6/11/11通过；实际随机Court ofUnity、必要驻守响应、两原助手失败和所有原指令保留。它们不覆盖本批新候选或可见/Windows。\n\n九语言图库原响应和CN1504全行/QA复核，英文1408未变、八差额未闭合；额外凯隐属既有#66和两个原门禁，不新增或合并印次。原164审查/250门禁/七增量/17156义务完整，79旧上下文归档、34当前实现刷新待新full/45整对象原样，旧验证引用保留；新增25至17181、165审查80上下文，语义false。全1740/14293/705/八英文/139新闻/KDA范围保留；仅最新双端和每平台一包，新固定full、当前产品、可见双端、Windows及全量目标OPEN。Goal active / NOT READY。\n\n以下原批按原范围与当时终态保留。\n\n'
status_path=root/'docs/PROJECT_STATUS.md';old_status=gzip.decompress((prior/'PROJECT_STATUS.md.gz').read_bytes()).decode();assert old_status.startswith('# 当前项目状态\n\n')
status_path.write_text('# 当前项目状态\n\n'+status+old_status[len('# 当前项目状态\n\n'):])
print('PUBLISHED N42 bounded25;165reviews80contexts250gates7deltas;34explicitimplementationpins45wholecontexts;17156oldIDs+25expected17181;independentgovernance pending',flush=True)
