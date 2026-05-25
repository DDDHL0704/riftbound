# Stage 4D-17HX Recovery Snapshot Object Location Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates property names for recovered player-view snapshot nested `Players[*]["objects"][objectId]["location"]` payload objects. Object location property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before object-location field consumers can normalize those keys.

Test change: `RecoveryValidatorRejectsSnapshotPlayerObjectLocationPayloadPropertyNameDrift` proves duplicate normalized location payload keys, whitespace-mutated location payload keys and blank location payload keys produce explicit recovery diagnostics for recovered player-view snapshot player object location payloads while preserving existing optional player object location payload behavior.

Validation:

- Focused single test: new snapshot player object location property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `251/251`.
- Adjacent recovery/opening/store-smoke: passed `832/832`.
- Backend full: passed `6197/6197`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the recovered player-view snapshot player object location payload property-name shape slice. Remaining recovered/spectator nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
