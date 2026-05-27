# Stage 4D-17MT Recovery Snapshot Timing Pending Task Queue Active Task Id Reference Audit

Date: 2026-05-28 02:51 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened recovered player-view snapshot recovery validation only. `MatchRecoveryValidator.ValidateSnapshotShape` now rejects optional-present `activeTaskId` values in object-shaped `Snapshot.Timing["pendingTaskQueue"]` payloads when the id does not match a normalized `pendingTaskQueue.tasks[]` task id before future pending-task-queue consumers dereference recovered active task identity.

Runtime change: `ValidatePendingTaskQueuePayloadValues` now returns the normalized optional `activeTaskId` from the shared optional-string value helper. The recovered snapshot pending-task-queue caller compares that id against the task-id set collected from validated `tasks[]` entries and emits `snapshot for {player} timing pending task queue active task id {taskId} does not match a pending task queue task id` when the active id is dangling.

Test coverage: `RecoveryValidatorRejectsSnapshotTimingPendingTaskQueueDanglingActiveTaskId` proves a recovered snapshot pending task queue with a syntactically valid but absent `activeTaskId` target produces the new explicit referential diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `377/377`
- Adjacent recovery/opening/store-smoke: `958/958`
- Backend full: `6323/6323`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17MT_RECOVERY_SNAPSHOT_TIMING_PENDING_TASK_QUEUE_ACTIVE_TASK_ID_REFERENCE_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
