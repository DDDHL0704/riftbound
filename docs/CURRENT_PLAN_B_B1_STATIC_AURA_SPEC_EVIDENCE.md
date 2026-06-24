# Plan B B1 Static Aura Spec Evidence

更新时间：2026-06-24

## Evidence Summary

This evidence records the first B1 static-aura data-driven slice.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` defines `StaticAuraSpec`, `StaticAuraKinds`, `StaticAuraTargetScopes`, and `StaticAuraParticipantScopes`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses the two current representative static-aura text patterns.
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` exposes parsed static auras through `BehaviorSpec.StaticAuras`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Ornn and Tifarian Training Grounds static-aura specs.

Engine projection:

- `src/Riftbound.Engine/StaticAuraSpecRules.cs` builds a cached map from official card catalog `BehaviorSpec.StaticAuras`.
- `src/Riftbound.Engine/MatchSession.cs` no longer declares `ContinuousEffectStaticAuraCards`; object and battlefield `STATIC_AURA` projections resolve via `StaticAuraSpecRules`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` includes a source-level guard that rejects reintroducing `ContinuousEffectStaticAuraCards` in `MatchSession`.

Recovery:

- `src/Riftbound.Engine/MatchRecovery.cs` validates battlefield static-aura source cards against the `BehaviorSpec` aura surface.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` updates the source-card drift expectation to the spec-driven diagnostic.

## Validation Evidence

- Focused static-aura catalog/projection guard: 2/2 passed.
- Adjacent static-aura / Ornn / LayerEngine battlefield static aura / battlefield static power: 217/217 passed.

## Remaining Evidence Needed

Before B1 can be called complete, later slices still need evidence for:

- Core runtime recompute removing the Ornn registry bridge.
- Combat static power bonus reading `StaticAuraSpec`.
- Multiple static auras and aura stacking.
- Interaction with until-end-of-turn power modifiers beyond existing representative coverage.
- Full backend conformance pass and `git diff --check` after the final B1 slice.
