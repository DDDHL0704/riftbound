# Stage 4D-03B Payment Engine Non-Play Evidence

日期：2026-05-13
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文记录 Stage 4D-03B non-play payment window focused slice 证据。该证据接受 4D-03B focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Code Changes

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `ResolveActivateAbility` 的 Vi 代表路径改为构建 `ACTIVATE_ABILITY` `PaymentPlan`，并通过 `TryCommitPayment` 扣 mana / power。
  - `ResolveXerathDamageAbility` 将 Spellshield tax mana 与 ability power cost 合并进同一个 `ACTIVATE_ABILITY` plan envelope，再统一 commit。
  - `ResolveLegendAct` 使用 `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` 处理 mana 与 experience cost，保留既有不足资源错误分支。
  - `TryResolveBattlefieldHeldPayPowerScoreTrigger` 使用 `BATTLEFIELD_HELD` payment plan 支付 4 power，并输出 plan-driven `COST_PAID` metadata。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - 强化 Vi / Xerath activate ability、Legend Act 与 battlefield held score fixtures 的 `COST_PAID` payload 断言。

## 2. Covered Acceptance Points

- Vi `ACTIVATE_ABILITY` 代表路径保留事件顺序，并输出 `paymentWindow`、source object、ability id、reason、total mana / power 和 remaining pool metadata。
- Xerath `ACTIVATE_ABILITY` + Spellshield tax 使用同一个 payment envelope，保留 `spellshieldTaxMana` 与 taxed target ids metadata。
- `LEGEND_ACT` 的 experience payment 进入 shared commit，并输出 `experienceCost` 与 `remainingExperience`。
- Battlefield held pay-4-power score 通过 shared commit 扣 generic / typed power，并输出 `BATTLEFIELD_HELD` payment metadata。
- Existing GameHub / ActionPrompt seeds for activate ability, legend act and battlefield held score remain green.

## 3. Verification

Focused regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~P4ActivateAbilityCommandAddsViDoublePowerSkillToStack|FullyQualifiedName~P7TypedPowerPaymentActivatesViSkillWithTraitPool|FullyQualifiedName~P4ActivateAbilityCommandAddsXerathDamageSkillWithSpellshieldTaxToStack|FullyQualifiedName~P7TypedPowerPaymentActivatesXerathSkillWithTraitPool|FullyQualifiedName~P79LegendActSpendsExperienceExhaustsLegendAndDraws|FullyQualifiedName~P79LegendActRejectsInsufficientExperienceWithoutSideEffects|FullyQualifiedName~P79LegendActKhazixSpendsExperienceToGrantBoon|FullyQualifiedName~P79BattlefieldHeldPaysPowerToGainScore|FullyQualifiedName~P79BattlefieldHeldPaysTypedPowerToGainScore|FullyQualifiedName~P79BattlefieldScoreDelayPreventsHeldScorePaymentBeforeThirdTurn|FullyQualifiedName~P79BattlefieldUnitExperienceAbilitySeedOffersActivateAbilityAndGainsExperience|FullyQualifiedName~P79LegendActSeedBroadcastsPromptAndDrawsFromLegendActionInDevelopment|FullyQualifiedName~P79BattlefieldHeldScoreSeedOffersBattlefieldDestinationAndGainsScore"
```

Result: passed, 18/18.

Adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~LegendAct|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed, 318/318.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 3791/3791.

Whitespace:

```sh
git diff --check
```

Result: no output after implementation review.

## 4. Remaining Scope

This evidence is intentionally a focused slice. The following remain open:

- full `[A]` / payment-step `[C]` resource skill model;
- complete Haste / Echo / Spellshield payment windows;
- replacement / prevention / optional / extra / alternative cost breadth;
- prompt `sourceRequirements` / command payment quote parity for every payment path;
- all non-play payment windows beyond the accepted representatives;
- LayerEngine, keyword full-pass and full-card official matrix.
