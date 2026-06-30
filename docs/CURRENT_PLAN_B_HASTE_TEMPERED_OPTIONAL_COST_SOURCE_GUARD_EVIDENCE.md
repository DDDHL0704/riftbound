# Plan B Haste + Tempered Optional-Cost Source Guard Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `SFD·002/221` 武装强袭者 states the source has both `急速` and `百炼`.
- The `急速` text supplies the additional 1 mana / 1 red power active-entry optional cost.
- The `百炼` text supplies the optional attach route when the source is played.

Existing engine data:

- `CardBehaviorRegistry` stores the Haste ready cost fields for `SFD·002/221`.
- `CardEquipmentKeywordRules` stores the `SFD·002/221` Tempered optional-attach representative boundary.
- Existing Armed Assaulter tests cover prompt exposure, typed red payment, same-command Haste + Tempered acceptance, stale prompt replay, invalid attach choices, and post-resolution attach behavior.

## Engine Evidence

Before this slice, `CoreRuleEngine` used a dedicated `ArmedAssaulterHasteTemperedSourceEffectKind` runtime constant and direct `behavior.EffectKind` check to decide whether the combined optional-cost branch applied.

After this slice:

- `CoreRuleEngine` no longer contains `ArmedAssaulterHasteTemperedSourceEffectKind`.
- `CoreRuleEngine` no longer contains the `ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE` runtime selector.
- `IsArmedAssaulterHasteTemperedOptionalAttachRepresentative` now requires `PlaysSourceToBaseAsUnit`, `HasHasteReadyEntryCost(behavior)`, and `CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary(behavior.CardNo)`.
- Haste payment math still uses `CardPermissionKeywordRules.TryBuildHasteReadyOptionalCost`.
- Tempered choice legality still uses `IsLegalTemperedOptionalAttachChoice`.

## Test Evidence

- `PlayBehaviorSourceIdentityGuardTests.OptionalCostRepresentativeSourcesUseBehaviorFieldsWhereAvailable` failed red before implementation because `CoreRuleEngine` still contained `ArmedAssaulterHasteTemperedSourceEffectKind`.
- The same guard now blocks reintroducing `ArmedAssaulterHasteTemperedSourceEffectKind` or `ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE` in `CoreRuleEngine` / `MatchSession`, and requires the Haste entry-cost helper plus Tempered boundary access.
- Focused Armed Assaulter representative regression passed `29/29`.
- Adjacent / hidden-info representative gate `ArmedAssaulter|TemperedEquipment|JaxTempered|HasteReady|PlayBehaviorSourceIdentityGuardTests|PaymentEngineCoverageAuditTests|ConformanceFixtureShapeTests|MatchRecovery` passed `2981/2981`.
- Backend full conformance passed `9024/9024`.

## Non-Claims

This evidence does not claim complete Haste official breadth, complete Tempered / assemble official breadth, complete attach lifecycle breadth, complete LayerEngine / continuous effects matrix, complete PaymentEngine, P0 completion, or READY.
