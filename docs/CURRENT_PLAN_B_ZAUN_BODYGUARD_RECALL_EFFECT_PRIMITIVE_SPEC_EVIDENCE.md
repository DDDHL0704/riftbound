# Plan B Zaun Bodyguard Recall Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `OGN·188/298` 祖安保镖 BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.ReturnsTargetToHand` and `EffectPhraseSpec.ReturnDestinationZone`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Zaun Bodyguard's official `返回其所属的手牌` phrase into `ReturnsTargetToHand=true`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `从战场上` to `TargetScope=BATTLEFIELD_UNIT`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `手牌` to `ReturnDestinationZone=HAND`.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` adds the `return-target-to-hand` primitive kind.
- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for `Recall` primitive metadata before falling back to existing P2 `CardBehaviorDefinition` metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Zaun Bodyguard's parsed effect metadata and proves the primitive plan reason cites `BehaviorSpec.Effects`.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects.returnsTargetToHand` and `returnDestinationZone` fields.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryZaunBodyguardRecallPrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3414/3414 passed.
- Backend full conformance: 8935/8935 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Complete recall / return-to-hand official breadth, on-play trigger breadth, control-zone movement breadth, hidden-info owner-hand placement breadth, and legal official-deck score-victory replay.
- Project remains NOT READY.
