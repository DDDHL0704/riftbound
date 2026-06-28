# Plan B Secret Art Mercy Boon Effect Primitive Spec Evidence

更新时间：2026-06-28

## Evidence Summary

This evidence records the `OGN·053/298` 秘奥义！慈悲度魂落 BehaviorSpec effect primitive metadata slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` adds optional `EffectPhraseSpec.GrantsBoon` and `EffectPhraseSpec.BoonPowerBonusAmount`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses Secret Art Mercy's official `给予一名友方单位增益` phrase into `GrantsBoon=true`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps `一名友方单位` to `TargetScope=FRIENDLY_UNIT`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` maps the official `{{S}}+1增益` reminder to `BoonPowerBonusAmount=1`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` prevents boon reminder/global-boon `{{S}}+1` text from becoming a direct `TempMight` effect.

Engine primitive plan:

- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` adds the `grant-boon` primitive kind.
- `src/Riftbound.Engine/BehaviorTemplatePrimitiveExecutor.cs` checks `BehaviorSpec.Effects` for `Boon` primitive metadata before falling back to existing P2 `CardBehaviorDefinition` metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Secret Art Mercy's parsed effect metadata, proves the primitive plan reason cites `BehaviorSpec.Effects`, and locks that Secret Art Mercy does not produce a direct `TempMight` effect.

Protocol/frontend:

- `src/Riftbound.DevUi/src/types/catalog.ts` mirrors the new optional `BehaviorSpec.effects.grantsBoon` and `boonPowerBonusAmount` fields.

## Validation Evidence

- Focused `BehaviorSpecEffectPhrasesCarrySecretArtMercyBoonPrimitiveMetadata`: 1/1 passed.
- Adjacent `BehaviorTemplate|CardCatalogBaseline|ConformanceFixtureRunner`: 3415/3415 passed.
- Backend full conformance: 8936/8936 passed.
- Dev UI catalog contract build: passed.

## Remaining Evidence Needed

- Complete boon official breadth, repeat-boon stacking breadth, boon-trigger breadth, global boon modifier duration cleanup, and legal official-deck score-victory replay.
- Project remains NOT READY.
