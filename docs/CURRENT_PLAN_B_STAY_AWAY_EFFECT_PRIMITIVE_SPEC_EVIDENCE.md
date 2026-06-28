# Plan B Stay Away Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `UNL-042/219` 走开 BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec` primitive fields: `TargetScope`, `DrawCount`, `StatusEffectId`, and `ConditionKind`.
- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds `BehaviorEffectConditionKinds.PlayedFromHand`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Stay Away's official stun phrase into `TargetScope=ANY_UNIT` and `StatusEffectId=STUNNED`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Stay Away's hand-play draw phrase into `DrawCount=1` and `ConditionKind=PLAYED_FROM_HAND`.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` now checks `BehaviorSpec.Effects` before falling back to existing P2 `CardBehaviorDefinition` metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Stay Away's parsed effect metadata and proves the primitive plan reasons cite `BehaviorSpec.Effects` for both draw and stun primitives.
- The existing `p2-preflight-play-stay-away-stun-draw-stack` fixture remains green through the adjacent `ConformanceFixtureRunner` run, proving this metadata slice did not change current authoritative stack resolution.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects` fields.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryStayAwayStunDrawPrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3409/3409 passed.
- Backend full conformance: 8930/8930 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Standby reaction play route for Stay Away.
- Full swift/reaction timing breadth, complete stun/draw hidden-info breadth, and legal official-deck score-victory replay.
- Project remains NOT READY.
