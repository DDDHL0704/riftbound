# Stage 4D-02E Battle Task Advancement Audit

日期：2026-05-14
结论：**IMPLEMENTED / PROJECT NOT READY**

## Scope

本切片只处理 natural `ASSIGN_COMBAT_DAMAGE` 关闭当前 active `START_BATTLE` 后的 battlefield task advancement。

实现点：

- `CommitCombatDamageAssignments` 在完成 simultaneous damage、cleanup、battle close 与 battlefield control 后，调用既有 `AdvancePendingBattlefieldTasksAfterStateChange`。
- 若当前战斗已经关闭、没有 stack / active battle / active spell duel / state-based cleanup 阻塞，且还有未完成 spell duel 的 contested battlefield，则立即推进下一个 battlefield task。
- 复用既有 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` 事件与 `SPELL_DUEL_OPEN` prompt/snapshot metadata。

## Representative

新增 focused test：

- `BattleDamageAssignmentLifecycleTests.NaturalAssignCombatDamageAdvancesNextContestedBattlefieldTask`

该测试构造：

- `BF-DAMAGE` 与 `BF-NEXT` 同时 contested。
- `BF-DAMAGE` 已有 `BattlefieldTaskMarkers.SpellDuelCompleted("BF-DAMAGE")`，因此当前 active task 是 `START_BATTLE`。
- `BF-DAMAGE` 走 natural minimal `DECLARE_BATTLE + COMBAT_ASSIGNMENT` 并进入 `ASSIGN_COMBAT_DAMAGE`。
- legal assignment 关闭 `BF-DAMAGE` 后，服务端立即为 `BF-NEXT` 发出 `BATTLEFIELD_CONTESTED` 与 `SPELL_DUEL_STARTED`，并进入 `SPELL_DUEL_OPEN`。

## Non-Goals

- 未做 full combat rewrite。
- 未启动 LayerEngine。
- 未扩 PaymentEngine。
- 未新增卡牌 representative。
- 未修改前端或 coverage matrix。
- 未关闭 P0-004 / P0-005 / P1 / READY。

## Residual

本切片证明 post-assignment 的下一个 contested battlefield task 可以自然推进。它不声明完整官方战斗生命周期闭环；剩余 P0-004/P1 仍需后续切片继续审计。
