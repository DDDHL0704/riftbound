# Stage 4D-17MR Recovery Snapshot Timing Pending Task Queue Task Id Uniqueness Audit

Date: 2026-05-28 02:36 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened recovered player-view snapshot recovery validation only. `MatchRecoveryValidator.ValidateSnapshotShape` now rejects duplicate normalized `taskId` values across object-shaped `Snapshot.Timing["pendingTaskQueue"]["tasks"][]` entries before future pending-task-queue consumers can consume ambiguous recovered task identity.

Runtime change: `ValidatePendingTaskQueueTaskPayloadValues` now returns the normalized task id while preserving the existing required/optional value diagnostics. The recovered snapshot pending-task-queue caller tracks those ids with ordinal uniqueness and emits `snapshot for {player} timing pending task queue task item task id {taskId} is duplicated` on repeat ids.

Test coverage: `RecoveryValidatorRejectsSnapshotTimingPendingTaskQueueTaskDuplicateIds` proves surrounding-whitespace task ids keep the existing whitespace diagnostic and duplicate normalized task ids produce the new explicit duplicate diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `375/375`
- Adjacent recovery/opening/store-smoke: `956/956`
- Backend full: `6321/6321`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17MR_RECOVERY_SNAPSHOT_TIMING_PENDING_TASK_QUEUE_TASK_ID_UNIQUENESS_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
