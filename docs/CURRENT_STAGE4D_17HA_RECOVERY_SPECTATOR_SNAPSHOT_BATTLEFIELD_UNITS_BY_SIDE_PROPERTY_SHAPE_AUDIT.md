# Stage 4D-17HA Recovery Spectator Snapshot Battlefield Units By Side Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates object property names for nested spectator snapshot lane battlefield `unitsBySide` map payloads before map extraction consumes them. Map property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before battlefield occupant grouping comparisons consume those maps. This prevents duplicate JSON properties or spectator snapshot units-by-side key drift from being silently resolved by dictionary / `JsonElement` key lookup during recovery-frame spectator validation.

Test change: `RecoveryValidatorRejectsSpectatorReplaySnapshotBattlefieldUnitsBySidePropertyNameDrift` proves duplicate, whitespace-mutated and blank property names in battlefield units-by-side maps produce explicit recovery diagnostics while preserving existing battlefield list parity checks.

Validation:

- Focused single test: new battlefield units-by-side property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `228/228`.
- Adjacent recovery/opening/store-smoke: passed `809/809`.
- Backend full: passed `6174/6174`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator snapshot battlefield units-by-side property-name shape slice. Remaining spectator snapshot nested payload property-name breadth outside this map, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
