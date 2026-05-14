# Stage 4D-03AD PaymentEngine Trigger Temporary Resource Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本切片继续收窄 P0-005 PaymentEngine breadth。目标是把 `TRIGGER_PAYMENT` 中 SFD Fiora typed-power trigger payment representative，从已支持 `RECYCLE_RUNE:*` 扩展到 `TEMP_PAYMENT_RESOURCE:*` prompt / command / audit parity。

## Current Gap

现状：

- 普通 non-trigger pending `PAY_COST` 已支持 `TEMP_PAYMENT_RESOURCE:*`。
- `TRIGGER_PAYMENT` 在 `ResolvePendingPayCost` 中提前分流到 `ResolveTriggerPayCost`。
- `ResolveTriggerPayCost` 的 legal resource action set 只合并 `pendingPayment.PaymentResourceActionIds` 与 recycle choices，不合并 `TemporaryPaymentResourceActionIdsForPendingPayment`。
- `ResolveSfdFioraPowerfulReadyTriggerPayment` 只抽取并提交 `RECYCLE_RUNE:*`，未消费 temporary payment resource ledger。
- Prompt / snapshot 侧的 pending payment metadata 已能合并 temporary payment resources，存在潜在 prompt / command mismatch。

## Scope

实现范围仅限：

- payment window: `TRIGGER_PAYMENT`
- trigger / reason: `SFD_FIORA_POWERFUL_READY_PAY_YELLOW_READY`
- cost: 1 yellow typed power
- existing resource action: `RECYCLE_RUNE:*`
- new resource action: `TEMP_PAYMENT_RESOURCE:*`

Expected behavior:

- SFD Fiora trigger payment prompt exposes an owned typed-yellow temporary payment resource when P1 lacks yellow power.
- `PAY_COST` command accepts necessary `TEMP_PAYMENT_RESOURCE:*` for the Fiora trigger payment.
- Mixed recycle + temporary behavior is allowed only when useful and legal.
- Success emits temporary resource spend / cleanup events, shared `paymentId`, `COST_PAID` payload with temporary ids / typed contribution, then readies the powerful unit and closes the payment window.
- Invalid temporary resources reject with no mutation.

## Suggested Write Scope

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- optionally `src/Riftbound.Engine/MatchSession.cs` only if prompt metadata needs a narrow adjustment

Do not modify frontend files or `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Acceptance

Focused tests should cover:

- prompt quotes a typed-yellow temporary payment resource for SFD Fiora trigger payment;
- command consumes a typed-yellow temporary payment resource and readies the target;
- temporary spend / cleanup / `COST_PAID` use `TRIGGER_PAYMENT` and the same payment id;
- wrong owner, zero, wrong allowed kind, wrong trait, duplicate, invalid id, unnecessary, and insufficient temporary resource actions reject with no mutation;
- stale source / stale target still reject without consuming temporary resources;
- existing recycle-rune Fiora trigger payment remains green.

## No-Go

- Do not expand every trigger payment window in this slice.
- Do not let temporary resources pay mana-only trigger payments.
- Do not add new resource-skill cards.
- Do not enter LayerEngine / timestamp / dependency work.
- Do not close P0-005, P1, READY, or full-official status.
