# Plan B Source Next Spell Cost Reduction Behavior Fields Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the runtime effect-kind selector for `OGN·031/298` 狂暴龙怪's current source-unit next-spell cost-reduction representative branch.

The stable catalog effect id `RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT` remains in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. It is no longer referenced by `CoreRuleEngine` to decide whether the source-next-spell cost reduction branch applies.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGN·031/298` 狂暴龙怪: when the source is played, reduce the cost of the controller's next spell this turn by 5.
- Existing direct engine regressions `P79RagingDrakeCreatesNextSpellCostReductionAfterResolution`, `P79RagingDrakeNextSpellCostReductionPromptShowsReducedSpellCost`, and `P79RagingDrakeNextSpellCostReductionPaysReducedSpellCostAndConsumesMarker` cover marker creation, prompt reduction, payment, and marker consumption.
- Existing hub regression `P79RagingDrakeNextSpellCostReductionPromptOffersReducedSpellThroughHub` covers prompt metadata through the session path.

## Implementation

- `CardBehaviorDefinition` now carries source-next-spell cost reduction metadata:
  - `SourceNextSpellCostReductionMana`
  - `SourceNextSpellCostReductionEffectKind`
- `OGN·031/298` fills those fields with amount `5` and legacy public effect kind `RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION`.
- `CoreRuleEngine` creates the until-end marker from those fields instead of checking `RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT`.
- `CoreRuleEngine` consumes next-spell markers by parsing the marker effect kind and source object id, then resolving the amount through `CardBehaviorRegistry`.
- `MatchSession` prompt cost calculation uses the same marker parsing and behavior-row lookup so prompt metadata and authoritative payment stay aligned.
- Legacy marker shape `RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:<playerId>:<sourceObjectId>` is preserved for existing recovery, fixture, and UI expectations.

## Validation

- Baseline before this slice: backend full conformance passed `9027/9027`.
- Red focused source guard failed before implementation because `SourceNextSpellCostReductionMana` and `SourceNextSpellCostReductionEffectKind` did not exist.
- Green focused representative gate: `RagingDrakeNextSpellCostPlaySourceUsesBehaviorFields|RagingDrakeSourceNextSpellCostReductionCarriesOfficialAmount|P79RagingDrake|LuxPaidCostHighPrintedSpellReducedBelowThresholdDoesNotTriggerUnitOrLegend` passed `7/7`.
- Adjacent / hidden-info representative gate: `PlayBehaviorSourceIdentityGuardTests|RagingDrake|LuxHighCost|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3035/3035`.
- Backend full conformance passed `9028/9028`.

## Holdbacks

This does not close complete source next-spell cost-reduction official breadth, complete play-trigger routing, complete PaymentEngine, P0 full objective, or READY.
