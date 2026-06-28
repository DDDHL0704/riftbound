# Plan B Cleave Temp Might Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `OGN·004/298` 顺劈 BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.PowerModifierAmount`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Cleave's official `{{S}}+3` phrase into `PowerModifierAmount=3`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps Cleave's target text to `TargetScope=ANY_UNIT`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `如果它是进攻方` to `ConditionKind=TARGET_IS_ATTACKING`.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for `TempMight` primitive metadata before falling back to existing P2 `CardBehaviorDefinition` metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Cleave's parsed effect metadata and proves the primitive plan reason cites `BehaviorSpec.Effects`.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects.powerModifierAmount` field.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryCleaveTempMightPrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3410/3410 passed.
- Backend full conformance: 8931/8931 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Full Overwhelm / battle damage semantics, spell-duel timing breadth, LayerEngine duration cleanup breadth, and legal official-deck score-victory replay.
- Project remains NOT READY.
