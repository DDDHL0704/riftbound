# Stage 4D-17MZ Recovery Spectator Timing Pending Task Queue Active Task Id Completeness Audit

Date: 2026-05-28 03:40 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame timing recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects object-shaped `Timing["pendingTaskQueue"]` payloads with a non-empty `tasks[]` list but missing, null or empty `activeTaskId` before pending-task-queue parity consumers accept an active spectator queue with no active task identity.

Runtime change: the spectator pending-task-queue task validation path now requires a normalized non-empty active task id when the validated task list contains at least one item. Existing authoritative active-task-id parity and dangling active-task-id reference checks remain in place.

Test coverage: `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueMissingActiveTaskIdWithTasks` proves a spectator replay-frame pending task queue with generated valid tasks and no `activeTaskId` produces the new explicit completeness diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `383/383`
- Adjacent recovery/opening/store-smoke: `964/964`
- Backend full: `6329/6329`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17MZ_RECOVERY_SPECTATOR_TIMING_PENDING_TASK_QUEUE_ACTIVE_TASK_ID_COMPLETENESS_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
