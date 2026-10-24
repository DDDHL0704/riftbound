# 普通入场技能当前私有来源 · 2026-10-24

[Issue168](https://github.com/DDDHL0704/riftbound/issues/168)。本包修复已公开入场技能的当前来源身份泄漏；技能原印次、效果、控制者和playAbility保留。新VEN登记0、整卡认证增量0，backend NOT READY。

## 现象与范围

当前main `bb8b7a17a72f446c40da1bad9f5651588ec86e0c` 的既有 `OGN·087/298` 即可复现：明确中盘手牌初态，实际打出并保留抽牌技能，再以 `OGN·104/298` 返回来源；对手和旁观者的技能sourceObjectId仍指向新的私有区域物体。三卡独立迁移源104e4f600原58例为51P/7F/0skip，原Basic/Projection文件逐字保留；该旧牌反例证明问题不依赖新VEN登记。失败停在接受RETURN结算后的隐藏扫描，不称失败步骤已通过冷恢复。

生产变化限于3个Engine文件：BuildStackItemSnapshotView对普通SourceConfirmed技能调用既有可见性规则；CapturedBattleSourceIsPrivate对应当前私有源；新增独立权威区域/观察权限/公开技能字段核对。缺失/null技能列表形成错误且不进入会空引用的后续栈校验。既有战斗、德莱文条件保留；同接点PR146的EmpowerActivation条件同步保留，未移入VEN032注册。无新伤害/战力算法、历史回填或旧版本迁移。

测试复用VenBasicFlow.Driver，只将类由private改internal。没有引入三卡目录、注册或旧累计分支。普通源独立校验取自4c677的独立版本；较新旧分支的HasHistoricalPublicDamageSource属于未合入的其他机制，未导入本包。

## 固定输入验证

测试源 `fd01bb30d7f0b1d5d2fce17f5ff5b1e90c7894bd`，后续ready头只增加本文。0编译警告/错误，`-m:1`及测试单线程。

- 新12/12 + 相邻455/455 = **467/467，零跳过**。相邻为8个相关类及250个恢复方法，精确L3合法开局/冷日志验收清单排除；不是本机full/SQL/网络。
- 两条现有OGN087普通流程涵盖双方，12条接受命令、34事件；每步正常冷恢复/接受重复和完整原日志回放。当前私有检查点与末态共4个JSON恢复帧；18个玩家字段/缺失/重复行篡改、2个旁观者及2个原事件篡改均拒绝。
- 8个区域与2个私有面孔用例是有限投影帧，不称真实移动历史。公开区域保留来源、私有区域按现有观测权限匿名；公开技能仍固定原牌面。
- 生产恢复store先运行MatchRecoveryValidator并传递实际错误报告，Restore消费该报告、从权威状态重新构造服务视图。原新增测试误要求清空报告后Restore仍自行检查存储视图，原失败保留；现同时断言实际错误报告拒绝，以及人为清空可信报告时重建的双方服务视图仍与规范视图一致，不返回伪造存储视图。未修改Restore逻辑。
- 反向源 `52bde259f8446d1c6e7f1535e3784f6a729c794c` 保留强化来源条件和null形状保护，只去掉普通来源匿名与对应独立校验。同12例为 **4公开控制P / 8私有预期F**；两条最后接受返回在隐藏扫描处失败，负控导出的未完成日志不计为冷恢复绿灯。该私有源不是交付祖先。

实际RIF `9ae06e113d922bb84ced1f8da62602a43881832d6970a92b1c307ddbb34bd878`；Engine SHA256 `f0f6fb6358def741bd474b91010fcad3a02be3edd48d9375bd52a4d94598a507`，MVID `02df8f2b-113d-4e7e-9bb6-ac6203687d96`。源码输入2489、运行文件1418前后逐项哈希一致；8个主/反向真实加载模块条目分别绑定自身冻结运行目录。六个原未跟踪文件哈希不变。

## 原件

工作树 `/Users/dinghaolin/MyProjects/.worktrees/riftbound-cards-20261023` 的 ignored 根 `var/qa/cards-direct-pr-migration-20261024/ordinary-play-source-privacy-v1/`：

- `source-runtime-proof-v1.json`，SHA256 `cbbe61cf7189ef6df3850533135346c6e5737a34c1f07f15571cb21573108f8e`；`verify-final-v1.py`机械核对已通过。
- `compile-inputs-v5.json`、`runtime-manifest-v5.json`、`focus-v5/ordinary-source.trx`、`adjacent-v5/adjacent.trx`、`focus-v5/ordinary-evidence/`。
- `counter-v5/`保留源、运行件、TRX与失败步骤实录。v1旧未合入接口编译失败、v2恢复职责错误假设、v3负例类型注解编译失败、v4null栈空引用全部原件/readback保留。
- 原三卡58例源/bin/失败在同级 `unit-entry-main-port-v1/`，本包不以旧648例或该负控代替当前绿灯。

实际L2须核对最新PR head/base/merge-result SHA、quality/targeted/rule_suite产物和零跳过后方可合入；本机结果属于L1。三卡消费者作为后续独立包显式依赖此修复，适用来源/全规则/L3与整卡认证仍开放。
