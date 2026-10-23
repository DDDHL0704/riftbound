# N54 固定群伤实现：独立协议与行为验收

中国区简体中文，2026-10-23。主窗口提供六个共享源的固定 N54 v1 overlay；本窗口逐成员核对大小/SHA 后应用，先备份旧源，不改共享实现。overlay SHA-256 为 `2d574969320d7f035198579128291e82c5a6bde3f9e82732e02d8d60c29644ae`。主窗口负责运行时改动，本批交付独立消费者测试的协议适配、边界回归与证据。

**138 例、19 方法全部通过、0 跳过。** 其中第一组 85 例为三选择型消费者 62、原无目标群伤 12、原 Tibbers 4、原 CurrentVoidGate 7；第二组 53 例为 CurrentControllerDamageBonus 50 加 3 个既有终局/回放对局。两组使用同一 DLL 哈希，33 个指定输入在运行前后未变。

三选择型消费者保留原 32 个场景，改用明确接口：弹幕时间 `[battlefield]`；新月打击 `[battlefield, enemyUnitAtThatField]`；天声震落 `[friendlyBaseUnit, battlefield]`。原误伤、缺少加成及移动缺失战场 ID 的独立断言均通过。实际选择 A/B 的场景证明伤害只发生于所选战场；天声震落移动到同一已选战场，且位置中存在精确 ID。加伤矩阵分别验证无加成、仅安妮、仅虚空之门、两者同时存在；新月主伤与溅射各自正确叠加，安妮没有重复计算。

新增 30 个非法提交分别覆盖缺少/伪造战场、以基地或单位代替战场、多余/重复目标、P2 越权、过期 prompt/tick，以及天声的敌方基地/非基地单位、新月的跨战场/基地/友方单位。全部拒绝，权威状态、双方私有快照和双方提示不变，产生 0 事件。每个合法场景在真实出牌后重复投递同一 intent 和原始 raw command，32 次均没有增加 journal 或改变状态/双视图/提示。重复请求另行记录，未虚增原始 journal 前缀数。

合法场景在行为断言之后核对当前提示的目标数量、战场目标索引和公开单位选项。弹幕与天声可选择 A/B；新月在 B 无单位时允许提示根据关联目标过滤其战场列表，但实际提交不得跨场。此处验证的是既定协议及当前合法性，不将旧候选编码说成官方唯一编码。

原无目标群伤 12 例、Tibbers 4 例的源测试未改。第一批官方来源反例中缺失的局部伤害均转绿；安妮全局加成继续只应用一次。Tibbers 仍由真实打出进基地、普通技能结算链及两次优先权让过执行。原始来源与红例保留在先前独立提交：`e5378f1f`、`dd9b339a`、`592fef09`；不覆盖原源码快照、失败、未到达断言和旧接口观察。

78 条新导出的实际历史合计 **178 指令（148 接受、30 拒绝）、540 事件、178 原始前缀回放、32 次另记幂等重投**。其中选择型 62 条：126 指令/301 事件；无目标 12 条：40 指令/171 事件；Tibbers 4 条：12 指令/68 事件。新的正确伤害可能导致不同死亡事件，因此不强求原错误版本事件数不变；每条新历史均由同一固定输入独立重放。

本批执行的三个既有终局对局为：

- `OfficialDeckMidgameResolvesVoidGateTargetSpellSkillDamageBonusAndScoreVictoryActionLogReplaysToFinalStateHash`
- `OfficialLowCurvePostBattleScoreVictoryActionLogReplaysToFinalStateHash`
- `OfficialDecksResolveMultiDefenderBattleDamageAssignmentScoreVictoryActionLogReplaysToFinalStateHash`

这些对局验证相关全局/局部伤害与战斗终局流程未回归，不代表三张新接口均已通过各自的完整构筑对局。中局测试也不证明合法开局构筑。

本批没有重复运行主窗口负责的旧消费者 fixture 校准、check/full 或 SQL/API/进程恢复；这部分由集成窗口继续，不能由本批全绿代替。目标仍 ACTIVE，整张卡/全部适用来源/完整后端均未宣称完成。

[固定输入、来源、原运行与逐前缀清单](evidence/selected-battlefield-aoe-n54-parallel-2026-10-23/source-and-green-test-manifest.json)，[逐成员 SHA/大小验证的原件包](evidence/selected-battlefield-aoe-n54-parallel-2026-10-23/original-tests-fixed-inputs-and-sources-v1.tar.gz)。
