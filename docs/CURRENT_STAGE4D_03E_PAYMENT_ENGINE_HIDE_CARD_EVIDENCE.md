# Stage 4D-03E Payment Engine Hide Card Evidence

日期：2026-05-13
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文记录 Stage 4D-03E `HIDE_CARD` standby payment focused slice 证据。该证据接受 4D-03E focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Code Changes

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `ResolveHideCard` 为 `STANDBY_A`、`STANDBY_TEEMO_MANA` 与 `STANDBY_FREE` 构造 `PaymentCostRules.PaymentPlan`。
  - `AuthorizePayment` / `TryCommitPayment` 在任何状态 mutation 之前执行，费用不足保持 rejected no-mutation。
  - `COST_PAID` payload 改用 plan-driven `BuildCostPaidPayload`，保留 legacy keys 并记录 base / total cost、remaining pool、`reason = STANDBY_HIDE` 与 `sourceObjectId`。
  - `STANDBY_FREE` 表达为 0-cost plan，`STANDBY_TEEMO_MANA` 保持 1 mana plan。
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - 新增标准待命、免费待命、Teemo 替代待命和费用不足 no-mutation 的 plan audit metadata 测试。

## 2. Covered Acceptance Points

- 标准 `HIDE_CARD optionalCosts=["STANDBY_A"]` 支付 1 mana 后，`COST_PAID` 带 `paymentWindow = HIDE_CARD`、`sourceObjectId`、`reason`、base / total cost 与 remaining pool metadata。
- `STANDBY_FREE` 使用 0-cost payment plan，保留 `standbyHideCostWaived` 并不扣资源。
- `STANDBY_TEEMO_MANA` 使用 payment plan，保留 `teemoStandbyHideReplacement` 并支付 1 mana。
- 费用不足拒绝且 state hash 不变，不移动手牌、不翻面、不写事件。
- Bandle Tree extra standby、hidden-info payload、Ember Monk trigger 与相邻 reveal / prompt / Hub regressions 保持绿色。

## 3. Verification

Focused regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HideCard|FullyQualifiedName~Standby|FullyQualifiedName~PaymentEngineUnificationTests"
```

Result: passed, 88/88.

Adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HideCard|FullyQualifiedName~Standby|FullyQualifiedName~RevealCard|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed, 290/290.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 3800/3800.

Whitespace:

```sh
git diff --check
```

Result: no output.

## 4. Remaining Scope

This evidence is intentionally a focused slice. The following remain open:

- full `[A]` / payment-step `[C]` resource skill model;
- `LEGEND_ACT` / `PAY_COST` pending trigger payment / battlefield held score payment resource actions;
- complete Haste / Echo / Spellshield payment windows;
- complete standby reveal / reaction lifecycle;
- replacement / prevention / optional / extra / alternative cost breadth beyond accepted representatives;
- prompt `sourceRequirements` / command payment quote parity for every payment path;
- LayerEngine, keyword full-pass and full-card official matrix.
