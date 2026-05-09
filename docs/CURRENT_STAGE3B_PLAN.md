# 阶段 3B 计划：Battlefield / Standby / Control / Conquer lifecycle + Central cleanup queue 最小官方化切片

日期：2026-05-09
当前 HEAD：`3d70ef0`
结论：**NOT READY**

本文是 D 文档 / 规则证据 / P0-P1 审计 agent 的阶段 3B 当前口径。阶段 3A 已完成 smoke 基线、复杂命令强类型映射、`PAY_COST` 最小 runtime 与对战桌面外壳，但这些关闭项不等于 Stage 3 或产品 READY。

## 1. 3B 范围

3B 只收口：

- Battlefield state 最小权威模型：战场对象、占据者、控制者、争夺状态、待命公开/隐藏视图、最近战场结果。
- Standby 最小官方化：待命子区域、失控待命在 cleanup 中移除、隐藏信息不泄漏、可展示但不由前端裁决。
- Control / held / conquer 最小切片：代表性战后控制结算、据守/征服事件和 snapshot 结果可重连展示。
- Central cleanup queue 最小切片：`PendingTaskQueue` / `PendingCleanupTasks` 表达 state-based cleanup、blocking guard、active task、非法待命/致命伤害/未贴附装备等代表任务。

3B 明确不进入：

- 最终正式 18 步 E2E。
- 1009 张卡 full-official 实现。
- 完整 battle / spell duel lifecycle。
- 完整 `ASSIGN_COMBAT_DAMAGE` runtime。
- 完整 `ORDER_TRIGGERS` runtime。
- 完整 PaymentEngine、LayerEngine、全路径 replay/determinism。

## 2. 规则证据链

| 规则域 | 规则 / FAQ 入口 | 当前实现状态 | 归属 agent | 3B 下一步 |
| --- | --- | --- | --- | --- |
| Battlefield / standby zone | `CORE-260330` rules 107.2-107.3；rules 315.2.b.2、319-323 | `BattlefieldStates`、具体战场移动、`standbyObjectIds`、`faceDownStandbyCount`、snapshot 展示已有；`MatchStateExposesAuthoritativeBattlefieldAndCleanupTaskViews`、`SnapshotsExposeBattlefieldControlOccupantsAndStandbyState` 提供代表证据 | B 主实现；C 展示；D 审计 | 固化 battlefield/standby snapshot 字段含义，记录待命隐藏信息和失控待命 cleanup 的证据 |
| Control / contest / freeze | `CORE-260330` rules 187-189、344-348、461-464；`JFAQ-251023` q4.1-q5.4；`SOUL-OFAQ-260114` p21 | 战后控制者写回、`BATTLEFIELD_CONTROL_RESOLVED`、`BattlefieldResolutions` 和最近 battlefield contest smoke 已有；完整控制权冻结/释放仍未证明 | B 主实现；E fixture；D 审计 | 3B 可关闭代表战后控制与重连展示；完整 freeze/release 留 P0 |
| Held / conquer scoring | `CORE-260330` rules 315.2.b.2、461-464；`SOUL-JFAQ-260114` p4-p5 | `BATTLEFIELD_HELD` / `BATTLEFIELD_CONQUERED` 大量代表路径、`P79BattlefieldHeldDrawsCardFromBattlefieldObject`、`P79BattlefieldConquerMillsTopTwoFromBattlefieldObject`、Hub 代表种子已有 | B 主实现；E 选代表战场卡；D 审计 | 3B 只收一条 held 与一条 conquer 结果链；全战场卡、替代得分和复杂支付触发留后续 |
| Central cleanup queue | `CORE-260330` rules 319-324；`JFAQ-251023` q5.1-q5.2；`SOUL-OFAQ-260114` p19-p20 | `PendingTaskQueue`、`PendingCleanupTasks`、`RunStateBasedCleanupLoop`、blocking guard、非法待命/致命伤害/未贴附装备代表任务已有；`PendingTaskQueueUsesSpellDuelTaskAsActiveWhileContestDuelIsOpen` 与 `PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses` 证明 task view 入口 | B 主实现；C 只读显示；D 审计 | 3B 可关闭最小 queue view 与代表 cleanup；所有状态变化统一 enqueue 仍留 P0 |
| Frontend shell safety | 服务端权威原则；`CORE-260330` rules 107-129；`SnapshotDto.timing.pendingTaskQueue` / `lanes.battlefields` | DevUi 已展示 battlefield、standby、pending task、battlefield/battle resolution 摘要；字段仍偏 `Record<string, unknown>` | C 主展示；B 提供字段；D 审计 | 前端只能显示服务端 task/result，不本地计算控制、待命合法性、得分、胜负 |

## 3. 3B 关闭候选 P0 子项

这些是 3B 可以尝试关闭的最小子项；D/A 复核前不能从总 P0 清单移除。

| 候选 | 可关闭子项 | 当前证据入口 | 仍不可关闭内容 | 归属 agent |
| --- | --- | --- | --- | --- |
| 3B-CAND-001 | battlefield/standby snapshot 作为前端只读展示字段 | `BattlefieldStates`、`MatchStateExposesAuthoritativeBattlefieldAndCleanupTaskViews`、`SnapshotsExposeBattlefieldControlOccupantsAndStandbyState` | 完整 battlefield task lifecycle、所有战场卡效果 | B/C/D |
| 3B-CAND-002 | 非法待命代表 cleanup task 与 blocking guard | `PendingTaskQueueExposesIllegalStandbyCleanupAsStateBasedTask`；battlefield contest smoke 中 `standbyObjectIds=[]` 且非法待命入墓 | 所有失控待命时机、控制权冻结期间不得移除、所有 standby 卡 | B/E/D |
| 3B-CAND-003 | 战后 control / held / conquer 代表结果可 snapshot/reconnect | `CoreRuleEngineChangesBattlefieldControllerAfterBattle`、`P79BattlefieldHeldDrawsCardFromBattlefieldObject`、`P79BattlefieldConquerMillsTopTwoFromBattlefieldObject`、GameHub held/conquer seed | 全量战场卡、替代得分、支付/拒付得分触发、复杂控制权冻结 | B/E/D |
| 3B-CAND-004 | central cleanup queue 的最小 task view | `PendingTaskQueue` phase/activeTaskId/isBlocking/tasks；spell-duel/battle active task 代表测试 | 所有 command/stack/trigger/move/enter/leave/damage/power change 统一 enqueue 与 repeat-until-stable completion audit | B/C/D |

## 4. 3B 仍存在 P0

| P0 | 当前状态 | 可在 3B 内继续切片 | 留到 3C / 最终 |
| --- | --- | --- | --- |
| 3B-P0-001 cleanup queue 全触发面 | 代表 state-based cleanup 和 task view 已有，但不是所有状态变化都统一进入 queue | command/stack resolve/move/damage/power change 的最小统一入口与回归矩阵 | 全命令 property、替代/预防/LayerEngine 依赖、全随机恢复 |
| 3B-P0-002 control freeze/release | 战后控制代表路径已有；FAQ 要求战斗/法术对决期间冻结控制，关闭后再释放 | 选一条战斗或非战斗法术对决冻结/释放 fixture | 完整 spell duel / battle lifecycle 与所有控制权改变卡 |
| 3B-P0-003 delayed illegal standby removal | 非法待命 cleanup 代表路径已有 | 验证失控待命在“下一次 cleanup”移除，并在 freeze 期间不提前移除 | 所有 standby 容量、隐藏待命、即时贴附/翻开/反应族 |
| 3B-P0-004 held/conquer scoring order | 大量 held/conquer 代表效果已有 | 固化一条 held、一条 conquer 的事件顺序、得分/触发、snapshot/reconnect | 全战场卡、得分替代、付费触发拒付、同时触发排序 |
| 3B-P0-005 3B smoke evidence | 已有后台 headless Chrome/CDP battlefield contest smoke 记录 | 把命令、房间号、断言点、reconnect、隐藏信息红线归档到 3B 证据位 | 正式 18 步 E2E |

## 5. 3B P1 / 文档风险

- `CURRENT_STAGE3_CORE_FLOW_AUDIT.md` 仍是宽阶段 3 总图，若未读本文可能误把 3A OPEN 表述当作当前状态；当前 3B 以本文和 `CURRENT_RULE_EVIDENCE_TODO.md` 为准。
- `CURRENT_FRONTEND_CONTRACT_GAPS.md` 中 cleanup/battlefield 字段仍偏 DevUi，正式 DTO 还没冻结。
- 多数 held/conquer 证据来自代表 card fixtures，不能自动外推到 1009 full-official。
- 后台 headless Chrome/CDP smoke 不是最终用户验收级 18 步 E2E。

## 6. B/C 完成后的证据位

| 证据位 | 交付对象 | D 需要记录 | 状态 |
| --- | --- | --- | --- |
| 3B-EVID-B-BATTLEFIELD | battlefield/standby/control 最小 task | diff/commit、规则依据、测试命令、snapshot 字段、仍缺 freeze/release | OPEN |
| 3B-EVID-B-CLEANUP | central cleanup queue 最小切片 | task kind、reason、activeTaskId、blocking guard、零副作用/重复清理语义 | OPEN |
| 3B-EVID-B-HELD-CONQUER | held/conquer 代表链 | battlefield card、事件顺序、得分/触发、snapshot/reconnect、全量缺口 | OPEN |
| 3B-EVID-C-SHELL | 前端只读展示 | 页面/组件、只读字段、无本地裁决、隐藏信息断言、reload/reconnect | OPEN |

## 7. checkpoint 建议摘要

阶段 3B 当前只收口 battlefield/standby/control/conquer lifecycle 与 central cleanup queue 的最小官方化切片。3A 子项已关闭但项目仍 **NOT READY**。3B 可尝试关闭 battlefield/standby snapshot、非法待命 cleanup 代表路径、战后 control/held/conquer 代表结果、central cleanup task view 四个子项；完整 control freeze/release、全触发面 cleanup queue、全战场卡 held/conquer、完整 battle/spellDuel、damage assignment、ORDER_TRIGGERS、PaymentEngine、LayerEngine、1009 全量和最终 18 步 E2E 仍为 P0/P1 或后续范围。
