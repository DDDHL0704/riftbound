# Plan B Hostile Takeover Control Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `SFD·202/221` 恶意收购 BehaviorSpec control primitive metadata slice.

2026-06-29 follow-up: `CoreRuleEngine` / `MatchSession` now use `RequiresVisibleFieldUnitPrimitiveTarget` for control primitive target validation/prompt filtering. The source guard proves `HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT` is no longer a runtime target-filtering branch in those files; the catalog, fixtures, and docs may still use that effect id as evidence identity.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional control fields to `EffectPhraseSpec`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Hostile Takeover's official control text into `TargetScope=ENEMY_BATTLEFIELD_UNIT`, `GainsControl=true`, `ControlDestinationZone=BATTLEFIELD`, and `ReadiesTarget=true`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses its end-turn cleanup text into `ControlDuration=UNTIL_END_OF_TURN`, `ControlReturnDestinationZone=BASE`, and `ControlReturnCountsAsMove=false`.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` adds the `gain-control-target` primitive kind.
- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for complete control target/destination metadata before emitting the primitive.
- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` treats Hostile Takeover's parsed end-turn `Recall` secondary template as covered by the control primitive metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Hostile Takeover's parsed control metadata and proves the primitive plan reason cites `BehaviorSpec.Effects`.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects` control fields.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarryHostileTakeoverControlPrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3419/3419 passed.
- Backend full conformance: 8940/8940 passed.
- Dev UI catalog contract build: passed.
- 2026-06-29 runtime target-guard follow-up: red/green focused `PrimitiveTargetGuardSourceTests` 1/1 passed; source guard + Reprimand source guard 2/2 passed; affected representative move/return/destroy/control/power-swap guards 105/105 passed; adjacent primitive/catalog/recovery 2401/2401 passed; backend full 9023/9023 passed.

## Remaining Evidence Needed

- Hostile Takeover battle/conquer branch after control is gained, Reversal stack-spell control, Forced Conscription optional experience branch, immediate control-and-recall variants, full control-zone movement lifecycle, and legal official-deck score-victory replay breadth.
- Project remains NOT READY.
