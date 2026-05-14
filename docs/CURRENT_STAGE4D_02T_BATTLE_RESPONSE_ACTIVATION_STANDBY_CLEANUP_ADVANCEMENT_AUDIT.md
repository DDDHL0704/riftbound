# Stage 4D-02T Battle Response Activation Standby Cleanup Advancement Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

本切片验证 natural battle response 中真实 Shadow activation / stack resolution / return-to-response 后进入 `ASSIGN_COMBAT_DAMAGE`；合法 assignment 关闭当前 battle 并触发 battlefield-control-driven illegal standby cleanup 时，cleanup 会先于下一处 contested battlefield advancement 完成。

覆盖代表：

- 当前 battle：`BF-DAMAGE`
- battle response source：declared defender `P2-SHADOW`
- stack effect：Shadow stun `UNL-194/219`
- cleanup object：P2 hidden standby on `BF-DAMAGE`
- 后续 contested battlefield：`BF-NEXT`
- 后续 expected task：`task:start-spell-duel:BF-NEXT`

## Implemented Behavior

- Shadow activation stack-open 期间不推进 `BF-NEXT`。
- Shadow stack resolved 并回到 battle response priority 后仍不推进 `BF-NEXT`。
- Final response pass 后打开 `BATTLE_DAMAGE_ASSIGNMENT_OPENED`，assignment window 期间仍不推进 `BF-NEXT`。
- Legal assignment destroys Shadow / Bulwark, leaves P1 attacker surviving, closes current battle, and resolves `BF-DAMAGE` control from P2 to P1.
- Illegal standby cleanup emits `BATTLEFIELD_STANDBY_REMOVED` before `BF-NEXT` emits `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`.
- Hidden standby moves to P2 graveyard, becomes face-up, and has authoritative `ObjectLocation.Zone == "GRAVEYARD"`.
- Final state has no stale current `START_BATTLE` or `REMOVE_ILLEGAL_STANDBY` cleanup task, and `BF-NEXT` is active in `SPELL_DUEL_TASKS` / `SpellDuelFocus`.

## Runtime Notes

- `ResolveDeclareBattle` now accepts an internal `resumingBattleResponseDeclaration` flag. It is only passed by battle-response replay after both players pass the response window.
- Ordinary player-submitted `DECLARE_BATTLE` still uses the default path and still requires ready face-up units.
- Internal battle-response replay can tolerate participants already exhausted by response costs, so an activated Shadow that was a declared defender can still be part of the resumed battle declaration.
- `ApplyResolvedStackSourceLocation` now preserves a precise battlefield object id for an active battle participant stack source with no movement destination. This prevents response stack resolution from erasing the battlefield identity needed by declaration replay and cleanup ordering.

## Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Guardrails

- Runtime changes are limited to battle-response declaration replay and precise location preservation for active battle participant stack sources.
- No frontend, LayerEngine, PaymentEngine broad rewrite, or coverage matrix work was done.
- P0-004 remains open for full official battle lifecycle breadth.
- P0-002, P0-003, P0-005, P1, READY, and full-card official coverage remain open.

## Verdict

4D-02T closes the cross-product gap between actual battle-response activation, returned assignment, battlefield-control-driven illegal standby cleanup, and next contested battlefield task advancement. It does **not** claim full combat official closure or READY status. Project remains **NOT READY**.
