# Plan B Same-Battlefield Steadfast Static Keyword Aura Spec Evidence

更新时间：2026-06-25

## Evidence Summary

This evidence records the Taric representative for data-driven same-battlefield RULE_TEXT keyword grants.

Catalog / BehaviorSpec:

- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses `此处的其他友方单位获得{{...}}` into `StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsKeyword`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` now verifies `OGN·074/298` exposes one `StaticAuraSpec` with `GrantedKeyword=坚守`.

Engine projection:

- `src/Riftbound.Engine/MatchSession.cs` builds RULE_TEXT continuous effects through `BuildSameBattlefieldOtherFriendlyUnitsKeywordAuraEffects`.
- `src/Riftbound.Engine/StaticAuraSpecRules.cs` supplies `TryGetSameBattlefieldOtherFriendlyUnitsKeywordAura` from official BehaviorSpec data.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` verifies the projection targets only the other friendly unit at Taric's battlefield.

Combat resolution:

- `src/Riftbound.Engine/CoreRuleEngine.cs` resolves same-battlefield other-friendly keyword bonuses through `ResolveSameBattlefieldOtherFriendlyUnitsKeywordBonus`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` verifies a defending friendly unit granted `坚守` receives `keywordBonus=1`, `keyword=坚守`, and combat power 3 from base power 2.
- The same test verifies the enemy attacker remains at `keywordBonus=0`, no `staticPowerBonus`, combat power 5.

Existing fixture alignment:

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-taric-keyword-unit.fixture.json` still covers the ordinary hand-play path into the controller base, and now points the same-battlefield static keyword grant to the B2 RULE_TEXT aura representative.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, and `docs/CURRENT_P2_STATUS.md` now distinguish the covered Taric static keyword grant from the still-open defensive power and Bulwark ordering surfaces.

## Validation Evidence

- Focused static-aura parse / Taric representative: 2/2 passed.
- Adjacent StaticAura / StaticPower / ContinuousEffect / Steadfast / Taric / DeclareBattle / FullGameEndToEnd representatives: 543/543 passed.
- MatchRecovery hidden-information boundary: 1989/1989 passed.
- Backend full: 8532/8532 passed.

## Remaining Evidence Needed

- Full Taric official coverage still needs `壁垒` damage ordering and any broader defensive keyword interactions not covered by this representative.
- Full RULE_TEXT keyword grant scope coverage remains open beyond same-battlefield other-friendly units.
- The card-effect matrix FU row for `OGN·074/298` still requires a separate, matrix-aware blocker-reduction slice before its FU-level status is changed.
