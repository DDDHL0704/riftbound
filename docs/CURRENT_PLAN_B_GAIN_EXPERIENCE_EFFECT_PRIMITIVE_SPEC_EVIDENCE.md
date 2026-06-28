# Plan B Gain Experience Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the direct gain-experience BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.ExperienceCount`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses direct `获得N经验` phrases into `ExperienceCount`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` selects the direct `当你打出我时，获得2经验。` phrase for `UNL-034/219` 暖春之使 instead of its `{{狩猎}}` reminder text.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` leaves dynamic `每有一名友方单位，便获得1经验` text with `ExperienceCount=null`.
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` includes `BehaviorTemplateIds.GainExperience` in the safe existing-template mapping set when a representative P2 implementation exists.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` adds the `gain-experience` primitive kind.
- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for positive `ExperienceCount` before emitting a primitive plan.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies `UNL-034/219` carries `ExperienceCount=2` from the direct gain phrase, `UNL-092/219` builds a ready `gain-experience` primitive with amount 1, and `UNL-157/219` dynamic friendly-unit-count experience remains delegated.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects.experienceCount` field.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryGainExperiencePrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3418/3418 passed.
- Backend full conformance: 8939/8939 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Dynamic experience formulas, experience payment, activated abilities, Hunt conquest/hold experience, conditional delayed experience, and legal official-deck score-victory replay breadth.
- Project remains NOT READY.
