# Stage 4D-17GQ Recovery Spectator Timing Resolution History Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now requires spectator replay timing resolution-history item payloads to be objects and validates object property names for `Timing["battlefieldResolutions"][]` and `Timing["battleResolutions"][]`. Properties must have non-blank names without surrounding whitespace, and duplicate normalized property names are rejected before resolution-history field extraction consumes those payloads. This prevents duplicate JSON properties or spectator timing resolution-history property-name drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryPropertyNameDrift` proves duplicate, whitespace-mutated and blank property names in battlefield-resolution and battle-resolution timing item objects produce explicit recovery diagnostics.

Validation:

- Focused single test: new resolution-history property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `218/218`.
- Adjacent recovery/opening/store-smoke: passed `799/799`.
- Backend full: passed `6164/6164`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator timing resolution-history property-name shape slice. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
