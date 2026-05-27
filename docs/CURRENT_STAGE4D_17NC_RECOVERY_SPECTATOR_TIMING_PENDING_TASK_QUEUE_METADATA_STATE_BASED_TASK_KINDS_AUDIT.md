# Stage 4D-17NC Recovery Spectator Timing Pending Task Queue Metadata State Based Task Kinds Audit

Date: 2026-05-28 04:05 CST

Status: accepted. Project remains **NOT READY**.

Scope: A_MAIN tightened spectator replay-frame timing recovery validation only. `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects object-shaped `Timing["pendingTaskQueue"]["metadata"]` payloads whose valid `stateBasedTaskKinds` list does not match the state-based cleanup task kinds derived from the same spectator pending-task-queue `tasks[]` list before pending-task-queue parity consumers trust stale or incomplete queue metadata.

Runtime change: spectator pending-task-queue task validation now returns valid task kinds from `tasks[]` while preserving existing task scalar diagnostics. The spectator metadata caller compares those task-list-derived kinds with metadata `stateBasedTaskKinds` before the existing authoritative state comparison.

Test coverage: `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueMetadataStateBasedTaskKindsMismatch` proves a generated spectator pending task queue with valid tasks but mismatched metadata state-based task kinds produces an explicit internal consistency diagnostic.

Validation:

- Focused single: `1/1`
- Focused recovery: `386/386`
- Adjacent recovery/opening/store-smoke: `967/967`
- Backend full: `6332/6332`
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Files touched:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current checkpoint/completion/P0-P1/next-dispatch/shared-board docs
- `docs/CURRENT_STAGE4D_17NC_RECOVERY_SPECTATOR_TIMING_PENDING_TASK_QUEUE_METADATA_STATE_BASED_TASK_KINDS_AUDIT.md`

Locked / unchanged: Matrix JSON content, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln`.

Readiness: this narrows P1-004 replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
