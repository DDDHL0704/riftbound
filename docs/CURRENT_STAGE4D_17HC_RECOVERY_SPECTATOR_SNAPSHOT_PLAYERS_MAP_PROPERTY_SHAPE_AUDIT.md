# Stage 4D-17HC Recovery Spectator Snapshot Players Map Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates property names for the top-level spectator snapshot `Players` map before seat coverage and per-player payload comparisons consume those player keys. Map property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before player payload validation and seat extraction continue.

Test change: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayersMapPropertyNameDrift` proves duplicate normalized player-map keys, whitespace-mutated player keys and blank player keys produce explicit recovery diagnostics while preserving existing spectator player payload parity checks.

Validation:

- Focused single test: new players-map property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `230/230`.
- Adjacent recovery/opening/store-smoke: passed `811/811`.
- Backend full: passed `6176/6176`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator snapshot top-level players-map property-name shape slice. Remaining spectator snapshot nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
