# Stage 4D-03C Payment Engine Play Optional/Extra Evidence

日期：2026-05-13
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文记录 Stage 4D-03C `PLAY_CARD` optional / extra / payment-resource focused slice 证据。该证据接受 4D-03C focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Code Changes

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `TryBuildPlayCardPlan` 使用 `PaymentPlan` / `AuthorizePayment` 统一代表性 `PLAY_CARD` preflight affordability。
  - `ResolvePlayCard` 的 `PaymentPlan` audit metadata 增补 cost reductions、optional mana reduction、battlefield reductions/increases、Spellshield tax 与 recycled rune object ids。
  - `RECYCLE_RUNE:*` payment resource action 仍先落入临时资源池，再由 shared `TryCommitPayment` 执行 final commit，保留 no-mutation rollback。
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - 强化普通 typed-power `PLAY_CARD` `COST_PAID` payment plan metadata 断言。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - 强化 Haste ready、Echo、Spellshield tax、experience optional cost、battlefield Echo reduction 与 Haste payment resource fixtures 的 `COST_PAID` payload 断言。

## 2. Covered Acceptance Points

- Haste `HASTE_READY` 代表路径输出 `PLAY_CARD` payment id/window、source、reason、base/total mana、typed power、remaining pool 和 `paymentResourceActions` / `recycledRuneObjectIds` metadata。
- Echo `ECHO` 代表路径输出 base/total mana、optionalCosts、battlefield Echo reduction metadata 和 remaining mana。
- Spellshield target tax 代表路径输出 base/total mana、`spellshieldTaxMana` 与 taxed target ids。
- `SPEND_EXPERIENCE:*` optional cost 代表路径输出 `experienceCost`、`optionalCostManaReduction` 与 remaining experience。
- Existing prompt / Hub seeds for Haste, Echo, Spellshield, Experience and payment resources remain green.

## 3. Verification

Focused regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P7PlayCardRecyclesLegacyOwnedRuneAsPaymentResourceAction|FullyQualifiedName~P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount|FullyQualifiedName~P7PlayCardPaymentResourceContributionMetadataSeparatesTraits|FullyQualifiedName~P7PlayCardGenericPaymentResourceCanUseMixedTraitContribution|FullyQualifiedName~P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions|FullyQualifiedName~P7PlayCardRejectsOverRecycledPaymentResourceActions|FullyQualifiedName~P7PlayCardRecyclesRuneForHasteReadyPaymentResourceAction|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForRepresentative|FullyQualifiedName~P4HasteOptionalReadyBranchRejectsInsufficientPower|FullyQualifiedName~P4EchoKeywordProfileBuildsManaOnlyRepeatPlan|FullyQualifiedName~CoreRuleEnginePlaysCenterStageEchoDrawThroughStack|FullyQualifiedName~CoreRuleEngineRejectsEchoWhenManaIsInsufficient|FullyQualifiedName~P4SpellshieldTaxAddsManaForEnemySpellTarget|FullyQualifiedName~P4SpellshieldTaxAggregatesMultipleEnemySpellTargets|FullyQualifiedName~CoreRuleEngineRejectsSpellshieldTaxWhenManaIsInsufficient|FullyQualifiedName~P4ExperienceOptionalCostReducesManaAndSpendsExperience|FullyQualifiedName~CoreRuleEngineRejectsPoppyExperienceOptionalCostWhenExperienceIsInsufficient|FullyQualifiedName~P79BattlefieldStaticReducesEchoCost|FullyQualifiedName~P79BattlefieldHeldNextSpellEchoRepeatsSpellAndConsumesMarker|FullyQualifiedName~P79HastePaymentRecycleSeedPaysReadyBranchThroughHub|FullyQualifiedName~P79SpellshieldMultipleTaxSeedEnumeratesLegalTargetsAndPaysThroughHub|FullyQualifiedName~P79BattlefieldStaticEchoCostReductionSeedPaysReducedEchoCost|FullyQualifiedName~P79BattlefieldHeldNextSpellEchoPromptOffersGrantedEchoAndRepeatsThroughHub|FullyQualifiedName~P79AssembleExperienceSeedOffersExperienceCostAndAttachesThroughHub"
```

Result: passed, 31/31.

Adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~Haste|FullyQualifiedName~Echo|FullyQualifiedName~Spellshield|FullyQualifiedName~Experience|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed, 363/363.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 3791/3791.

Whitespace:

```sh
git diff --check
```

Result: no output.

## 4. Remaining Scope

This evidence is intentionally a focused slice. The following remain open:

- full `[A]` / payment-step `[C]` resource skill model;
- complete Haste / Echo / Spellshield payment windows;
- replacement / prevention / optional / extra / alternative cost breadth beyond accepted representatives;
- prompt `sourceRequirements` / command payment quote parity for every payment path;
- all payment windows beyond accepted representatives;
- LayerEngine, keyword full-pass and full-card official matrix.
