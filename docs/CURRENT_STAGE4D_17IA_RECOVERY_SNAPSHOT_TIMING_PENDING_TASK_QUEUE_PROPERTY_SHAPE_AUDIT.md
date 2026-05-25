# Stage 4D-17IA Recovery Snapshot Timing Pending Task Queue Property Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSnapshotShape` now validates property names for recovered player-view snapshot `Timing["pendingTaskQueue"]`, nested `Timing["pendingTaskQueue"]["tasks"][]` item payloads and `Timing["pendingTaskQueue"]["metadata"]`. Pending-task-queue property names must be non-blank, must not carry surrounding whitespace, and duplicate normalized property names are rejected before pending-task-queue field consumers can normalize those keys.

Test change: `RecoveryValidatorRejectsSnapshotTimingPendingTaskQueuePropertyNameDrift` proves duplicate normalized pending-task-queue keys, whitespace-mutated pending-task-queue keys and blank pending-task-queue keys produce explicit recovery diagnostics across recovered player-view snapshot timing pending-task-queue object payloads, task item payloads and metadata payloads while preserving existing optional timing pending-task-queue object behavior.

Validation:

- Focused single test: new snapshot timing pending-task-queue property-name drift test passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `254/254`.
- Adjacent recovery/opening/store-smoke: passed `835/835`.
- Backend full: passed `6200/6200`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only the recovered player-view snapshot timing pending-task-queue property-name shape slice. Remaining recovered/spectator nested payload property-name breadth, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
