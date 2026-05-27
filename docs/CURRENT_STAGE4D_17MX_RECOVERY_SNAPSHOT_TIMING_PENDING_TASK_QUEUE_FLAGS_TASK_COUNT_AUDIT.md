# Stage 4D-17MX Recovery Snapshot Timing Pending Task Queue Flags Task Count Audit

Date: 2026-05-28 03:24 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened recovered player-view snapshot recovery validation only. `MatchRecoveryValidator.ValidateSnapshotShape` now rejects object-shaped `Snapshot.Timing["pendingTaskQueue"]` payloads whose `hasTasks` or `isBlocking` flags do not match the same `pendingTaskQueue.tasks[]` list count before future pending-task-queue consumers trust stale queue-state flags.

Runtime change: `ValidatePendingTaskQueuePayloadValues` now returns the normalized optional active task id plus valid `hasTasks` and `isBlocking` flag values. The recovered snapshot pending-task-queue caller compares both flags against `tasks[].Count > 0` after the task list shape is valid and emits explicit task-count consistency diagnostics for mismatched flags.

Test coverage: `RecoveryValidatorRejectsSnapshotTimingPendingTaskQueueFlagTaskCountMismatch` proves a recovered snapshot pending task queue with a valid non-empty task list but false `hasTasks` and `isBlocking` flags produces explicit consistency diagnostics.

Validation:

- Focused single: `1/1`
- Focused recovery: `381/381`
- Adjacent recovery/opening/store-smoke: `962/962`
- Backend full: `6327/6327`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17MX_RECOVERY_SNAPSHOT_TIMING_PENDING_TASK_QUEUE_FLAGS_TASK_COUNT_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
