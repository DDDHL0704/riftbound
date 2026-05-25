# Stage 4D-17HU Recovery Snapshot Zones Payload Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates property names for recovered player-view snapshot nested `Players[*]["zones"]` payload objects. Zone object property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before zone field consumers can normalize those keys.

Test change: `RecoveryValidatorRejectsSnapshotPlayerZonesPayloadPropertyNameDrift` proves duplicate normalized zone keys, whitespace-mutated zone keys and blank zone keys produce explicit recovery diagnostics for recovered player-view snapshot player zones payloads while preserving existing optional player zones payload behavior.

Validation:

- Focused single test: new snapshot player zones payload property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `248/248`.
- Adjacent recovery/opening/store-smoke: passed `829/829`.
- Backend full: passed `6194/6194`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the recovered player-view snapshot player zones payload object property-name shape slice. Remaining recovered/spectator nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
