# Stage 4D-02M Post-Payment Battle Task Advancement Audit

日期：2026-05-14
结论：**IMPLEMENTED / PROJECT NOT READY**

## Scope

本切片验证 post-battle `PendingPayment` 关闭后，服务端会恢复 pending battlefield task advancement。它承接 4D-02L 的 `PendingPayment` blocker：支付窗口打开期间不推进下一处 contested battlefield，支付接受或拒绝关闭窗口后再用 shared advancement helper 推进。

覆盖代表：

- 当前 battle：`P1-BATTLEFIELD-TREASURE-PILE`
- post-battle trigger payment：`BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD`
- 当前 active task：`task:start-battle:P1-BATTLEFIELD-TREASURE-PILE`
- 后续 contested battlefield：`P1-BATTLEFIELD-NEXT-FIELD`
- 后续 expected task：`task:start-spell-duel:P1-BATTLEFIELD-NEXT-FIELD`

## Implemented Behavior

- Trigger payment open state keeps `PendingPayment` authoritative and does not emit next battlefield `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`.
- Accepted trigger payment now closes the payment window, preserves payment / trigger / token audit events, then advances the next contested battlefield when no blocker remains.
- Declined trigger payment also closes the blocker and advances the next contested battlefield, without `COST_PAID`, trigger resolution or token creation.
- Rejected payment attempts remain no-mutation, keep `PendingPayment` open and do not advance next battlefield tasks.
- Runtime uses `BuildAcceptedResolutionAfterPaymentWindowClosed(...)` to call existing `AdvancePendingBattlefieldTasksAfterStateChange(...)` after accepted trigger payment close paths, keeping blocker semantics centralized.
- The helper is reused by battlefield conquer gold, powerful draw, OGN Vayne recall, Icevale Archer attack, Jax weapon attach, SFD Fiora ready trigger and generic trigger decline paths.

## Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

## Remaining Scope

本切片只收口 post-battle payment close 的 next contested task advancement representative。它不关闭完整 battle lifecycle、完整 held / conquer / control matrix、P0-002、P0-003、P0-004、P0-005、P1、前端最终验收、card matrix 或 READY。
