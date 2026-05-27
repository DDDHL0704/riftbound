# Stage 4D-17MU Recovery Spectator Timing Pending Task Queue Active Task Id Reference Audit

Date: 2026-05-28 02:59 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects optional-present `activeTaskId` values in object-shaped `Timing["pendingTaskQueue"]` payloads when the id does not match a normalized `pendingTaskQueue.tasks[]` task id before pending-task-queue parity consumers compare or dereference spectator active task identity.

Runtime change: `ValidateSpectatorPendingTaskQueuePayloadValues` now returns the normalized optional `activeTaskId` from the shared optional-string value helper. The spectator pending-task-queue task caller compares that id against the task-id set collected from validated `tasks[]` entries and emits `spectator replay frame timing pending task queue active task id {taskId} does not match a pending task queue task id` when the active id is dangling.

Test coverage: `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueDanglingActiveTaskId` proves a spectator replay-frame pending task queue with a syntactically valid but absent `activeTaskId` target produces the new explicit referential diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `378/378`
- Adjacent recovery/opening/store-smoke: `959/959`
- Backend full: `6324/6324`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17MU_RECOVERY_SPECTATOR_TIMING_PENDING_TASK_QUEUE_ACTIVE_TASK_ID_REFERENCE_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
