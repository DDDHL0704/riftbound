# Stage 4D-17HD Recovery Spectator Timing Map Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates property names for the top-level spectator snapshot `Timing` map before scalar/list timing, pending-window, continuous-effect, trigger-queue and battle timing comparisons consume those keys. Map property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before timing field validation continues.

Test change: `RecoveryValidatorRejectsSpectatorReplayTimingMapPropertyNameDrift` proves duplicate normalized timing keys, whitespace-mutated timing keys and blank timing keys produce explicit recovery diagnostics while preserving existing spectator timing payload parity checks.

Validation:

- Focused single test: new timing-map property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `231/231`.
- Adjacent recovery/opening/store-smoke: passed `812/812`.
- Backend full: passed `6177/6177`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator snapshot top-level timing-map property-name shape slice. Remaining spectator snapshot nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
