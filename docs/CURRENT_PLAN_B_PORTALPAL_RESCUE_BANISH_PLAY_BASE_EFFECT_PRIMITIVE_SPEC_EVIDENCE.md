# Plan B Portalpal Rescue Banish Play Base Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `OGN·102/298` 传送门大营救 BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.BanishesTarget`, `EffectPhraseSpec.PlayDestinationZone`, and `EffectPhraseSpec.IgnoreCosts`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Portalpal Rescue's official `放逐一名友方单位` phrase into `BanishesTarget=true`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `一名友方单位` to `TargetScope=FRIENDLY_UNIT`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `打出到其所属的基地` to `PlayDestinationZone=BASE`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `无视费用` to `IgnoreCosts=true`.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` adds the `banish-then-play-target` primitive kind.
- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for `Banish` primitive metadata before falling back to existing P2 `CardBehaviorDefinition` metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Portalpal Rescue's parsed effect metadata and proves the primitive plan reason cites `BehaviorSpec.Effects`.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects.banishesTarget`, `playDestinationZone`, and `ignoreCosts` fields.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryPortalpalRescueBanishPlayBasePrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3413/3413 passed.
- Backend full conformance: 8934/8934 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Complete banish / play-to-base official breadth, battle / spell-duel timing breadth, control-zone movement breadth, automated evidence disposition, and legal official-deck score-victory replay.
- Project remains NOT READY.
