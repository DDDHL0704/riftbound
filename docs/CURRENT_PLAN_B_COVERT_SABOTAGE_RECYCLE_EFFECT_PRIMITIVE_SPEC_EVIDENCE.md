# Plan B Covert Sabotage Recycle Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `OGN·156/298` 暗中破坏 BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.RecyclesTarget`, `RecycleSourceZone`, `RecycleDestinationZone`, and `TargetForbiddenTag`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Covert Sabotage's official `回收` phrase into `RecyclesTarget=true`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `对手` + `手牌` to `TargetScope=OPPONENT_HAND_CARD`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `非单位卡牌` to `TargetForbiddenTag=CARD_TYPE:UNIT`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps hand recycle routing to `RecycleSourceZone=HAND` and `RecycleDestinationZone=MAIN_DECK`.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` adds the `recycle-target` primitive kind.
- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for `Recycle` primitive metadata before falling back to existing P2 `CardBehaviorDefinition` metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Covert Sabotage's parsed effect metadata and proves the primitive plan reason cites `BehaviorSpec.Effects`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` moves `OGN·156/298` from delegated primitive smoke coverage into ready primitive smoke coverage.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects.recyclesTarget`, `recycleSourceZone`, `recycleDestinationZone`, and `targetForbiddenTag` fields.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryCovertSabotageRecyclePrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3416/3416 passed.
- Backend full conformance: 8937/8937 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Complete recycle official breadth, hidden hand reveal UX/protocol breadth, owner deck placement breadth, and legal official-deck score-victory replay.
- Project remains NOT READY.
