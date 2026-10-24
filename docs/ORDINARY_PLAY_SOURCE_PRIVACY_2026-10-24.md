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

实际RIF `9ae06e113d922bb84ced1f8da62602a43881832d6970a92b1c307ddbb34bd878`；Engine SHA256 `f0f6fb6358def741bd474b91010fcad3a02be3edd48d9375bd52a4d94598a507`，MVID `02df8f2b-113d-4e7e-9bb6-ac6203687d96`。源码输入2489、运行文件1418前后逐项哈希一致；主/反向实际加载的Engine、Catalog、Contracts及Conformance模块逐一绑定自身冻结运行目录。六个原未跟踪文件哈希不变。

## 原件

工作树 `/Users/dinghaolin/MyProjects/.worktrees/riftbound-cards-20261023` 的 ignored 根 `var/qa/cards-direct-pr-migration-20261024/ordinary-play-source-privacy-v1/`：

- `source-runtime-proof-v1.json`，SHA256 `cbbe61cf7189ef6df3850533135346c6e5737a34c1f07f15571cb21573108f8e`；`verify-final-v1.py`机械核对已通过。
- `compile-inputs-v5.json`、`runtime-manifest-v5.json`、`focus-v5/ordinary-source.trx`、`adjacent-v5/adjacent.trx`、`focus-v5/ordinary-evidence/`。
- `counter-v5/`保留源、运行件、TRX与失败步骤实录。v1旧未合入接口编译失败、v2恢复职责错误假设、v3负例类型注解编译失败、v4null栈空引用全部原件/readback保留。
- 原三卡58例源/bin/失败在同级 `unit-entry-main-port-v1/`，本包不以旧648例或该负控代替当前绿灯。

实际L2须核对最新PR head/base/merge-result SHA、quality/targeted/rule_suite产物和零跳过后方可合入；本机结果属于L1。三卡消费者作为后续独立包显式依赖此修复，适用来源/全规则/L3与整卡认证仍开放。

L2后续候选（NOT READY）：run40083957495实际受测 `62d7a93e0b0b304f71202ed21ef0788a31261f8c`，规则套件13988例有4个公开历史来源断言失败。原SourceConfirmed条件误把合法分摊伤害旧代际引用HOST隐藏。本地后续源 `29b476754c4a8ced6508428c26a556f1bab97f49` 新增独立partial helper：仅在DamageDistribution来源严格早于当前代际且现有ValidDistributionStack完整通过时保留公开历史引用；普通来源仍匿名，三处投影/独立校验条件一致，②原伤害上下文方法未修改。未改已协调的ValidateAuthoritativeState资源/空栈插入顺序。

新范围实际67例=66通过/1失败，原L2的4个公开历史失败已通过，原12个普通来源隐私用例保持通过。7份规范中局日志各5条接受命令/18事件，JSON冷恢复与原日志回放通过；原来源代际0、真实BURNOUT后手牌代际1。5种损坏上下文被拒绝；唯一失败为current-generation负例，原断言保留。2491源码/1418运行文件全量哈希稳定。新完整原件在 `var/qa/cards-direct-pr-migration-20261024/l2-failure-pr159-pr162-v1/pr170-history-v1/`；failure-source-runtime-proof-v1.json SHA256 `79a1eb056a9ffdeedefb3d98eacdc609d77feee56275ddd3856459bbfb80e6fe`。

新增实际共享缺陷 [Issue174](https://github.com/DDDHL0704/riftbound/issues/174)：将捕获来源代际改为当前手牌代际并重建协调视图，保持原命令/事件，MatchRecoveryValidator返回0错误且Restore接受；给P2的外层sourceObjectId虽为HIDDEN，内部damageDistribution.source却为HOST/1。原日志重放IsMatch=false；未验证真实Postgres/API路径，不将构造帧复现扩大为整个持久层结论。独立小型复现程序/实际输出在相邻 `pr170-current-generation-reproducer-v1/`，actual-reproducer-output.json SHA256 `a9399d6353724469503ef89cc942b4f5c702b04f3a4fd7c0b3b79793300e9272`。共享接点为CoreRuleEngine.ValidDistributionContext，交①协调归属后组合明确修复提交；未删除current-generation数据行，也不称本候选可合入。原Issue168仍OPEN，467例原结果仍只归原固定源。

Issue174组合修复：②按①分配交付明确生产提交 `c96bc5e631d27d42369b7faa4b44775fa0d0c857`，仅ValidDistributionContext一个方法4+/2−，同代际必须仍在实际公开场上且实际位置等于捕获位置，旧代际及此前绑定/印次/Origin/预算检查保留。④在吸收main `ceda49fec8bda2ace0cb9ac630496e09032dac07` 后以 `71255c048154ba2ee70019100fd9c2368bba6a1f` 原样cherry-pick该提交，未自行改②方法；该生产文件与c96完全相同，保护的PermanentPlayTargets文件与mainceda相同。

新受测源 `6b7dd678985b0a5c277c9dc8790bda3bc1dd4b2d` 编译0警告0错误，131+515=646例无交集全过、零跳过。原67用例多重集（含原current-generation失败行）、原467完整多重集、原12普通来源隐私与旧L2四条公开历史身份均保留；额外64个main124请求入口用例、60个main127可选费用/目标时机用例通过。负例新增检验实际校验报告送入Restore后应拒绝，未删除或放宽原断言。

7个新规范中局checkpoint各5接受命令/18事件，共35命令126事件，完整JSON冷恢复和原日志回放通过；6个协调负例保持各自新规范帧的原命令/事件，实际错误非空，恢复均以RECOVERY_INCONSISTENT拒绝。独立小型程序从新实际JSON重新执行Validator→Restore→served snapshot及原日志回放：规范帧0错误、恢复成功、P2合法旧HOST/0可见、回放true；当前私有HOST/1协调帧得到“invalid distributed damage ability”、恢复拒绝、原回放false。没有将旧29帧改RIF或回填旧历史；旧实际失败输出SHA保持a9399d63…e9272。仍未验证真实Postgres/API路径。

新原件 `var/qa/cards-direct-pr-migration-20261024/l2-failure-pr159-pr162-v1/pr170-c96-combined-v1/`：固定源/bin、2份实际TRX、7规范完整帧、6协调负例完整帧/恢复拒绝实录、independent-check-v1程序与实际输出、module-identities.json及verify-final-v1.py。2499源码/1418运行文件全量哈希稳定，四受查程序集InfoVersion均指向新源；Engine MVID `2123727d-34f2-4bde-9853-b1b922930063`，MVID组成的implementation/RIF独立重算吻合，RIF `b6de0a09e229a4696bfa5870aa9ceee9349809aae57b32c1f5d6d6c20069ba30`。

新source-runtime-proof-v1.json SHA256 `01ed3123ee260fb93bdd707738c21f422a372e6a2b86909b13e028e30a4ec0fb`；独立实际输出SHA256 `ddb7850a3ff14906873bbecb12d352b3ad4becf381165ec49049b7f3b5ad5df4`，机械核验已运行通过。后续head仅更新本doc；最新L2待实际验证，168/174保持开放。④组合测试不替代②PR175自己的最新L2，也不认证整体后端、SQL/网络/完整矩阵或整卡。
