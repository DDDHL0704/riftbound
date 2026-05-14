# Stage 4D-03AE PaymentEngine Pending Temporary Resource Prompt Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本切片继续收窄 P0-005 PaymentEngine breadth。目标是修正 pending `PAY_COST` / `TRIGGER_PAYMENT` prompt metadata 中 temporary payment resource aggregate 的 quote parity，使 `availablePowerWithPaymentResources` 与 command 侧合法 temporary resource choices 保持同一口径。

## Current Gap

现状：

- 4D-03AD 已让 SFD Fiora `TRIGGER_PAYMENT` command path 能消费合法 typed `TEMP_PAYMENT_RESOURCE:*`。
- `PendingPaymentResourcePowerByTrait(state, payment)` 已合并合法 temporary payment resource typed contribution。
- `availablePowerWithPaymentResources` 仍额外累加 `TemporaryPaymentResourcePower(state, payment)`。
- `TemporaryPaymentResourcePower(state, payment)` 只按 owner / remaining / `RUNE_COST` / payment has power cost 过滤，没有调用 `TemporaryPaymentResourceCanHelpPowerCost`，因此：
  - 合法 typed temporary resource 可能被 `availablePowerWithPaymentResources` 双算；
  - wrong-trait typed temporary resource 也可能抬高 aggregate available power；
  - command 侧仍会拒绝 wrong-trait / insufficient temporary resources，形成 prompt metadata / command parity 风险。

## Scope

实现范围仅限 pending payment prompt metadata：

- `paymentWindow`: ordinary pending `PAY_COST` and `TRIGGER_PAYMENT`
- affected metadata:
  - `paymentResourceChoices`
  - `paymentResourcePowerByChoice`
  - `availablePowerWithPaymentResources`
  - `availablePowerByTraitWithPaymentResources`
- temporary resource type: `TEMP_PAYMENT_RESOURCE:*`

Expected behavior:

- `availablePowerWithPaymentResources` must count only legal payment resources that can help the current pending payment.
- Legal temporary typed power must not be double counted.
- Wrong-trait temporary resources must not inflate aggregate available power.
- Per-trait metadata and per-choice metadata remain unchanged except where needed to keep aggregate parity.
- Command behavior should remain unchanged unless tests reveal an adjacent audit payload inconsistency.

## Suggested Write Scope

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- optionally `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

`CoreRuleEngine.cs` should not need changes unless implementation discovers a command/audit mismatch beyond prompt metadata.

Do not modify frontend files or `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Acceptance

Focused tests should cover:

- SFD Fiora pending `TRIGGER_PAYMENT` with one legal typed-yellow temporary resource reports aggregate available power as 1, not 2;
- wrong-trait temporary resource does not appear in `paymentResourceChoices` and does not inflate `availablePowerWithPaymentResources`;
- mixed recycle + temporary pending payment aggregate counts each legal resource exactly once;
- ordinary pending `PAY_COST` temporary resource metadata remains green;
- existing 4D-03AD command consumption tests remain green.

## No-Go

- Do not add new resource-skill cards.
- Do not expand Renata or every `ACTIVATE_ABILITY` window in this slice.
- Do not let temporary resources pay mana-only / experience / non-rune costs.
- Do not add protocol fields unless an existing metadata field cannot express parity.
- Do not modify frontend files.
- Do not enter LayerEngine / timestamp / dependency work.
- Do not close P0-005, P1, READY, or full-official status.
