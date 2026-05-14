# Stage 4D-02H Battle Response Brush Replacement Context Audit

日期：2026-05-14
结论：**ACCEPTED TEST-ONLY / PROJECT NOT READY**

## Scope

本切片只补 natural battle-response 中 Brush replacement declaration context 的 focused guard。4D-02G runtime carrier 已足够支撑该路径，本切片未修改 runtime。

覆盖路径：

- active contested Brush battlefield `START_BATTLE`
- Brush battlefield has `REPLACES_BATTLEFIELD:<original>` tag
- original battlefield is held-score representative
- `DECLARE_BATTLE` includes `COMBAT_ASSIGNMENT` and `BRUSH_USE_REPLACED_BATTLEFIELD:<original>`
- legal Shadow battle response exists
- both players pass priority
- resumed battle applies Brush replacement context
- held-score payment path uses original battlefield identity
- internal context carrier remains hidden from player / spectator snapshots and prompt

## Test

新增：

- `BattleDamageAssignmentLifecycleTests.NaturalBattleResponsePreservesBrushReplacementContextAfterPass`

断言：

- initial declaration accepted;
- `BATTLE_RESPONSE_PRIORITY_OPENED` appears before replacement / held / score / battle close;
- initial declaration and response-open events preserve Brush optional costs;
- internal `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` exists only in authoritative state during response;
- P1 / P2 / spectator snapshots and P2 prompt do not expose the carrier;
- after both players pass, close-response and resumed-declaration events still include Brush optional costs;
- `BATTLEFIELD_REPLACEMENT_APPLIED.replacementChoice` matches submitted Brush choice;
- replacement payload points from Brush battlefield to original held-score battlefield;
- held-score trigger / cost / score use the original battlefield identity;
- carrier is cleared afterward;
- no stale assignment/declaration prompt remains.

## Non-Goals

- 未修改 runtime。
- 未重写 combat。
- 未启动 LayerEngine。
- 未扩 PaymentEngine。
- 未修改前端、fixtures mass update 或 card coverage matrix。
- 未关闭 P0-004 / P0-005 / P1 / READY。
