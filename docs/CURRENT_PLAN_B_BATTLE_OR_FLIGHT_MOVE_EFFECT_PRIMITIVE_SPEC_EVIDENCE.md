# Plan B Battle Or Flight Move Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `OGN·168/298` 战或逃 BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.MovesTarget`, `MoveCount`, and `MoveDestination`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Battle Or Flight's official `移动` phrase into `MovesTarget=true`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `一名单位从战场上` to `TargetScope=BATTLEFIELD_UNIT`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `一名` to `MoveCount=1`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `其所属的基地` to `MoveDestination=OWNER_BASE`.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` adds the `move-target` primitive kind.
- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for fully specified `Move` primitive metadata before falling back to existing P2 `CardBehaviorDefinition` metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Battle Or Flight's parsed effect metadata and proves the primitive plan reason cites `BehaviorSpec.Effects`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` adds `OGN·168/298` to ready primitive smoke coverage while keeping destination-ambiguous `OGN·043/298` delegated.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects.movesTarget`, `moveCount`, and `moveDestination` fields.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryBattleOrFlightMovePrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3417/3417 passed.
- Backend full conformance: 8938/8938 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Multi-target movement, swap movement, controller-chosen destinations, Roam movement, combat movement, attachment-following movement breadth, and legal official-deck score-victory replay.
- Project remains NOT READY.
