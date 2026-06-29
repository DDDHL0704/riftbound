# Plan B Zaun Bodyguard Recall Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `OGN·188/298` 祖安保镖 BehaviorSpec effect primitive metadata slice.

2026-06-29 follow-up: the same recall family also records `OGN·172/298` 责退 target-guard de-hardcoding. The engine still resolves the existing `REPRIMAND_RETURN_BATTLEFIELD_UNIT_TO_HAND` stack item through the shared `ReturnsTargetToHand` resolution path, but target legality and prompt filtering no longer branch on that effect id in `CoreRuleEngine` or `MatchSession`. The broader 2026-06-29 follow-up routes return-to-hand, move-to-base, destroy, gain-control, and power-swap field-unit primitive targets through the same `RequiresVisibleFieldUnitPrimitiveTarget` guard.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.ReturnsTargetToHand` and `EffectPhraseSpec.ReturnDestinationZone`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Zaun Bodyguard's official `返回其所属的手牌` phrase into `ReturnsTargetToHand=true`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `从战场上` to `TargetScope=BATTLEFIELD_UNIT`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `手牌` to `ReturnDestinationZone=HAND`.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` remains the catalog source for `OGN·172/298` 责退 with `ReturnsTargetToHand=true` and default `TargetScope=BATTLEFIELD_UNIT`.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` adds the `return-target-to-hand` primitive kind.
- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for `Recall` primitive metadata before falling back to existing P2 `CardBehaviorDefinition` metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Zaun Bodyguard's parsed effect metadata and proves the primitive plan reason cites `BehaviorSpec.Effects`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` replaces the Reprimand-specific target guard with `RequiresVisibleFieldUnitPrimitiveTarget`, keyed by field-unit behavior primitives plus unit target scope.
- `src/Riftbound.Engine/MatchSession.cs` uses the same shared condition for ActionPrompt target filtering, so prompt candidates and authoritative validation stay aligned.
- `tests/Riftbound.ConformanceTests/ReprimandReturnToHandGuardTests.cs` adds a source guard proving `CoreRuleEngine` and `MatchSession` no longer contain the Reprimand effect id branch, plus a legacy untyped public-unit compatibility test.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects.returnsTargetToHand` and `returnDestinationZone` fields.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryZaunBodyguardRecallPrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3414/3414 passed.
- Backend full conformance: 8935/8935 passed.
- Dev UI catalog contract build: passed.
- 2026-06-29 focused `ReprimandReturnToHandGuardTests`: 11/11 passed.
- 2026-06-29 adjacent `ReprimandReturnToHandGuardTests|GustReturnToHandTests|BehaviorSpecEffectPhrasesCarryZaunBodyguardRecallPrimitiveMetadata|P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen`: 236/236 passed.
- 2026-06-29 backend full conformance: 9022/9022 passed.
- 2026-06-29 shared primitive target-guard follow-up: red/green focused `PrimitiveTargetGuardSourceTests` 1/1 passed; source guard + Reprimand source guard 2/2 passed; affected representative move/return/destroy/control/power-swap guards 105/105 passed; adjacent primitive/catalog/recovery 2401/2401 passed; backend full 9023/9023 passed.

## Remaining Evidence Needed

- Complete recall / return-to-hand official breadth, on-play trigger breadth, control-zone movement breadth, hidden-info owner-hand placement breadth, and legal official-deck score-victory replay.
- Project remains NOT READY.
