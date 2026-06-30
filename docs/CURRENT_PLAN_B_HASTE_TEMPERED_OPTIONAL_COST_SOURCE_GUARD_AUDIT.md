# Plan B Haste + Tempered Optional-Cost Source Guard Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the remaining Armed Assaulter haste + tempered optional-cost runtime effect-kind selector from `CoreRuleEngine`.

The stable catalog effect id `ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE` remains in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. It is no longer referenced by `CoreRuleEngine` or `MatchSession` to decide whether the combined `HASTE_READY` + `TEMPERED_ATTACH:*` optional-cost branch applies.

## Authority

- `data/official/card-catalog.zh-CN.json` row `SFD·002/221` 武装强袭者 has both official keyword lines:
  - `{{急速}}`: the controller may pay 1 mana and 1 red power as an additional cost so the source enters active.
  - `{{百炼}}`: when the source is played, the controller may assemble one of their armaments to it with reduced assemble cost, including already attached armaments.
- Existing Haste evidence covers `CardBehaviorDefinition.HasteReadyManaCost=1`, `HasteReadyPowerCost=1`, and `HasteReadyPowerTrait=red` for `SFD·002/221`.
- Existing Tempered evidence covers `CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary("SFD·002/221")`.

## Implementation

- `CoreRuleEngine` removed the private `ArmedAssaulterHasteTemperedSourceEffectKind` constant.
- `IsArmedAssaulterHasteTemperedOptionalAttachRepresentative` now derives the branch from shared behavior and boundary data:
  - the source is played as a unit;
  - the behavior has an implemented Haste ready entry cost;
  - the source card is inside the Tempered optional-attach representative boundary.
- Existing optional-cost validation still requires exactly one `HASTE_READY` option and exactly one legal `TEMPERED_ATTACH:<equipmentObjectId>` option.
- Existing prompt exposure remains unchanged: `MatchSession` already exposes Haste choices from behavior fields and Tempered choices from the same representative boundary.

## Validation

- Baseline before this slice: backend full conformance passed `9024/9024`.
- Red focused source guard: `PlayBehaviorSourceIdentityGuardTests.OptionalCostRepresentativeSourcesUseBehaviorFieldsWhereAvailable` failed before implementation because `CoreRuleEngine` still contained `ArmedAssaulterHasteTemperedSourceEffectKind`.
- Green focused / representative behavior: `OptionalCostRepresentativeSourcesUseBehaviorFieldsWhereAvailable|ArmedAssaulter` passed `29/29`.
- Adjacent / hidden-info representative gate: `ArmedAssaulter|TemperedEquipment|JaxTempered|HasteReady|PlayBehaviorSourceIdentityGuardTests|PaymentEngineCoverageAuditTests|ConformanceFixtureShapeTests|MatchRecovery` passed `2981/2981`.
- Backend full conformance after implementation: passed `9024/9024`.

## Holdbacks

This does not close complete Haste official breadth, complete Tempered / assemble official breadth, complete attach lifecycle breadth, complete LayerEngine / continuous effects matrix, complete PaymentEngine, P0 full objective, or READY.
