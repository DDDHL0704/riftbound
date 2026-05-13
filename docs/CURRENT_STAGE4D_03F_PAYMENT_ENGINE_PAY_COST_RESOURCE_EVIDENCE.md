# Stage 4D-03F Payment Engine PAY_COST Resource Evidence

日期：2026-05-13
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文记录 Stage 4D-03F pending `PAY_COST` payment resource focused slice 证据。该证据接受 4D-03F focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Code Changes

- `src/Riftbound.Engine/MatchSession.cs`
  - `PendingPaymentState` 新增 `PaymentResourceActionIds`，并在 normalization 中保留。
  - `BuildPendingPaymentSnapshotView` 暴露 `paymentResourceActions`。
  - `PayCostMetadataFor` / pending payment prompt metadata 将 spend choices 与 resource choices 分开，新增 `paymentResourcePowerByChoice`、`paymentResourceActionIds`、`availablePowerWithPaymentResources` 与 `availablePowerByTraitWithPaymentResources`。
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - 普通 pending `PAY_COST` 现在区分 spend choices 和 `RECYCLE_RUNE:*` resource actions。
  - 合法 resource actions 会先通过既有 recycle-rune payment helper 应用到复制的 player zones / card objects / rune pools，再调用 `PaymentCostRules.TryCommitPayment`。
  - 不必要或非法 resource action rejected 且不改变 authoritative state。
  - `BuildPendingPaymentPlan` 可以接收 resource action ids 与 spend-only legal choice ids，用于 plan-driven `COST_PAID` audit。
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - 新增 pending `PAY_COST` 回收红色符文后支付 typed red power 的成功路径。
  - 新增不必要回收符文和非法回收符文 no-mutation regression。

## 2. Covered Acceptance Points

- 普通 pending `PAY_COST` 可在当前 typed power 不足时提交 `RECYCLE_RUNE:<objectId>` 和 `SPEND_POWER:red:1`。
- 支付成功事件顺序为 `RUNE_RECYCLED` -> `POWER_GAINED` -> `COST_PAID` -> `PAYMENT_WINDOW_CLOSED`，且所有事件共享同一个 `paymentId` / `paymentWindow`。
- 回收符文会从基地移入符文牌堆底部，符文对象恢复未横置，随后 typed power 被同一 payment plan 扣除，最终 remaining pool 为 0。
- `COST_PAID` payload 包含 `paymentResourceActions`、`legalPaymentChoiceIds`、`paymentChoiceIds`、`recycledRuneObjectIds`、typed cost 与 remaining pool metadata。
- 当前已有足够 typed power 时夹带 `RECYCLE_RUNE:*` 会 rejected 且 state hash 不变。
- face-down 或缺少服务端 `cardNo` 的 rune resource action 会 rejected 且 no-mutation。
- 现有 `TRIGGER_PAYMENT` mana-only 支付 / 拒付 / 非法选择 / 资源不足代表路径保持绿色。

## 3. Verification

Focused regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST"
```

Result: passed, 55/55.

Adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed, 233/233.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 3804/3804.

Whitespace:

```sh
git diff --check
```

Result: no output.

## 4. Remaining Scope

This evidence is intentionally a focused slice. The following remain open:

- concrete `TRIGGER_PAYMENT` power-cost / resource-action representative path;
- full `[A]` / payment-step `[C]` resource skill model;
- `LEGEND_ACT` / battlefield held score payment resource actions;
- complete Haste / Echo / Spellshield payment windows;
- replacement / prevention / optional / extra / alternative cost breadth beyond accepted representatives;
- prompt `sourceRequirements` / command payment quote parity for every payment path;
- LayerEngine, keyword full-pass and full-card official matrix.
