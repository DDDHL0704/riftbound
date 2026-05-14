# Stage 4D-02P Battle Response Activation Immediate Advancement Audit

日期：2026-05-15
结论：**TEST-ONLY GUARD ACCEPTED / PROJECT NOT READY**

## Scope

本切片验证 natural battle response 中真实 Shadow activation / stack resolution / return-to-response 后，如果 final response pass 进入 immediate battle close，服务端仍会在当前 battle close / control resolution 后推进下一处 contested battlefield task。它在 4D-02O assignment branch guard 之上，补上 actual stack lifecycle 参与后的 immediate branch parity 证据。

覆盖代表：

- 当前 battle：`BF-DAMAGE`
- battle response source：`P2-SHADOW`
- stack effect：Shadow stun `UNL-194/219`
- 当前 active task：`task:start-battle:BF-DAMAGE`
- 后续 contested battlefield：`BF-NEXT`
- 后续 expected task：`task:start-spell-duel:BF-NEXT`

## Implemented Evidence

- `DECLARE_BATTLE + COMBAT_ASSIGNMENT` 在存在合法 Shadow response source 时先打开 `BATTLE_RESPONSE_PRIORITY_OPENED`，且不提前推进 `BF-NEXT`。
- P2 激活 Shadow 后产生 stack-open 状态，`BF-NEXT` 仍未推进。
- P2 / P1 pass stack priority 后，stack 被结算并回到 battle response priority，`BF-NEXT` 仍未推进。
- P2 final response pass 后仍不推进 `BF-NEXT`，也不提前打开 stale next task。
- P1 final response pass 后关闭 battle response，不打开 `BATTLE_DAMAGE_ASSIGNMENT_OPENED`。
- immediate branch 完成当前 `BF-DAMAGE` battle close / battlefield control resolution 后，再推进 `BF-NEXT`。
- 最终状态进入 `SPELL_DUEL_TASKS`，active task 为 `task:start-spell-duel:BF-NEXT`，P1 prompt 为 `SpellDuelFocus`，不残留 stale `AssignCombatDamage` / `BattleDeclaration` prompt。

## Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Guardrails

- Runtime code was not changed in this slice.
- No frontend, LayerEngine, PaymentEngine, or coverage matrix work was done.
- P0-004 remains open for full official battle lifecycle breadth.
- P0-002, P0-003, P0-005, P1, READY, and full-card official coverage remain open.

## Verdict

4D-02P closes a cross-product evidence gap between actual battle-response activation, stack resolution, immediate battle close, and next contested battlefield task advancement. It does **not** claim full combat official closure or READY status. Project remains **NOT READY**.
