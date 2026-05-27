# Stage 4D-17MW Recovery Snapshot Timing Pending Task Queue Metadata State Based Task Kinds Audit

Date: 2026-05-28 03:15 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened recovered player-view snapshot recovery validation only. `MatchRecoveryValidator.ValidateSnapshotShape` now rejects object-shaped `Snapshot.Timing["pendingTaskQueue"]["metadata"]["stateBasedTaskKinds"]` lists that do not match the state-based cleanup task kinds derived from the same `pendingTaskQueue.tasks[]` list before future pending-task-queue consumers trust stale or incomplete queue metadata.

Runtime change: `ValidatePendingTaskQueueTaskPayloadValues` now returns the normalized task id and normalized task kind. The recovered snapshot pending-task-queue caller accumulates validated task kinds, derives the expected state-based cleanup kind list with the existing recovery state-based cleanup helper, and emits `snapshot for {player} timing pending task queue metadata state-based task kinds do not match pending task queue task kinds` when the metadata list is syntactically valid but inconsistent with the task list.

Test coverage: `RecoveryValidatorRejectsSnapshotTimingPendingTaskQueueMetadataStateBasedTaskKindsMismatch` proves a recovered snapshot pending task queue with a valid task count but an incomplete `metadata.stateBasedTaskKinds` list produces the new explicit consistency diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `380/380`
- Adjacent recovery/opening/store-smoke: `961/961`
- Backend full: `6326/6326`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17MW_RECOVERY_SNAPSHOT_TIMING_PENDING_TASK_QUEUE_METADATA_STATE_BASED_TASK_KINDS_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
