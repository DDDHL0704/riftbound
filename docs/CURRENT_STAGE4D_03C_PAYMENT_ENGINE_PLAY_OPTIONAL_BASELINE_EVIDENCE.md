# Stage 4D-03C Payment Engine Play Optional/Extra Baseline Evidence

日期：2026-05-13
状态：**BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03C 实现前测试基线。该基线只说明当前 Haste / Echo / Spellshield / experience / payment-resource 代表路径行为绿色，可作为迁移 shared payment quote / commit 的回归护栏；不表示 4D-03C 已实现，不关闭 P0-005。

## 1. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P7PlayCardRecyclesLegacyOwnedRuneAsPaymentResourceAction|FullyQualifiedName~P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount|FullyQualifiedName~P7PlayCardPaymentResourceContributionMetadataSeparatesTraits|FullyQualifiedName~P7PlayCardGenericPaymentResourceCanUseMixedTraitContribution|FullyQualifiedName~P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions|FullyQualifiedName~P7PlayCardRejectsOverRecycledPaymentResourceActions|FullyQualifiedName~P7PlayCardRecyclesRuneForHasteReadyPaymentResourceAction|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForRepresentative|FullyQualifiedName~P4HasteOptionalReadyBranchRejectsInsufficientPower|FullyQualifiedName~P4EchoKeywordProfileBuildsManaOnlyRepeatPlan|FullyQualifiedName~CoreRuleEnginePlaysCenterStageEchoDrawThroughStack|FullyQualifiedName~CoreRuleEngineRejectsEchoWhenManaIsInsufficient|FullyQualifiedName~P4SpellshieldTaxAddsManaForEnemySpellTarget|FullyQualifiedName~P4SpellshieldTaxAggregatesMultipleEnemySpellTargets|FullyQualifiedName~CoreRuleEngineRejectsSpellshieldTaxWhenManaIsInsufficient|FullyQualifiedName~P4ExperienceOptionalCostReducesManaAndSpendsExperience|FullyQualifiedName~CoreRuleEngineRejectsPoppyExperienceOptionalCostWhenExperienceIsInsufficient|FullyQualifiedName~P79BattlefieldStaticReducesEchoCost|FullyQualifiedName~P79BattlefieldHeldNextSpellEchoRepeatsSpellAndConsumesMarker|FullyQualifiedName~P79HastePaymentRecycleSeedPaysReadyBranchThroughHub|FullyQualifiedName~P79SpellshieldMultipleTaxSeedEnumeratesLegalTargetsAndPaysThroughHub|FullyQualifiedName~P79BattlefieldStaticEchoCostReductionSeedPaysReducedEchoCost|FullyQualifiedName~P79BattlefieldHeldNextSpellEchoPromptOffersGrantedEchoAndRepeatsThroughHub|FullyQualifiedName~P79AssembleExperienceSeedOffersExperienceCostAndAttachesThroughHub"
```

结果：passed，31/31。

覆盖点：

- `PaymentCostRules` shared plan helper。
- `PLAY_CARD` recycle rune payment resource action。
- Haste ready payment resource action。
- Echo ordinary spell repeat、Echo insufficient mana、battlefield Echo reduction 与 battlefield-granted Echo。
- Spellshield single / multiple target tax 与 insufficient tax guard。
- Experience optional cost and assemble experience prompt seed。

## 2. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~Haste|FullyQualifiedName~Echo|FullyQualifiedName~Spellshield|FullyQualifiedName~Experience|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：passed，363/363。

## 3. Baseline Conclusion

当前代表路径绿色，可以进入 4D-03C 服务端实现；迁移后必须至少复跑上述 focused / adjacent filters、backend full 和 `git diff --check`。本基线不覆盖完整 `[A]` / `[C]` resource skills、所有替代 / 额外 / 可选费用、所有支付窗口、完整 LayerEngine 或 full-card official matrix。
