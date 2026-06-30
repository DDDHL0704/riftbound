# Plan B Balanced Disciple Source Draw Behavior Fields Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the runtime effect-kind selector for `UNL-097/219` 均衡门徒's current source-unit conditional draw representative branch.

The stable catalog effect id `BALANCED_DISCIPLE_NO_OTHER_POWER_VANILLA_PLAY_UNIT` remains in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. It is no longer referenced by `CoreRuleEngine` to decide whether the other-controlled-unit-power draw branch applies.

## Authority

- `data/official/card-catalog.zh-CN.json` row `UNL-097/219` 均衡门徒: when the source is played, if the controller's other units have total power at least 5, draw one card.
- Existing fixture `p2-preflight-play-balanced-disciple-other-power-draw.fixture.json` covers the satisfied condition and server draw.
- Existing fixture `p2-preflight-play-balanced-disciple-no-other-power-vanilla-unit.fixture.json` covers the unsatisfied condition.

## Implementation

- `CardBehaviorDefinition` now carries source draw metadata:
  - `SourceDrawConditionKind`
  - `SourceDrawCount`
  - `SourceDrawRequiredOtherControlledUnitPower`
- `CardSourceDrawConditionKinds.OtherControlledUnitPowerAtLeast` describes the implemented condition.
- `UNL-097/219` fills those fields with threshold `5` and draw count `1`.
- `CoreRuleEngine` resolves the branch through `TryResolveSourceUnitConditionalDraw`, reusing the existing authoritative `SumOtherControlledUnitPower` helper and excluding the source object.

## Validation

- Baseline before this slice: backend full conformance passed `9025/9025`.
- Red focused source guard: `PlayBehaviorSourceIdentityGuardTests.BalancedDiscipleOtherPowerDrawPlaySourceUsesBehaviorFields` failed before implementation because `CoreRuleEngine` still contained `BalancedDiscipleOtherPowerDrawSourceEffectKind`.
- Green focused representative gate: `BalancedDiscipleOtherPowerDrawPlaySourceUsesBehaviorFields|BalancedDiscipleSourceDrawCarriesOfficialOtherPowerCondition|CoreRuleEnginePlaysBalancedDiscipleOtherPowerDraw` passed `3/3`.
- Adjacent / hidden-info representative gate: `PlayBehaviorSourceIdentityGuardTests|BalancedDisciple|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3024/3024`.
- Backend full conformance passed `9026/9026`.

## Holdbacks

This does not close complete conditional source draw official breadth, complete play-trigger routing, complete PaymentEngine, P0 full objective, or READY.
