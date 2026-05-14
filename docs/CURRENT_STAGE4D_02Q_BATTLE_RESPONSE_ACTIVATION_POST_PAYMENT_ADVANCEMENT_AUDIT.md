# Stage 4D-02Q Battle Response Activation Post-Payment Advancement Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

本切片验证 natural battle response 中真实 Shadow activation / stack resolution / return-to-response 后，final immediate battle close 若打开 trigger payment，服务端仍会以 `PendingPayment` 阻止 next contested battlefield advancement，并只在 accepted payment / accepted decline 关闭窗口后推进下一处 contested battlefield task。

覆盖代表：

- 当前 battle：`BF-DAMAGE`
- battle response source：`P2-SHADOW`
- stack effect：Shadow stun `UNL-194/219`
- post-battle trigger：`ICEVALE_ARCHER_ATTACK_PAY_1_POWER_MINUS_1`
- 后续 contested battlefield：`BF-NEXT`
- 后续 expected task：`task:start-spell-duel:BF-NEXT`

## Implemented Behavior

- Shadow activation stack-open 期间不推进 `BF-NEXT`。
- Shadow stack resolved 并回到 battle response priority 后仍不推进 `BF-NEXT`。
- Final response pass 后 immediate battle close 不打开 `BATTLE_DAMAGE_ASSIGNMENT_OPENED`。
- Icevale Archer trigger payment window 现在在 `BATTLE_CLOSED` / `BATTLEFIELD_CONTROL_RESOLVED` 后打开，避免 payment 先于 battle close/control ordering。
- `PendingPayment` 打开期间不输出 `BF-NEXT` 的 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`。
- Accepted payment 关闭窗口后推进 `BF-NEXT` 到 `SPELL_DUEL_TASKS` / `SpellDuelFocus`，并保留 `COST_PAID` / `BATTLEFIELD_TRIGGER_RESOLVED` / `POWER_MODIFIED_UNTIL_END_OF_TURN` 审计。
- Accepted decline 关闭窗口后同样推进 `BF-NEXT`，且不产生 cost / trigger effect。
- Rejected payment no-mutation，保留 `PendingPayment`，不推进 `BF-NEXT`。

## Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Guardrails

- Runtime change is limited to Icevale Archer attack trigger payment opening order inside the existing battle resolution path.
- No frontend, LayerEngine, PaymentEngine broad rewrite, or coverage matrix work was done.
- P0-004 remains open for full official battle lifecycle breadth.
- P0-002, P0-003, P0-005, P1, READY, and full-card official coverage remain open.

## Verdict

4D-02Q closes the cross-product gap between actual battle-response activation, immediate battle close, post-battle trigger payment blockers, and next contested battlefield task advancement. It does **not** claim full combat official closure or READY status. Project remains **NOT READY**.
