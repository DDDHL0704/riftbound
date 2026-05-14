# Stage 4D-03AD PaymentEngine Trigger Temporary Resource Audit

日期：2026-05-14
结论：**IMPLEMENTED REPRESENTATIVE / PROJECT NOT READY**

4D-03AD 完成 SFD Fiora `TRIGGER_PAYMENT` 的 temporary payment-only resource parity representative。该切片继续收窄 P0-005，但不关闭 full PaymentEngine、P1、READY 或 full-official 状态。

## Scope

已覆盖 payment window：

- `TRIGGER_PAYMENT`
- trigger / reason: `SFD_FIORA_POWERFUL_READY_PAY_YELLOW_READY`
- cost: 1 yellow typed power
- existing resource action: `RECYCLE_RUNE:*`
- new resource action: `TEMP_PAYMENT_RESOURCE:*`

## Runtime Behavior

- `PAY_COST` trigger-payment validation now includes legal `TEMP_PAYMENT_RESOURCE:*` choices for the active pending payment.
- SFD Fiora typed-yellow trigger payment can consume owned temporary payment resources with remaining typed yellow rune-cost power.
- Pending payment prompt metadata now quotes typed temporary payment resources in `paymentResourceChoices`, `paymentResourcePowerByChoice`, and `availablePowerByTraitWithPaymentResources`.
- Successful trigger payment consumes temporary resources inside the `TRIGGER_PAYMENT` window, emits temporary resource spend / cleanup events, writes `COST_PAID` audit metadata, readies the target unit, and closes the payment window.
- `COST_PAID` separates submitted payment resource actions from spend choices and records temporary ids plus typed contribution.

## Guards

Focused tests cover:

- prompt quote for a typed-yellow temporary payment resource;
- command commit consuming the typed-yellow temporary resource and readying the target;
- temporary spend / cleanup / `COST_PAID` using the same `paymentId` and `TRIGGER_PAYMENT` window;
- wrong-owner, zero, wrong-kind, wrong-trait, duplicate, invalid-id, unnecessary, and insufficient temporary resource rejection with no mutation;
- stale source and stale target rejection without consuming temporary resources;
- existing recycle-rune trigger payment regressions.

## No-Go

- No broad expansion of every trigger payment window was performed.
- Temporary resources still do not pay mana-only trigger payments.
- No new resource-skill cards were added.
- No LayerEngine / timestamp / dependency work was started.
- No frontend files were changed.
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` was not changed.
- P0-005, P1, READY, and full-official status remain open.

## Residual Risk

This slice covers only the SFD Fiora typed-yellow trigger payment representative. Other future trigger payment windows and full official PaymentEngine breadth remain outside this slice. Project status remains **NOT READY**.
