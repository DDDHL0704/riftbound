# Stage 4D-17NB Recovery Spectator Timing Pending Task Queue Metadata Task Count Audit

Date: 2026-05-28 03:58 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame timing recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects object-shaped `Timing["pendingTaskQueue"]["metadata"]` payloads whose valid non-negative `taskCount` does not match the same spectator pending-task-queue `tasks[]` list count before pending-task-queue parity consumers trust internally inconsistent queue metadata.

Runtime change: spectator pending-task-queue validation now records the same-payload `tasks[]` count when the task list is list-shaped, and the spectator metadata value helper returns the normalized `taskCount` while preserving existing scalar/list diagnostics. Existing authoritative pending-task-queue count comparisons remain in place.

Test coverage: `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueMetadataTaskCountMismatch` proves a generated spectator pending task queue with two valid tasks but `metadata.taskCount = 1` produces an explicit internal consistency diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `385/385`
- Adjacent recovery/opening/store-smoke: `966/966`
- Backend full: `6331/6331`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NB_RECOVERY_SPECTATOR_TIMING_PENDING_TASK_QUEUE_METADATA_TASK_COUNT_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
