# Stage 4D-17HT Recovery Snapshot Rune Pool Payload Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates property names for recovered player-view snapshot nested `Players[*]["runePool"]` payload objects. Rune-pool object property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before rune-pool field consumers can normalize those keys.

Test change: `RecoveryValidatorRejectsSnapshotPlayerRunePoolPayloadPropertyNameDrift` proves duplicate normalized rune-pool keys, whitespace-mutated rune-pool keys and blank rune-pool keys produce explicit recovery diagnostics for recovered player-view snapshot player rune-pool payloads while preserving existing optional player rune-pool payload behavior.

Validation:

- Focused single test: new snapshot player rune-pool payload property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `247/247`.
- Adjacent recovery/opening/store-smoke: passed `828/828`.
- Backend full: passed `6193/6193`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the recovered player-view snapshot player rune-pool payload object property-name shape slice. Remaining recovered/spectator nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
