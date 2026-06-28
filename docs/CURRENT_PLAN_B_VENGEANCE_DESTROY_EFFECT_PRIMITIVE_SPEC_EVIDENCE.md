# Plan B Vengeance Destroy Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `OGN·229/298` 复仇 BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.DestroysTarget`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Vengeance's official `摧毁一名单位` phrase into `DestroysTarget=true`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `一名单位` to `TargetScope=ANY_UNIT`.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for `Destroy` primitive metadata before falling back to existing P2 `CardBehaviorDefinition` metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Vengeance's parsed effect metadata and proves the primitive plan reason cites `BehaviorSpec.Effects`.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects.destroysTarget` field.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryVengeanceDestroyPrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3412/3412 passed.
- Backend full conformance: 8933/8933 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Full destroy replacement, last-breath trigger breadth, cleanup ordering, and legal official-deck score-victory replay.
- Project remains NOT READY.
