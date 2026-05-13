# Stage 4D-03B Payment Engine Non-Play Handoff

日期：2026-05-13
结论：**NOT READY**

本文是 Stage 4D-03B 的服务端实现交接规格。它延续 4D-03 focused foundation，只定义 P0-005 的下一片 non-play payment window 迁移范围，不授权前端补洞、不升级卡牌矩阵、不宣称完整 PaymentEngine、P1 或 READY 已关闭。

前置状态：4D-03 focused foundation 已验收，见 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_EVIDENCE.md`。4D-03B 的实现前基线见 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_BASELINE_EVIDENCE.md`。

## 1. Goal

4D-03B 要把更多非出牌支付窗口迁移到 4D-03 新增的 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` foundation：

- `ResolveActivateAbility` 的 Vi 代表技能支付。
- `ResolveXerathDamageAbility` 的 Xerath 技能支付与 Spellshield tax。
- `ResolveLegendAct` 的 mana / experience 支付。
- `TryResolveBattlefieldHeldPayPowerScoreTrigger` 的 pay-4-power held score trigger。

最小可接受推进是：以上至少三类非出牌支付路径不再直接 open-code `CanPayRuneCosts` / `PayRuneCosts` / `PayExperienceCosts`，并且 `COST_PAID` payload 统一带 `paymentId`、`paymentWindow`、source / ability / reason、total cost 与 remaining pool metadata。

## 2. Current Code Surface

Still open-coded after 4D-03:

- `CoreRuleEngine.ResolveActivateAbility`
  - Directly checks `CanPayRuneCosts(currentPool, ability.ManaCost, ability.PowerCost)`.
  - Directly calls `PayRuneCosts`.
  - Builds legacy `COST_PAID` payload without plan cost metadata.
- `CoreRuleEngine.ResolveXerathDamageAbility`
  - Separately applies spellshield tax + ability power cost.
  - Directly calls `CanPayRuneCosts` / `PayRuneCosts`.
- `CoreRuleEngine.ResolveLegendAct`
  - Separately checks mana and experience.
  - Directly calls `PayRuneCosts` and `PayExperienceCosts`.
  - Only emits `COST_PAID` for mana, while experience payment is represented separately.
- `CoreRuleEngine.TryResolveBattlefieldHeldPayPowerScoreTrigger`
  - Directly checks and pays 4 generic power.
  - Emits a minimal `COST_PAID` payload without `paymentId` / `paymentWindow` / remaining pool metadata.

## 3. Write Lock

Exclusive implementation write scope:

- `src/Riftbound.Engine/PaymentCostRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Allowed if prompt quote parity requires it:

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

Blocked parallel writes:

- Do not modify frontend UI in this slice.
- Do not modify card matrix status in this slice.
- Do not broaden into full `[A]` / `[C]` resource-skill implementation.
- Do not change board task / spell-duel / battle lifecycle behavior except where payment events are directly emitted.
- Do not stage or touch unrelated solution files.

## 4. Required Implementation Shape

- Build `PaymentPlan` before committing non-play payment windows.
- Use `TryCommitPayment` for rune pool and experience payment whenever the target path has a cost.
- Preserve existing rejection behavior and no-mutation semantics for insufficient mana/power/experience, wrong timing, wrong source, wrong targets and unsupported ability ids.
- Preserve existing event ordering unless a focused test intentionally documents the new payment envelope.
- Add plan-driven `COST_PAID` metadata while preserving existing compatibility keys such as `mana`, `power`, `abilityId` and `reason`.
- Keep battlefield held score deterministic and still prevent scoring the same battlefield twice in a turn.

## 5. Focused Acceptance Tests

4D-03B is not accepted until automated tests cover:

- Vi `ACTIVATE_ABILITY` uses shared payment plan metadata and still pays generic / typed power correctly.
- Xerath `ACTIVATE_ABILITY` with Spellshield tax uses one plan envelope for tax + skill cost and rejects insufficient tax without mutation.
- `LEGEND_ACT` pays mana and experience through shared commit, emits plan metadata, and keeps insufficient experience no-mutation.
- Battlefield held pay-4-power score emits `paymentId` / `paymentWindow` / remaining power metadata and still respects score-once-per-turn.
- Existing GameHub / ActionPrompt seeds for activate ability, legend act and battlefield held score remain green.

Suggested focused filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~P4ActivateAbilityCommandAddsViDoublePowerSkillToStack|FullyQualifiedName~P7TypedPowerPaymentActivatesViSkillWithTraitPool|FullyQualifiedName~P4ActivateAbilityCommandAddsXerathDamageSkillWithSpellshieldTaxToStack|FullyQualifiedName~P7TypedPowerPaymentActivatesXerathSkillWithTraitPool|FullyQualifiedName~P79LegendActSpendsExperienceExhaustsLegendAndDraws|FullyQualifiedName~P79LegendActRejectsInsufficientExperienceWithoutSideEffects|FullyQualifiedName~P79LegendActKhazixSpendsExperienceToGrantBoon|FullyQualifiedName~P79BattlefieldHeldPaysPowerToGainScore|FullyQualifiedName~P79BattlefieldHeldPaysTypedPowerToGainScore|FullyQualifiedName~P79BattlefieldScoreDelayPreventsHeldScorePaymentBeforeThirdTurn|FullyQualifiedName~P79BattlefieldUnitExperienceAbilitySeedOffersActivateAbilityAndGainsExperience|FullyQualifiedName~P79LegendActSeedBroadcastsPromptAndDrawsFromLegendActionInDevelopment|FullyQualifiedName~P79BattlefieldHeldScoreSeedOffersBattlefieldDestinationAndGainsScore"
```

Suggested adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~LegendAct|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Final gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## 6. No-Go Criteria

- Do not mark P0-005 resolved after this slice; full PaymentEngine still needs all `[A]` / `[C]`, Haste / Echo / Spellshield, replacement / optional / extra cost and all non-play windows.
- Do not rewrite prompt / command contracts unless required to keep touched payment paths authoritative.
- Do not update `IMPLEMENTED_TESTED` / full-official matrix fields from this handoff.
- Do not call active goal complete.

## 7. Handoff Summary

Next implementing agent should add focused tests first, then migrate the named non-play payment windows one by one to `PaymentPlan` / `TryCommitPayment`. Keep the patch narrow, preserve existing player-visible errors and no-mutation behavior, and return focused / adjacent / backend full output to A for review.
