# Plan B Source Next Spell Cost Reduction Behavior Fields Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `OGN·031/298` 狂暴龙怪 states that when the source is played, the controller's next spell this turn costs 5 less.

Existing engine evidence:

- Existing conformance coverage exercises marker creation, prompt reduction, authoritative payment, marker consumption, and Lux paid-cost interaction when the reduced paid cost drops below the high-cost threshold.

## Engine Evidence

Before this slice, `CoreRuleEngine` selected the branch through `RagingDrakeNextSpellCostSourceEffectKind` and direct `behavior.EffectKind` comparison. `CoreRuleEngine` and `MatchSession` also multiplied matching markers by a hard-coded `RagingDrakeNextSpellCostReductionMana = 5`.

After this slice:

- `CoreRuleEngine` no longer contains `RagingDrakeNextSpellCostSourceEffectKind`.
- `CoreRuleEngine` no longer contains `RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT` as a runtime selector.
- `CoreRuleEngine` and `MatchSession` no longer contain `RagingDrakeNextSpellCostReductionMana`.
- `CardBehaviorRegistry` stores the official amount and public effect kind on the `OGN·031/298` behavior row.
- `CoreRuleEngine` reads `SourceNextSpellCostReductionMana` and `SourceNextSpellCostReductionEffectKind` when creating the marker.
- `CoreRuleEngine` and `MatchSession` parse source-next-spell markers and resolve their amount through `CardBehaviorRegistry.TryGetSourceNextSpellCostReductionByEffectKind`.

## Test Evidence

- `PlayBehaviorSourceIdentityGuardTests.RagingDrakeNextSpellCostPlaySourceUsesBehaviorFields` blocks reintroducing the runtime effect-kind selector and hard-coded Raging Drake amount in Core or MatchSession.
- `CardCatalogBaselineTests.RagingDrakeSourceNextSpellCostReductionCarriesOfficialAmount` locks the official row to amount `5` and effect kind `RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION`.
- Existing `P79RagingDrake*` regressions passed with unchanged marker, prompt, payment, and consumption behavior.
- Existing `LuxPaidCostHighPrintedSpellReducedBelowThresholdDoesNotTriggerUnitOrLegend` passed, proving legacy marker-only state still resolves the amount correctly.
- Adjacent / hidden-info representative gate `PlayBehaviorSourceIdentityGuardTests|RagingDrake|LuxHighCost|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3035/3035`.
- Backend full conformance passed `9028/9028`.

## Non-Claims

This evidence does not claim complete source next-spell cost-reduction official breadth, complete play-trigger routing, complete PaymentEngine, P0 completion, or READY.
