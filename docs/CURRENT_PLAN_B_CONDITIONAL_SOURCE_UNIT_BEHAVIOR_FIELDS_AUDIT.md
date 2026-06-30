# Plan B Conditional Source Unit Behavior Fields Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the remaining runtime effect-kind selectors for the current conditional source-unit power / keyword representative branch:

- `UNL-004/219` 晋升信徒
- `UNL-108/219` 狡猾的蝾螈
- `OGN·019/298` 肆虐狂魂

The stable catalog effect ids remain in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. They are no longer referenced by `CoreRuleEngine` to decide whether the conditional source-unit power or keyword branch applies.

## Authority

- `data/official/card-catalog.zh-CN.json` row `UNL-004/219` 晋升信徒: if the controller spent at least 4 cost to play a spell this turn, the source gets `S+4`.
- `data/official/card-catalog.zh-CN.json` row `UNL-108/219` 狡猾的蝾螈: if the controller gained experience this turn, the source gets `S+1` and `游走`.
- `data/official/card-catalog.zh-CN.json` row `OGN·019/298` 肆虐狂魂: if the controller discarded a hand card this turn, the source gets `强攻` and `游走`.

## Implementation

- `CardBehaviorDefinition` now carries conditional source-unit metadata:
  - `ConditionalSourceUnitConditionKind`
  - `ConditionalSourceUnitPowerBonus`
  - `ConditionalSourceUnitTags`
- `CardConditionalSourceUnitConditionKinds` defines the implemented turn-memory conditions:
  - `ControllerPlayedFourPlusCostSpellThisTurn`
  - `ControllerGainedExperienceThisTurn`
  - `ControllerDiscardedHandCardThisTurn`
- `CoreRuleEngine.ResolveConditionalSourceUnitPowerBonus` and `ResolveConditionalSourceUnitTags` now read those fields and the existing turn-memory markers instead of comparing `behavior.EffectKind` against card-specific constants.
- Existing event payloads, source unit base power, printed tags, and turn-memory markers remain unchanged.

## Validation

- Baseline before this slice: backend full conformance passed `9024/9024`.
- Red focused source guard: `PlayBehaviorSourceIdentityGuardTests.ConditionalSourceUnitPowerAndTagsUseBehaviorFields` failed before implementation because `CoreRuleEngine` still contained `AscendedBelieverConditionalSourceEffectKind`.
- Green focused / fixture representative gate: `ConditionalSourceUnitPowerAndTagsUseBehaviorFields|ConditionalSourceUnitPowerAndTagsCarryOfficialTurnMemoryFields|ConformanceFixtureRunnerTests` passed `3114/3114`.
- Adjacent / hidden-info representative gate: `PlayBehaviorSourceIdentityGuardTests|ConditionalSourceUnitPowerAndTagsCarryOfficialTurnMemoryFields|ConformanceFixtureRunnerTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `5827/5827`.
- Backend full conformance after implementation: passed `9025/9025`.

## Holdbacks

This does not close complete conditional source-unit official breadth, complete source-object continuous effect breadth, complete keyword grant/removal `RULE_TEXT` layer breadth, complete PaymentEngine, P0 full objective, or READY.
