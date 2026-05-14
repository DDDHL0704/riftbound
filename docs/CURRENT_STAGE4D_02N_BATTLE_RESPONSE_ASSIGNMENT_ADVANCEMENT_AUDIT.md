# Stage 4D-02N Battle Response Assignment Advancement Audit

日期：2026-05-15
结论：**TEST-ONLY GUARD ACCEPTED / PROJECT NOT READY**

## Scope

本切片验证 natural battle 先经过 battle-response priority、再进入 `ASSIGN_COMBAT_DAMAGE`、最终提交合法 assignment 后，服务端仍会推进下一处 contested battlefield task。它把 4D-02D 的 response -> assignment integration 与 4D-02E 的 assignment -> next task advancement 串成同一条 focused guard。

覆盖代表：

- 当前 battle：`BF-DAMAGE`
- battle response source：`P2-SHADOW`
- 当前 active task：`task:start-battle:BF-DAMAGE`
- 后续 contested battlefield：`BF-NEXT`
- 后续 expected task：`task:start-spell-duel:BF-NEXT`

## Implemented Evidence

- `DECLARE_BATTLE + COMBAT_ASSIGNMENT` 在存在合法 Shadow response 时先打开 `BATTLE_RESPONSE_PRIORITY_OPENED`，且不提前打开 assignment window 或推进 `BF-NEXT`。
- P2 / P1 依次 pass 后，`BATTLE_RESPONSE_PRIORITY_CLOSED` 先于 `BATTLE_DAMAGE_ASSIGNMENT_OPENED`，当前 active task 仍指向 `BF-DAMAGE`。
- Assignment window 打开后仍不提前产生 `BF-NEXT` 的 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`。
- 合法 `ASSIGN_COMBAT_DAMAGE` 后，`BF-DAMAGE` battle close / battlefield control resolution 先完成，再推进 `BF-NEXT`。
- 最终状态进入 `SPELL_DUEL_TASKS`，active task 为 `task:start-spell-duel:BF-NEXT`，P1 prompt 为 `SpellDuelFocus`，不残留 stale `AssignCombatDamage` / `BattleDeclaration` prompt。

## Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Guardrails

- Runtime code was not changed in this slice.
- No frontend, LayerEngine, PaymentEngine, or coverage matrix work was done.
- P0-004 remains open for full official battle lifecycle breadth.
- P0-002, P0-003, P0-005, P1, READY, and full-card official coverage remain open.

## Verdict

4D-02N closes a cross-product evidence gap between battle-response priority, damage assignment, and next contested battlefield task advancement. It does **not** claim full combat official closure or READY status. Project remains **NOT READY**.
