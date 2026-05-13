# Stage 4D-03D Payment Engine Activate Resource Evidence

日期：2026-05-13
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文记录 Stage 4D-03D Vi / Xerath `ACTIVATE_ABILITY` payment resource action focused slice 证据。该证据接受 4D-03D focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Code Changes

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - Vi / Xerath `ACTIVATE_ABILITY` 路径接受合法 `RECYCLE_RUNE:<objectId>` payment resource action。
  - 回收符文资源动作先在本地副本中移动符文、增加 typed power 并产生 `RUNE_RECYCLED` / `POWER_GAINED`，再进入 shared `PaymentPlan` authorize / commit。
  - `COST_PAID` payload 保留 plan metadata，并记录 `paymentResourceActions`、`recycledRuneObjectIds`、remaining mana / power 与 Xerath `spellshieldTaxMana`。
  - 无效、重复、不必要或 mana 不足的资源动作保持 no-mutation。
- `src/Riftbound.Engine/MatchSession.cs`
  - `ACTIVATE_ABILITY.sourceRequirements` 新增 payment resource choice 与 per-choice power contribution metadata。
  - Vi / Xerath prompt 在当前 power 不足但基地有可回收符文时，公开服务端可提交的 `RECYCLE_RUNE:*` token。
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - 新增 Vi / Xerath quote + commit、过量 / 无效资源动作、Spellshield tax mana 不足 no-mutation 测试。

## 2. Covered Acceptance Points

- Vi 在 mana 足够、power 不足且基地有可回收基础符文时，prompt 暴露 `RECYCLE_RUNE:<objectId>`，命令可在同一 `ACTIVATE_ABILITY` optional costs 中回收符文支付 1 generic power。
- Xerath 在目标带 Spellshield 时可通过回收符文补足 power，但 `spellshieldTaxMana` 仍必须由当前 mana 支付。
- `RUNE_RECYCLED` / `POWER_GAINED` 与 `COST_PAID` 使用同一 `paymentWindow = ACTIVATE_ABILITY` 和同一个 `paymentId`。
- `COST_PAID` audit payload 记录 `paymentResourceActions`、`recycledRuneObjectIds`、total / remaining cost metadata。
- 不必要回收、面朝下符文、Spellshield tax mana 不足均 rejected 且 state hash 不变。

## 3. Verification

Focused regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActivateAbility|FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P7TypedPowerPaymentAssemblesLongSwordWithRecycleRunePaymentResource|FullyQualifiedName~CoreRuleEngineRejectsTapRuneSourceWithoutCardNo|FullyQualifiedName~CoreRuleEngineTapsBasicRuneAndReconcilesObjectLocation|FullyQualifiedName~CoreRuleEnginePromptsAndTapsLegacyOwnedBasicRune|FullyQualifiedName~CoreRuleEngineRejectsRecycleRuneSourceWithoutCardNo|FullyQualifiedName~CoreRuleEngineRecyclesBasicRuneForMatchingTraitPower|FullyQualifiedName~CoreRuleEngineRecyclesLegacyOwnedBasicRune|FullyQualifiedName~CoreRuleEngineRecyclesBasicRuneAndReconcilesObjectLocations"
```

Result: passed, 84/84.

Adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentResource|FullyQualifiedName~RecycleRune|FullyQualifiedName~TapRune|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed, 257/257.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 3796/3796.

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
- replacement / prevention / optional / extra / alternative cost breadth beyond accepted representatives;
- prompt `sourceRequirements` / command payment quote parity for every payment path;
- LayerEngine, keyword full-pass and full-card official matrix.
