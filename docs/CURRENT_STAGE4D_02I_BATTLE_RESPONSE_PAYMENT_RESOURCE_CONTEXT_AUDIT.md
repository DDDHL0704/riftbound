# Stage 4D-02I Battle Response Payment Resource Context Audit

日期：2026-05-14
结论：**IMPLEMENTED / PROJECT NOT READY**

## Scope

本切片为 natural active `START_BATTLE` 的 battle-response declaration-context carrier 增加 held-score payment-resource guard。

覆盖代表：

- `DECLARE_BATTLE` optional costs: `COMBAT_ASSIGNMENT + RECYCLE_RUNE:<runeObjectId>`
- source battlefield: `SFD·214/221`
- response source: legal Shadow battle-response source on the same battlefield

## Implemented Guard

- Initial declaration opens `BATTLE_RESPONSE_PRIORITY_OPENED` before any held-score payment, score, or battle close.
- `BATTLE_DECLARED`, `BATTLE_RESPONSE_PRIORITY_OPENED`, `BATTLE_RESPONSE_PRIORITY_CLOSED`, and resumed `BATTLE_DECLARED` preserve the payment-resource optional cost.
- After both players pass, the resumed battle uses the original `RECYCLE_RUNE:*` action in `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`.
- Audit events include `RUNE_RECYCLED`, `POWER_GAINED`, `COST_PAID`, and `SCORE_GAINED`.
- `COST_PAID.paymentResourceActions` and `COST_PAID.recycledRuneObjectIds` preserve the submitted resource action / rune id.
- Internal `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` carrier remains filtered from player, opponent, spectator snapshots and prompt JSON.

## Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

No runtime changes were required; the Stage 4D-02G carrier already preserves optional costs.

## Remaining Scope

This is a focused context-preservation guard. It does not claim full battle lifecycle, LayerEngine, or full PaymentEngine closure.
