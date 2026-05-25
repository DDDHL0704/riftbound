# Stage 4D-17HE Recovery Snapshot Player Payload Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads` now validates property names for recovered player-view snapshot `Players[*]` payload objects before id and seat extraction consumes those fields. Payload property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before player payload validation continues.

Test change: `RecoveryValidatorRejectsSnapshotPlayerPayloadPropertyNameDrift` proves duplicate normalized `id`, whitespace-mutated `seat` and blank player payload property names produce explicit recovery diagnostics while preserving existing recovered player payload value validation.

Validation:

- Focused single test: new snapshot player payload property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `232/232`.
- Adjacent recovery/opening/store-smoke: passed `813/813`.
- Backend full: passed `6178/6178`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the recovered player-view snapshot player payload property-name shape slice. Remaining recovered/spectator nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
