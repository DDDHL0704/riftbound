# Stage 4D-03AC PaymentEngine Battlefield Held Temporary Resource Audit

日期：2026-05-14
结论：**IMPLEMENTED REPRESENTATIVE / PROJECT NOT READY**

4D-03AC 完成 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 的 temporary payment-only resource parity representative。该切片继续收窄 P0-005，但不关闭 full PaymentEngine、P1、READY 或 full-official 状态。

## Scope

已覆盖 payment window：

- `BATTLEFIELD_HELD`
- trigger / reason: `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`
- cost: 4 generic power
- existing resource action: `RECYCLE_RUNE:*`
- new resource action: `TEMP_PAYMENT_RESOURCE:*`

## Runtime Behavior

- `DECLARE_BATTLE` prompt metadata now exposes held-score `paymentResourceChoices`, `paymentResourcePowerByChoice`, `availablePower`, and `availablePowerWithPaymentResources`.
- Prompt quoting handles direct held-score battlefield choices and Brush replacement choices that resolve to the original held-score battlefield identity.
- Prompt quoting considers recycle-rune choices before temporary resources, so mixed `RECYCLE_RUNE:*` + `TEMP_PAYMENT_RESOURCE:*` payment can be surfaced when both are needed.
- Command validation accepts necessary temporary resources owned by the held-score payment player, with remaining rune-cost power and `RUNE_COST` allowed payment kind.
- Successful held-score resolution consumes temporary resources inside the `BATTLEFIELD_HELD` payment window, emits temporary resource spend / cleanup events, writes `COST_PAID` audit metadata, and then awards score as before.
- Mixed recycle + temporary resource actions share the same `paymentId` in resource, payment, and score audit events.

## Guards

Focused tests cover:

- generic temporary resource prompt quote and command commit;
- typed temporary resource paying generic held-score cost;
- mixed recycle + temporary command commit;
- mixed recycle + temporary prompt quote;
- wrong-owner, zero, wrong-kind, duplicate, invalid-id, unnecessary, and insufficient temporary resource rejection with no mutation;
- already-scored and score-prevented paths do not consume temporary resources.

## No-Go

- No new resource-skill cards were added.
- No LayerEngine / timestamp / dependency work was started.
- No frontend files were changed.
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` was not changed.
- P0-005, P1, READY, and full-official status remain open.

## Residual Risk

This slice covers only the existing held-score representative payment window. Other future direct payment windows and full official PaymentEngine breadth remain outside this slice. Project status remains **NOT READY**.
