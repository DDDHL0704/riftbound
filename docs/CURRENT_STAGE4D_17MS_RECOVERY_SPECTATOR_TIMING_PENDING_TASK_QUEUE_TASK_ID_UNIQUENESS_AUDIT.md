# Stage 4D-17MS Recovery Spectator Timing Pending Task Queue Task Id Uniqueness Audit

Date: 2026-05-28 02:43 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects duplicate normalized `taskId` values across object-shaped `Timing["pendingTaskQueue"]["tasks"][]` entries before pending-task-queue parity consumers compare spectator task identity against authoritative state.

Runtime change: `ValidateSpectatorPendingTaskQueueTaskPayloadValues` now returns the normalized task id from the shared pending-task-queue task value helper. The spectator pending-task-queue task caller tracks those ids with ordinal uniqueness and emits `spectator replay frame timing pending task queue task item task id {taskId} is duplicated` on repeat ids.

Test coverage: `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueTaskDuplicateIds` proves surrounding-whitespace task ids keep the existing whitespace diagnostic and duplicate normalized task ids produce the new explicit duplicate diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `376/376`
- Adjacent recovery/opening/store-smoke: `957/957`
- Backend full: `6322/6322`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17MS_RECOVERY_SPECTATOR_TIMING_PENDING_TASK_QUEUE_TASK_ID_UNIQUENESS_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
