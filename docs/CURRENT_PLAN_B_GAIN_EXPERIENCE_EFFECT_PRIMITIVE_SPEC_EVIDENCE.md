# Plan B Gain Experience Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the direct and friendly-field-count gain-experience BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.ExperienceCount`, `ExperienceCountFormula`, and `ExperienceCountMultiplier`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses direct `获得N经验` phrases into `ExperienceCount`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` selects the direct `当你打出我时，获得2经验。` phrase for `UNL-034/219` 暖春之使 instead of its `{{狩猎}}` reminder text.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `UNL-157/219` 严厉军士 dynamic `场上每有一名友方单位，便获得1经验` text into `ExperienceCountFormula=FRIENDLY_FIELD_UNIT_COUNT` and `ExperienceCountMultiplier=1`, while keeping `ExperienceCount=null`.
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` includes `BehaviorTemplateIds.GainExperience` in the safe existing-template mapping set when a representative P2 implementation exists.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` adds the `gain-experience` primitive kind.
- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for positive `ExperienceCount` or a complete friendly-field-count formula before emitting a primitive plan.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies `UNL-034/219` carries `ExperienceCount=2` from the direct gain phrase, `UNL-092/219` builds a ready `gain-experience` primitive with amount 1, and `UNL-157/219` builds a ready formula primitive with `AmountFormula=FRIENDLY_FIELD_UNIT_COUNT` and `AmountMultiplier=1`.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects` experience count and formula fields.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryGainExperiencePrimitiveMetadata`: 1/1 passed before formula follow-up.
- Follow-up focused `BehaviorSpecEffectPhrasesCarryGainExperiencePrimitiveMetadata|P4PrimitiveExecutorBuildsBasicActionPlansAndLeavesComplexRoutesDelegated`: 2/2 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3419/3419 passed.
- Backend full conformance: 8940/8940 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Dynamic experience formulas outside friendly-field-unit count, experience payment, activated abilities, Hunt conquest/hold experience, conditional delayed experience, and legal official-deck score-victory replay breadth.
- Project remains NOT READY.
