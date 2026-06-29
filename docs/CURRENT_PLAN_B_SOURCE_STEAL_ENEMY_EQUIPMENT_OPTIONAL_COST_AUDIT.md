# Plan B Source Steal Enemy Equipment Optional-Cost Audit

Date: 2026-06-29

Project status: **NOT READY**.

## Scope

This slice moves Akshan's orange-extra enemy-equipment steal optional-cost selector from a runtime effect-kind branch to executable behavior fields. The affected source paths are `CoreRuleEngine` play-card optional-cost validation / post-entry stack resolution and `MatchSession` play-card prompt metadata.

The stable catalog effect id `AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT` remains in `CardBehaviorRegistry` and fixtures as card data. It is no longer referenced by `CoreRuleEngine` or `MatchSession` to decide whether the enemy-equipment steal optional cost applies.

## Authority

- `data/official/card-catalog.zh-CN.json` row `SFD·109/221` 阿克尚: controller may pay two orange as an additional cost when playing him; if paid, they may move an enemy equipment to their base, control it until Akshan leaves, and attach it to Akshan if it is an armament.
- Existing evidence row `p2-preflight-play-akshan-no-optional-assemble-no-orange-extra`.
- Existing focused Akshan orange-extra equipment steal tests cover payment, prompt filtering, move/control, armament attach, stale resolution, and leave-play return behavior.

## Implementation

- `CardBehaviorDefinition` now carries source enemy-equipment steal optional-cost metadata:
  - `SourceStealEnemyEquipmentAdditionalPowerCost`
  - `SourceStealEnemyEquipmentAdditionalPowerTrait`
  - `SourceStealEnemyEquipmentOptionalCostPrefix`
  - `SourceStealEnemyEquipmentReason`
- `SFD·109/221` 阿克尚 fills those fields with orange cost `2`, legacy optional-cost prefix `AKSHAN_STEAL_EQUIPMENT:`, and legacy reason `AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL`.
- `CoreRuleEngine.TryBuildSourceStealEnemyEquipmentOptionalCost` validates the optional power cost and legal enemy equipment choice from those fields.
- `CoreRuleEngine.TryResolveSourceStealEnemyEquipment` uses the same fields after the source unit enters to revalidate the source object, move/control the selected equipment, attach armaments, and write the legacy reason.
- Stolen-equipment leave-play cleanup now writes a generic marker that carries the behavior reason, while still reading the previous `AKSHAN_STOLEN_BY:` marker for compatibility.
- `MatchSession` prompt optional-cost choices and payment-resource requirements use the same behavior fields.

## Validation

- Red focused source guard: `PlayBehaviorSourceIdentityGuardTests.OptionalCostRepresentativeSourcesUseBehaviorFieldsWhereAvailable` failed before implementation because `CoreRuleEngine` still contained `AkshanOrangeExtraEquipmentStealSourceEffectKind`.
- Green focused / representative behavior: `OptionalCostRepresentativeSourcesUseBehaviorFieldsWhereAvailable|Akshan` passed `32/32`.
- Adjacent / hidden-info representative gate: `Akshan|PlayBehaviorSourceIdentityGuardTests|ArmedAssaulterHasteTemperedTests|PaymentEngineCoverageAuditTests|ConformanceFixtureShapeTests|MatchRecovery` passed `2917/2917`.
- Backend full conformance: passed `9024/9024`.

## Holdbacks

This does not close complete enemy-equipment steal official breadth, complete optional assemble matrix, full attach/control-until-leaves lifecycle breadth, complete LayerEngine / continuous effects matrix, complete PaymentEngine, P0 full objective, or READY.
