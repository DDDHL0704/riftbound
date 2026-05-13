# Stage 4D-03B Payment Engine Non-Play Baseline Evidence

日期：2026-05-13
结论：**BASELINE GREEN / PROJECT NOT READY**

本文记录 Stage 4D-03B 实现前的 non-play payment baseline。它只说明当前 HEAD 的代表路径测试为绿色，不关闭 P0-005，不升级 full official，不改变项目 **NOT READY** 结论。

## 1. Scope

4D-03B 目标来自 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_HANDOFF.md`：把 `ACTIVATE_ABILITY`、`LEGEND_ACT` 与 battlefield held score 这类非出牌支付窗口接入 4D-03 的 shared payment plan / commit foundation。

当前已存在绿色代表路径：

- Vi / Xerath `ACTIVATE_ABILITY` 支付、typed power、Spellshield tax 和 no-mutation guard。
- `LEGEND_ACT` mana / experience 支付、insufficient experience no-mutation 与 GameHub prompt seed。
- Battlefield held pay-power score、typed power score、score delay、score-once-per-turn 与 GameHub prompt seed。
- 4D-03 `PaymentEngineUnificationTests` 的 shared plan / rollback / audit metadata foundation。

当前仍未证明：

- 这些非出牌窗口已经共享 `PaymentPlan` / `TryCommitPayment`。
- `COST_PAID` payload 已统一暴露 `paymentId`、`paymentWindow`、total cost 和 remaining pool metadata。
- Prompt quote 与 command commit 对所有非出牌窗口完全同源。

## 2. Baseline Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~P4ActivateAbilityCommandAddsViDoublePowerSkillToStack|FullyQualifiedName~P7TypedPowerPaymentActivatesViSkillWithTraitPool|FullyQualifiedName~P4ActivateAbilityCommandAddsXerathDamageSkillWithSpellshieldTaxToStack|FullyQualifiedName~P7TypedPowerPaymentActivatesXerathSkillWithTraitPool|FullyQualifiedName~P79LegendActSpendsExperienceExhaustsLegendAndDraws|FullyQualifiedName~P79LegendActRejectsInsufficientExperienceWithoutSideEffects|FullyQualifiedName~P79LegendActKhazixSpendsExperienceToGrantBoon|FullyQualifiedName~P79BattlefieldHeldPaysPowerToGainScore|FullyQualifiedName~P79BattlefieldHeldPaysTypedPowerToGainScore|FullyQualifiedName~P79BattlefieldScoreDelayPreventsHeldScorePaymentBeforeThirdTurn|FullyQualifiedName~P79BattlefieldUnitExperienceAbilitySeedOffersActivateAbilityAndGainsExperience|FullyQualifiedName~P79LegendActSeedBroadcastsPromptAndDrawsFromLegendActionInDevelopment|FullyQualifiedName~P79BattlefieldHeldScoreSeedOffersBattlefieldDestinationAndGainsScore"
```

Result: passed, 18/18.

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~LegendAct|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed, 318/318.

Backend full inherited from accepted 4D-03 evidence:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result at 4D-03 acceptance: passed, 3791/3791.

## 3. A Interpretation

- Current non-play payment paths are behaviorally green and can be used as 4D-03B regression guardrails.
- The green baseline does not prove P0-005 complete because the implementation still has open-coded payment checks and commits in `ResolveActivateAbility`, `ResolveXerathDamageAbility`, `ResolveLegendAct` and `TryResolveBattlefieldHeldPayPowerScoreTrigger`.
- 4D-03B should migrate those representative paths without broadening into full official PaymentEngine.

## 4. Next Step

Use `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_HANDOFF.md` as Maxwell / B service-side implementation handoff. A will accept the slice only after reviewing diff, focused / adjacent / backend full output and updated audit docs.
