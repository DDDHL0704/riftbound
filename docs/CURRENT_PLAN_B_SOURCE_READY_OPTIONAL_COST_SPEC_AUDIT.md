# Plan B Source-Ready Optional-Cost Spec Audit

Date: 2026-06-29

Project status: **NOT READY**.

## Scope

This slice moves Crescent Guard's ready-entry optional purple payment from a runtime effect-kind selector to executable behavior fields. The affected source paths are `CoreRuleEngine` play-card payment validation / unit entry resolution and `MatchSession` play-card prompt metadata.

The stable catalog effect id `CRESCENT_GUARD_NO_SPELL_VANILLA_PLAY_UNIT` remains in `CardBehaviorRegistry` and fixtures as card data. It is no longer referenced by `CoreRuleEngine` or `MatchSession` to decide whether the ready optional cost applies.

## Authority

- `data/official/card-catalog.zh-CN.json` row `UNL-122/219` 新月禁卫: if its controller played a spell this turn, they may pay purple as an additional cost so it enters ready.
- Existing rules evidence row `p2-preflight-play-crescent-guard-spell-ready-payment`.
- `docs/rules-authority-and-audit.md` requires shared engine mechanisms rather than single-card runtime branches.

## Implementation

- `CardBehaviorDefinition` now carries source-ready optional-cost metadata:
  - `SourceReadyAdditionalPowerCost`
  - `SourceReadyAdditionalPowerTrait`
  - `SourceReadyConditionKind`
  - `SourceReadyOptionalCostPayloadKey`
- `CardSourceReadyConditionKinds.ControllerPlayedSpellThisTurn` represents the existing spell-memory condition.
- `UNL-122/219` 新月禁卫 fills those fields with purple cost `1`, controller-played-spell condition, and legacy payload key `crescentGuardReadyOptionalCostPaid`.
- `CoreRuleEngine.TryBuildSourceReadyOptionalCost` validates the optional power cost and condition from those fields.
- `CoreRuleEngine.IsSourceReadyOptionalCostPaid` uses the same fields to make the source enter ready and preserve the existing payload key.
- `MatchSession` prompt optional-cost choices and payment-resource requirements use the same fields.

## Validation

- Red focused source guard: `PlayBehaviorSourceIdentityGuardTests.CrescentGuardReadyOptionalCostSourceUsesBehaviorFields` failed before implementation because `CoreRuleEngine` still contained `CrescentGuardReadyOptionalCostSourceEffectKind`.
- Green focused / representative behavior: `CrescentGuardReadyOptionalCostSourceUsesBehaviorFields|CrescentGuardReady` passed `4/4`.
- Adjacent / hidden-info representative gate: `PlayBehaviorSourceIdentityGuardTests|CrescentGuardReady|PaymentEngineCoverageAuditTests|ConformanceFixtureShapeTests|MatchRecovery` passed `2864/2864`.
- Backend full conformance: passed `9024/9024`.

## Holdbacks

This does not close complete source-ready optional-cost official breadth, complete payment matrix, cleanup/replacement duration, targeting-stack timing, full B0/P0 objective, or READY.
