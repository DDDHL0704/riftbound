# Stage 4D-02K Battle Response Activation Brush Context Audit

日期：2026-05-14
结论：**IMPLEMENTED / PROJECT NOT READY**

## Scope

本切片验证 declaration context 可穿过 actual battle-response activation / stack resolution lifecycle。

覆盖代表：

- `DECLARE_BATTLE` optional costs: `COMBAT_ASSIGNMENT + BRUSH_USE_REPLACED_BATTLEFIELD:<originalBattlefieldObjectId>`
- Brush battlefield replaces original `SFD·214/221` held-score battlefield
- P2 controls a declared defender and a separate ready Shadow response source at the same battlefield
- Shadow is not included in `DefenderObjectIds`

## Implemented Guard

- Initial declaration opens `BATTLE_RESPONSE_PRIORITY_OPENED` and preserves Brush optional cost.
- P2 prompt exposes Shadow `ACTIVATE_ABILITY`.
- Shadow activation emits `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `COST_PAID`, and `STACK_ITEM_ADDED`.
- Shadow stack pass-pass resolves and returns to battle-response priority with the declaration context still present server-side.
- Internal `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` carrier remains filtered from P1 / P2 / spectator snapshots and prompt JSON after activation and after stack resolution.
- Final response pass-pass preserves Brush optional cost in `BATTLE_RESPONSE_PRIORITY_CLOSED` and resumed `BATTLE_DECLARED`.
- Final branch emits `BATTLEFIELD_REPLACEMENT_APPLIED`, held-score `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID`, `SCORE_GAINED`, and `BATTLE_CLOSED`.
- Final state clears the internal carrier and has no stale battle declaration / assignment prompt.

## Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

No runtime changes were required; the Stage 4D-02G carrier survives Shadow activation and stack resolution.

## Remaining Scope

This is a focused lifecycle/context guard. It does not claim full battle lifecycle, LayerEngine, or full PaymentEngine closure.
