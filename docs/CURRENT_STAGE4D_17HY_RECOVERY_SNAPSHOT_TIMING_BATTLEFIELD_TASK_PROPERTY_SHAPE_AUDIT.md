# Stage 4D-17HY Recovery Snapshot Timing Battlefield Task Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSnapshotShape` now validates property names for recovered player-view snapshot `Timing["battlefieldTasks"][]` item payload objects. Battlefield-task item property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before battlefield-task field consumers can normalize those keys.

Test change: `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskPropertyNameDrift` proves duplicate normalized battlefield-task keys, whitespace-mutated battlefield-task keys and blank battlefield-task keys produce explicit recovery diagnostics for recovered player-view snapshot timing battlefield-task item payloads while preserving existing optional timing battlefield-task list behavior.

Validation:

- Focused single test: new snapshot timing battlefield-task property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `252/252`.
- Adjacent recovery/opening/store-smoke: passed `833/833`.
- Backend full: passed `6198/6198`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the recovered player-view snapshot timing battlefield-task item property-name shape slice. Remaining recovered/spectator nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
