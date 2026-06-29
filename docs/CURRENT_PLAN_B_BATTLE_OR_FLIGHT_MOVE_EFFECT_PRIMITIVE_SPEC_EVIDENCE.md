# Plan B Battle Or Flight Move Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `OGN·168/298` 战或逃 BehaviorSpec effect primitive metadata slice.

2026-06-29 follow-up: `CoreRuleEngine` / `MatchSession` now use `RequiresVisibleFieldUnitPrimitiveTarget` for move primitive target validation/prompt filtering. The source guard proves `BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE` is no longer a runtime target-filtering branch in those files; the catalog, fixtures, and docs may still use that effect id as evidence identity.

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
- 2026-06-29 runtime target-guard follow-up: red/green focused `PrimitiveTargetGuardSourceTests` 1/1 passed; source guard + Reprimand source guard 2/2 passed; affected representative move/return/destroy/control/power-swap guards 105/105 passed; adjacent primitive/catalog/recovery 2401/2401 passed; backend full 9023/9023 passed.

## Remaining Evidence Needed

- Multi-target movement, swap movement, controller-chosen destinations, Roam movement, combat movement, attachment-following movement breadth, and legal official-deck score-victory replay.
- Project remains NOT READY.
