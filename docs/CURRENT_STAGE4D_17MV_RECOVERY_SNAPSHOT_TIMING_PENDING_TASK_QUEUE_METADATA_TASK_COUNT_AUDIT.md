# Stage 4D-17MV Recovery Snapshot Timing Pending Task Queue Metadata Task Count Audit

Date: 2026-05-28 03:06 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened recovered player-view snapshot recovery validation only. `MatchRecoveryValidator.ValidateSnapshotShape` now rejects non-negative `taskCount` values in object-shaped `Snapshot.Timing["pendingTaskQueue"]["metadata"]` payloads when the count does not match the same `pendingTaskQueue.tasks[]` list length before future pending-task-queue consumers trust internally inconsistent queue metadata.

Runtime change: `ValidatePendingTaskQueueMetadataPayloadValues` now returns the normalized `taskCount` from the shared non-negative integer value helper. The recovered snapshot pending-task-queue caller compares that count against the validated task-list payload count and emits `snapshot for {player} timing pending task queue metadata task count {metadataTaskCount} does not match pending task queue task count {actualTaskCount}` when both sides are valid but inconsistent.

Test coverage: `RecoveryValidatorRejectsSnapshotTimingPendingTaskQueueMetadataTaskCountMismatch` proves a recovered snapshot pending task queue with a syntactically valid but mismatched metadata `taskCount` produces the new explicit consistency diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `379/379`
- Adjacent recovery/opening/store-smoke: `960/960`
- Backend full: `6325/6325`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17MV_RECOVERY_SNAPSHOT_TIMING_PENDING_TASK_QUEUE_METADATA_TASK_COUNT_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
