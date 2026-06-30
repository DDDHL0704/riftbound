# Plan B Source Boon Draw Behavior Fields Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the runtime effect-kind selector for `OGN·061/298` 魄罗牧者's current conditional source-boon draw representative branch.

The stable catalog effect id `PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT` remains in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. It is no longer referenced by `CoreRuleEngine` to decide whether the controlled-Poro source-boon draw branch applies.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGN·061/298` 魄罗牧者: when the source is played while the controller controls a Poro unit, grant boon to the source and draw one card.
- Existing direct engine regression `P79PoroHerderGrantsBoonAndDrawsWhenControllerHasPoro` covers the satisfied condition and server draw.
- Existing fixture `p2-preflight-play-poro-herder-no-poro-static.fixture.json` covers the unsatisfied condition.

## Implementation

- `CardBehaviorDefinition` now carries source-boon draw metadata:
  - `SourceBoonConditionKind`
  - `SourceBoonRequiredControlledUnitTag`
  - `SourceBoonDrawCount`
  - `SourceBoonDrawEffectKind`
- `CardSourceBoonConditionKinds.ControllerControlsFaceUpUnitWithTag` describes the implemented condition.
- `OGN·061/298` fills those fields with required controlled unit tag `魄罗`, draw count `1`, and legacy public draw effect kind `PORO_HERDER_BOON_DRAW`.
- `CoreRuleEngine.ShouldGrantBoonToSourceUnit` resolves the branch through those fields and the shared `ControllerControlsFaceUpUnitWithTag` helper, which ignores face-down and standby units.
- The existing generic source-boon resolver still applies the boon through `ApplyBoon`; it now also applies `SourceBoonDrawCount` through the authoritative draw path.

## Validation

- Baseline before this slice: backend full conformance passed `9026/9026`.
- Red focused source guard failed before implementation because `CardSourceBoonConditionKinds.ControllerControlsFaceUpUnitWithTag`, `SourceBoonRequiredControlledUnitTag`, and `SourceBoonDrawCount` did not exist.
- Green focused representative gate: `PoroHerderBoonDrawPlaySourceUsesBehaviorFields|PoroHerderSourceBoonDrawCarriesOfficialControlledPoroCondition|P79PoroHerderGrantsBoonAndDrawsWhenControllerHasPoro` passed `3/3`.
- Adjacent / hidden-info representative gate: `PlayBehaviorSourceIdentityGuardTests|PoroHerder|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3026/3026`.
- Backend full conformance passed `9027/9027`.

## Holdbacks

This does not close complete source-boon official breadth, complete play-trigger routing, complete PaymentEngine, P0 full objective, or READY.
