# Plan B Source Steal Enemy Equipment Optional-Cost Evidence

Date: 2026-06-29

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `SFD·109/221` 阿克尚 states that the controller may pay two orange as an additional cost when playing him; if paid, they may move an enemy equipment to their base, control it until Akshan leaves, and attach it to him if it is an armament.
- Existing Akshan focused tests cover prompt exposure, typed orange payment including `RECYCLE_RUNE:*`, invalid-choice rejection, weapon attach, non-weapon movement/control, stale target no-effect, and source leaving field return.

## Engine Evidence

Before this slice, `CoreRuleEngine` and `MatchSession` used a dedicated `AkshanOrangeExtraEquipmentStealSourceEffectKind` runtime constant and direct `behavior.EffectKind` checks.

After this slice:

- `CardBehaviorRegistry` stores the enemy-equipment steal optional-cost data on the `SFD·109/221` behavior row.
- `CoreRuleEngine` validates the optional cost and legal equipment choice through `SourceStealEnemyEquipmentAdditionalPowerCost`, `SourceStealEnemyEquipmentAdditionalPowerTrait`, and `SourceStealEnemyEquipmentOptionalCostPrefix`.
- `CoreRuleEngine` resolves the post-entry move/control/attach path through the same fields and keeps the legacy event reason data-driven through `SourceStealEnemyEquipmentReason`.
- `MatchSession` exposes the prompt optional-cost choice and payment-resource requirements through the same fields.
- Leave-play cleanup now stores source-object id plus reason in a generic stolen-equipment marker and keeps legacy `AKSHAN_STOLEN_BY:` marker compatibility.

## Test Evidence

- `PlayBehaviorSourceIdentityGuardTests.OptionalCostRepresentativeSourcesUseBehaviorFieldsWhereAvailable` failed red before implementation because the engine still contained `AkshanOrangeExtraEquipmentStealSourceEffectKind`.
- The same guard passed after implementation and asserts `CoreRuleEngine` / `MatchSession` no longer contain the Akshan effect id.
- Existing Akshan focused tests passed with unchanged public behavior and payload shape.
- Adjacent / hidden-info representative gate `Akshan|PlayBehaviorSourceIdentityGuardTests|ArmedAssaulterHasteTemperedTests|PaymentEngineCoverageAuditTests|ConformanceFixtureShapeTests|MatchRecovery` passed `2917/2917`.
- Backend full conformance passed `9024/9024`.

## Non-Claims

This evidence does not claim complete optional-cost breadth, complete enemy-equipment steal official breadth, complete optional assemble matrix, full attach/control-until-leaves lifecycle breadth, complete LayerEngine / continuous effects matrix, complete PaymentEngine, P0 completion, or READY.
