# Stage 4D-17HG Recovery Snapshot Timing Map Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSnapshotShape` now validates property names for recovered player-view snapshot `Timing` maps before timing core scalar reads consume those keys. Map property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before timing field validation continues.

Test change: `RecoveryValidatorRejectsSnapshotTimingMapPropertyNameDrift` proves duplicate normalized timing-map keys, whitespace-mutated timing keys and blank timing keys produce explicit recovery diagnostics while preserving existing recovered timing field value validation.

Validation:

- Focused single test: new snapshot timing-map property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `234/234`.
- Adjacent recovery/opening/store-smoke: passed `815/815`.
- Backend full: passed `6180/6180`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the recovered player-view snapshot top-level timing-map property-name shape slice. Remaining recovered/spectator nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
