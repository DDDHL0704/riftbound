# 阶段 3C 证据：Spell duel / Battle / ASSIGN_COMBAT_DAMAGE / Battle cleanup

日期：2026-05-09
当前 HEAD / 3B checkpoint：`a74beac`
结论：**NOT READY**

本文是 D 文档 / 规则证据 / P0-P1 审计 agent 的阶段 3C 当前口径。3C 只建立并维护 spell duel、battle、`ASSIGN_COMBAT_DAMAGE` 与 battle cleanup 的规则证据链和关闭候选，不启动最终 18 步 E2E，不进入 1009 张卡全量实现，不标记 READY。

## 1. 3C 范围

3C 只收口：

- Spell duel lifecycle：普通 / 法术对决、开环 / 闭环、焦点、让过、反应窗口、关闭后回到正确任务。
- Battle lifecycle：战斗任务、参战单位、战斗前法术对决、战斗响应、伤害分配、战斗结果、战后清理。
- `ASSIGN_COMBAT_DAMAGE` runtime：从现有 schema/shell 推进到服务端 prompt、合法分配约束、提交校验与零副作用失败。
- Battle cleanup：战斗伤害造成后，攻防身份、致命伤害、战斗结果、控制/待命/战场结果与 cleanup queue 的衔接。

3C 不进入：

- 最终正式 18 步 E2E。
- 1009 张卡 full-official 覆盖。
- 完整 `ORDER_TRIGGERS` runtime。
- 完整 PaymentEngine / `DECLINE_PAY_COST`。
- 完整 LayerEngine、全路径 replay/determinism。
- 3B battlefield / standby / control / conquer 全量收口；3C 只消费其最小 task/view 作为前置。

## 2. 当前真实协议 / 状态字段

| 域 | 当前真实字段 | 3C 审计口径 |
| --- | --- | --- |
| `SpellDuelState` / `timing.spellDuel` | `isActive`、`isClosed`、`spellDuelId`、`battlefieldObjectId`、`focusPlayerId`、`passedFocusPlayerIds`、`stackItemIds`、`stackControllerIds` | 可支撑焦点和栈上下文展示；不等于完整 lifecycle。 |
| `BattleState` / `timing.battle` | `isActive`、`battleId`、`battlefieldObjectId`、`attackerObjectIds`、`defenderObjectIds`、`participantControllerIds`、`damageAssignment` | 3C 已可表达最小伤害分配窗口；仍不是完整 battle task journal。 |
| `BattleResolutionState` / `timing.battleResolutions` | `resolutionId`、`tick`、`kind`、`reason`、`battlefieldId`、`attackingPlayerId`、`defendingPlayerId`、`winnerPlayerId`、attackers/defenders/survivors/destroyed、`relatedEventKinds` | 可支撑重连后展示最近战斗结果；不能作为完整 battle task journal。 |
| `AssignCombatDamageCommand` | `battleId`、`battlefieldId`、`assignments[].sourceObjectId/targetObjectId/damage` | 3C 已接入最小 runtime prompt / validation / simultaneous commit；完整全规则分配矩阵仍缺。 |
| `ActionPromptContracts.AssignCombatDamage` | required payload、`assignmentChoices`、`legalTargets`、`battleParticipants`、validation errors、visible/hidden metadata 名称 | 3C 已填充最小 runtime choices/constraints；完整 barrier/back-row/exclusive constraints 仍缺。 |

## 3. 规则证据链

| 规则域 | 规则 / FAQ 入口 | 当前实现状态 | 归属 agent | 3C 下一步 |
| --- | --- | --- | --- | --- |
| Spell duel lifecycle | `CORE-260330` rules 307-313、333-348；`JFAQ-251023` q3.1-q3.3 | `SpellDuelState`、`SPELL_DUEL_FOCUS`、`PASS_FOCUS`、焦点恢复、swift/反应 timing context 有代表证据；3C 新增 close -> damage assignment 连续测试 | B 主实现；C 展示；E fixture；D 审计 | 完整 `SPELL_DUEL_ACTION` 专用 payload、所有反应/迅捷/反制链仍留后续 |
| Battle lifecycle | `CORE-260330` rules 454-461；rules 319-324；`JFAQ-251023` q2.3-q2.4 | `DECLARE_BATTLE` 代表路径、`BattleState`、`BattleResolutionState`、多防守/多参与者若干代表测试已有；仍偏同步结算片段，不是完整 task | B 主实现；E battle fixture；C 只读展示；D 审计 | 把 START_BATTLE -> battle view -> result/cleanup 串成可审计最小 task |
| Damage assignment | `CORE-260330` rules 142-143、417、460；`JFAQ-251023` q6.1-q6.4；`SOUL-OFAQ-260114` p19-p20 | 3C 已开放最小 runtime prompt、damagePool / legalTargets / existingDamage / lethalDamageThreshold / requiredAssignments，覆盖合法提交、非法提交、stale prompt 和零副作用拒绝 | B 主实现；E 多单位/壁垒/后排/负战力 fixture；C runtime UI；D 审计 | 完整壁垒/后排/同优先级/负战力/不可分配全矩阵仍留后续 |
| Battle cleanup | `CORE-260330` rules 319-324、461-464；`JFAQ-251023` q5.1-q5.2 | 3C 最小路径已覆盖 simultaneous damage、致命单位 cleanup、battle close、battlefield control update | B 主实现；E battle cleanup fixture；C 只读展示；D 审计 | control freeze/release、替代/预防、LayerEngine 与 cleanup queue 全触发面仍缺 |

## 4. 3C 关闭候选 P0 子项

| 候选 | 可关闭子项 | 当前证据入口 | 仍不可关闭内容 | 归属 agent |
| --- | --- | --- | --- | --- |
| 3C-CAND-001 | spell duel focus/pass/close 的最小 lifecycle 证据 | 已用 `rg` 确认存在的 `p2-preflight-spell-duel-pass-focus-closes-window`、`SpellDuelState`、`PASS_FOCUS` prompt、timing context 代表测试；3C 新增 `SpellDuelPassCloseEntersDamageAssignmentThenBattleCleanupUpdatesControl` | 完整 `SPELL_DUEL_ACTION` payload、所有反应/迅捷/反制链、战斗初始栈触发排序 | B/C/E/D |
| 3C-CAND-002 | battle view / battle resolution 的最小 task 证据 | `BattleState`、`BattleResolutionState`、`BATTLE_DECLARED`、`DAMAGE_APPLIED`、battleResolutions snapshot | 完整 battle task、响应窗口、所有多攻防组合、全战场结果联动 | B/C/E/D |
| 3C-CAND-003 | `ASSIGN_COMBAT_DAMAGE` runtime 最小 prompt | 3C 已提供 runtime prompt metadata、合法/非法/stale 测试、simultaneous damage commit | 壁垒/后排全族、同优先级复杂选择、所有负战力/无法伤害边界 | B/E/D |
| 3C-CAND-004 | battle cleanup 最小结果链 | 3C 已覆盖 battle damage -> lethal cleanup -> battle close -> battlefield control update | cleanup queue 全触发面、控制冻结/释放全路径、替代/预防/LayerEngine | B/E/D |

## 5. 3C 仍存在 P0/P1

| P0/P1 | 当前状态 | 可在 3C 内继续切片 | 留到后续 / 最终 |
| --- | --- | --- | --- |
| 3C-P0-001 spell duel 完整 lifecycle | focus/pass、timing context 和 close -> damage assignment 代表证据已有 | 3C 最小链已收口 | 全反应链、复杂 `SPELL_DUEL_ACTION`、触发排序 |
| 3C-P0-002 battle 完整 lifecycle | `DECLARE_BATTLE` 代表路径和 battle view 有 | 最小 START_BATTLE / result / cleanup 证据 | 完整 battle task、所有多攻防、战斗响应窗口 |
| 3C-P0-003 `ASSIGN_COMBAT_DAMAGE` runtime | 3C 最小 runtime prompt / submit / reject / zero-side-effect / simultaneous commit 已有 | 3C 最小切片已收口 | 全壁垒/后排/互斥/负战力/无法承受伤害矩阵 |
| 3C-P0-004 battle cleanup | 3C 已有致命伤害、battle close、control update 代表路径 | 3C 最小结果链已收口 | 替代/预防/LayerEngine、control freeze/release 全路径 |
| 3C-P0-005 3C smoke/evidence | 可能使用后台 smoke 或 focused tests | 记录命令、房间、断言、reconnect 与 hidden-info | 最终正式 18 步 E2E |
| 3C-P1-001 前端 DTO 稳定性 | `timing.spellDuel/battle/battleResolutions` 仍是 dictionary view | 列出正式字段草案 | 独立 typed DTO / 正式 UI 设计 |

## 6. 前端红线

- 前端不得按战力、本地关键词或卡面文本裁决伤害分配。
- 前端不得自行从 `battleResolutions` 或事件日志推导 winner、控制权或下一窗口；必须等待服务端 snapshot/prompt。
- `ASSIGN_COMBAT_DAMAGE` 专用交互只能在服务端 runtime prompt 明确下发 `assignmentChoices` / constraints 后开放。
- 任何 battle cleanup 中需要玩家选择的步骤，必须转为正式 prompt；不能由 task queue 文案生成 command。

## 7. checkpoint 建议摘要

阶段 3C 当前已收口 spell duel / battle / `ASSIGN_COMBAT_DAMAGE` / battle cleanup 的最小官方化候选。已有 `SpellDuelState`、`BattleState.damageAssignment`、`BattleResolutionState`、`ASSIGN_COMBAT_DAMAGE` runtime prompt / validation / simultaneous commit / battle cleanup 代表路径；仍未关闭完整 spell duel lifecycle、完整 battle task、完整全规则 damage assignment 矩阵、battle cleanup 全路径、最终 18 步 E2E、1009 全量、PaymentEngine、ORDER_TRIGGERS、LayerEngine 与 READY。
