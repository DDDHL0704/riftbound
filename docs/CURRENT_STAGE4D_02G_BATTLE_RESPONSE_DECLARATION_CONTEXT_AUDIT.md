# Stage 4D-02G Battle Response Declaration Context Audit

日期：2026-05-14
结论：**IMPLEMENTED / PROJECT NOT READY**

## Scope

本切片只补 natural battle-response 中的 declaration context preservation representative。

覆盖路径：

- active contested `START_BATTLE`
- `DECLARE_BATTLE + COMBAT_ASSIGNMENT`
- declaration includes battlefield target context
- legal Shadow battle response exists
- `BATTLE_RESPONSE_PRIORITY_OPENED`
- both players pass priority
- original declaration context is reused by the server
- resumed battle opens the expected Icevale Archer trigger payment
- internal context carrier is not exposed in player / spectator snapshots or prompt metadata

## Runtime Note

4D-02B intentionally limited battle-response opening to the minimal `COMBAT_ASSIGNMENT` shape with no battlefield target / replacement / payment-resource declaration context. 4D-02G removes that shortcut for active `START_BATTLE` battles: after validating the submitted declaration context, the server can open battle-response priority and store the original declaration context in authoritative state.

Implementation details:

- `ResolveDeclareBattle` validates Icevale Archer / defender battlefield target context before opening battle response.
- A server-side `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` carrier records battlefield id, attacker ids, defender ids, optional costs, and battlefield target ids.
- `ResolveBattleResponsePriorityPassed` restores the original `DeclareBattleCommand` from that carrier instead of rebuilding a pure `[COMBAT_ASSIGNMENT]` command.
- The carrier is cleared when the response window closes.
- `MatchSession.BuildContinuousEffectStates` filters the internal carrier so it does not appear as a public continuous effect.

## Test

新增：

- `BattleDamageAssignmentLifecycleTests.NaturalBattleResponsePreservesDeclarationContextAfterPass`

断言：

- initial declaration opens `BATTLE_RESPONSE_PRIORITY_OPENED`;
- declaration events preserve `battlefieldTargetObjectIds`;
- no immediate `PAYMENT_WINDOW_OPENED` before response pass;
- internal context carrier exists in authoritative state while the response window is open;
- P1 / P2 / spectator snapshots and P2 prompt do not contain `BATTLE_RESPONSE_DECLARATION_CONTEXT`;
- P2 prompt remains `STACK_PRIORITY` and bound to the correct battle / battlefield;
- after both players pass, `BATTLE_RESPONSE_PRIORITY_CLOSED` preserves the original target context;
- resumed declaration still contains the original battlefield target;
- Icevale Archer attack trigger payment opens with the preserved source / target context;
- carrier is cleared afterward;
- no stale assign-damage / declare-battle prompt remains for this payment branch.

## Non-Goals

- 未重写 combat。
- 未启动 LayerEngine。
- 未扩 PaymentEngine beyond the selected declaration-context path。
- 未修改前端、fixtures mass update 或 card coverage matrix。
- 未关闭 P0-004 / P0-005 / P1 / READY。
