# Stage 4D-17GY Recovery Spectator Payment Trait Map Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates object property names for nested spectator timing payment trait maps before map extraction consumes them. Covered payloads are `Timing["pendingPayment"]["cost"]["powerByTrait"]` and `Timing["temporaryPaymentResources"][]` `generatedPowerByTrait` / `remainingPowerByTrait`. Map property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before trait-power comparisons consume those maps. This prevents duplicate JSON properties or spectator timing payment trait-map key drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplayTimingPaymentPowerTraitMapPropertyNameDrift` proves duplicate, whitespace-mutated and blank property names in pending-payment and temporary-resource trait maps produce explicit recovery diagnostics while preserving existing payment value parity checks.

Validation:

- Focused single test: new payment trait-map property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `226/226`.
- Adjacent recovery/opening/store-smoke: passed `807/807`.
- Backend full: passed `6172/6172`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator timing payment trait-map property-name shape slice. Remaining spectator snapshot nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
