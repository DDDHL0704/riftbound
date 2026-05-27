# Stage 4D-17NA Recovery Spectator Timing Pending Task Queue Flags Task Count Audit

Date: 2026-05-28 03:49 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame timing recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects object-shaped `Timing["pendingTaskQueue"]` payloads whose valid `hasTasks` or `isBlocking` flags do not match the same `tasks[]` list count before pending-task-queue parity consumers trust stale queue-state flags.

Runtime change: the spectator pending-task-queue payload value helper now returns valid queue flags alongside the active task id, and the spectator queue caller reuses the shared flag/task-count consistency validator once `tasks[]` is list-shaped. Existing authoritative queue comparisons remain in place.

Test coverage: `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueFlagTaskCountMismatch` proves a generated spectator pending task queue with two valid tasks but false queue flags produces explicit internal consistency diagnostics.

Validation:

- Focused single: `1/1`
- Focused recovery: `384/384`
- Adjacent recovery/opening/store-smoke: `965/965`
- Backend full: `6330/6330`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NA_RECOVERY_SPECTATOR_TIMING_PENDING_TASK_QUEUE_FLAGS_TASK_COUNT_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
