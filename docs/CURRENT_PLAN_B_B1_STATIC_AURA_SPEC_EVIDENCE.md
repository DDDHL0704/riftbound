# Plan B B1 Static Aura Spec Evidence

更新时间：2026-06-24

## Evidence Summary

This evidence records the current B1 static-aura data-driven slices.

BehaviorSpec / catalog:

- `src/Riftbound.Contracts/BehaviorSpecs.cs` defines `StaticAuraSpec`, `StaticAuraKinds`, `StaticAuraTargetScopes`, and `StaticAuraParticipantScopes`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs` parses the current representative static-aura text patterns, including battlefield all-units, same-battlefield other-friendly, same-battlefield friendly-filtered, non-local other-friendly, and friendly-filtered unit power auras.
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` exposes parsed static auras through `BehaviorSpec.StaticAuras`.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` verifies Ornn, Tifarian Training Grounds, Soul Shepherd, Rumble, and Lee Sin representative static-aura specs.

Engine projection:

- `src/Riftbound.Engine/StaticAuraSpecRules.cs` builds a cached map from official card catalog `BehaviorSpec.StaticAuras`.
- `src/Riftbound.Engine/MatchSession.cs` no longer declares `ContinuousEffectStaticAuraCards`; object and battlefield `STATIC_AURA` projections resolve via `StaticAuraSpecRules`.
- `src/Riftbound.Engine/CoreRuleEngine.cs` resolves implemented static-aura power bonuses from `BehaviorSpec.StaticAuras`, including friendly-filtered and same-battlefield friendly-filtered target filters.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` includes a source-level guard that rejects reintroducing `ContinuousEffectStaticAuraCards` in `MatchSession`.

Recovery:

- `src/Riftbound.Engine/MatchRecovery.cs` validates object and battlefield static-aura source cards against the `BehaviorSpec` aura surface.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` updates the source-card drift expectation to the spec-driven diagnostic.

## Validation Evidence

- Latest focused static-aura catalog / same-battlefield friendly-filtered runtime: 2/2 passed.
- Latest adjacent static-aura / static-power / continuous-effect: 399/399 passed.
- Latest backend full: 8361/8361 passed.

## Remaining Evidence Needed

Before B1 can be called complete, later slices still need evidence for:

- Core runtime recompute removing the Ornn registry bridge.
- Multiple static auras and aura stacking beyond the current additive representatives.
- Interaction with until-end-of-turn power modifiers beyond existing representative coverage.
- Additional conditional subscopes and RULE_TEXT keyword grants.
- Full official static-aura breadth and `git diff --check` after each final slice.
