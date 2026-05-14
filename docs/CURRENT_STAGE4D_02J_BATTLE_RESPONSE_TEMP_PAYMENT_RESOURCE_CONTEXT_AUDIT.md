# Stage 4D-02J Battle Response Temporary Payment Resource Context Audit

日期：2026-05-14
结论：**IMPLEMENTED / PROJECT NOT READY**

## Scope

本切片为 natural active `START_BATTLE` 的 battle-response declaration-context carrier 增加 held-score temporary payment resource guard。

覆盖代表：

- `DECLARE_BATTLE` optional costs: `COMBAT_ASSIGNMENT + TEMP_PAYMENT_RESOURCE:<resourceId>`
- source battlefield: `SFD·214/221`
- response source: legal Shadow battle-response source on the same battlefield
- temporary resource: owner `P2`, generic remaining power `1`, allowed payment kind `RUNE_COST`

## Implemented Guard

- Initial declaration opens `BATTLE_RESPONSE_PRIORITY_OPENED` before any held-score payment, temporary resource spend, score, or battle close.
- `BATTLE_DECLARED`, `BATTLE_RESPONSE_PRIORITY_OPENED`, `BATTLE_RESPONSE_PRIORITY_CLOSED`, and resumed `BATTLE_DECLARED` preserve the submitted temporary resource optional cost.
- After both players pass, the resumed battle consumes the original `TEMP_PAYMENT_RESOURCE:*` action in `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`.
- Audit events include `TEMPORARY_PAYMENT_RESOURCE_SPENT`, `TEMPORARY_PAYMENT_RESOURCE_CLEARED`, `COST_PAID`, and `SCORE_GAINED`.
- `COST_PAID.paymentResourceActions`, `temporaryPaymentResourceIds`, `temporaryPaymentResourcePower`, and `remainingPower` preserve the submitted action and consumption lifecycle.
- Internal `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` carrier remains filtered from player, opponent, spectator snapshots and prompt JSON.

## Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

No runtime changes were required; the Stage 4D-02G carrier already preserves optional costs and the existing held-score PaymentEngine path already consumes temporary payment resources.

## Remaining Scope

This is a focused context-preservation guard. It does not claim full battle lifecycle, LayerEngine, or full PaymentEngine closure.
