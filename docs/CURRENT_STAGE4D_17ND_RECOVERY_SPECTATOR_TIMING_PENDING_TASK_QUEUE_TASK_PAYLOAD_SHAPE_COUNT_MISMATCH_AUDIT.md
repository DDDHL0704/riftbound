# Stage 4D-17ND Recovery Spectator Timing Pending Task Queue Task Payload Shape Count Mismatch Audit

Date: 2026-05-28 04:14 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame timing recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates `Timing["pendingTaskQueue"]["tasks"][]` item payload shape and basic spectator task values whenever `tasks[]` is list-shaped, even when the spectator task count already differs from the authoritative pending queue.

Runtime change: spectator pending-task-queue task validation now accepts an authoritative-parity gate. It always accumulates spectator task payload shape, scalar, duplicate-id and active-task-id diagnostics for the list-shaped spectator payload, while indexed authoritative task parity remains gated to matching task counts.

Test coverage: `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueTaskPayloadShapeWithTaskCountMismatch` proves an extra malformed spectator pending task entry still produces the explicit task payload required diagnostic alongside the authoritative task-count mismatch diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `387/387`
- Adjacent recovery/opening/store-smoke: `968/968`
- Backend full: `6333/6333`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17ND_RECOVERY_SPECTATOR_TIMING_PENDING_TASK_QUEUE_TASK_PAYLOAD_SHAPE_COUNT_MISMATCH_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
