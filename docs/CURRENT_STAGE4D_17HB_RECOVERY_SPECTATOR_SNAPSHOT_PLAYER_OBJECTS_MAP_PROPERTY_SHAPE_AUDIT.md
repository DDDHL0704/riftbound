# Stage 4D-17HB Recovery Spectator Snapshot Player Objects Map Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates object property names for nested spectator snapshot player `objects` map payloads before visible-object map extraction consumes them. Map property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before visible-object id comparisons consume those maps. `JsonElement` object-map extraction now iterates explicitly instead of using `ToDictionary`, so duplicate JSON map keys produce recovery diagnostics instead of an exception during validation.

Test change: `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectsMapPropertyNameDrift` proves duplicate, whitespace-mutated and blank property names in player visible-object maps produce explicit recovery diagnostics while preserving existing visible-object payload parity checks.

Validation:

- Focused single test: new player objects-map property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `229/229`.
- Adjacent recovery/opening/store-smoke: passed `810/810`.
- Backend full: passed `6175/6175`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the spectator snapshot player visible-object map property-name shape slice. Remaining spectator snapshot nested payload property-name breadth outside this map, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
