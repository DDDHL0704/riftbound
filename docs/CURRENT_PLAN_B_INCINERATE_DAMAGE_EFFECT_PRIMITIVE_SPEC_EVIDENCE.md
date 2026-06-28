# Plan B Incinerate Damage Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `OGS·003/024` 焚烧 BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.DamageAmount`.
- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `BehaviorEffectConditionKinds.None`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Incinerate's official `造成2点伤害` phrase into `DamageAmount=2`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `战场上的一名单位` to `TargetScope=BATTLEFIELD_UNIT`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` records unconditional damage as `ConditionKind=NONE`.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for `Damage` primitive metadata before falling back to existing P2 `CardBehaviorDefinition` metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Incinerate's parsed effect metadata and proves the primitive plan reason cites `BehaviorSpec.Effects`.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects.damageAmount` field.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryIncinerateDamagePrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3411/3411 passed.
- Backend full conformance: 8932/8932 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Full damage prevention/replacement, lethal cleanup breadth, spell-duel timing breadth, and legal official-deck score-victory replay.
- Project remains NOT READY.
