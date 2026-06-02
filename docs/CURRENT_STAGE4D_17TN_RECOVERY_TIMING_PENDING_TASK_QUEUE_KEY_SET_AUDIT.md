# Stage 4D-17TN Recovery Timing Pending Task Queue Key Set Audit

Date: 2026-06-02

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17TN narrows P1-004 recovery/replay determinism for spectator replay-frame timing `pendingTaskQueue.tasks[]` payloads. The slice targets a count-mismatch gap: pending task queue task payload shape and same-payload values were already validated under task-count mismatch, but authoritative task-id parity still relied on broad index-based comparison that is skipped when spectator task count differs from authoritative `PendingTaskQueue.Tasks`.

Runtime files changed:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Behavior

`MatchRecoveryValidator` now builds an authoritative pending-task index keyed by spectator-visible task id before count-equal parity checks. The key builder uses the same visibility rule as spectator replay serialization:

- visible cleanup tasks use the authoritative task id
- hidden illegal standby cleanup tasks use the redacted visible id from `VisibleCleanupTaskIdForRecovery`

Spectator replay-frame `pendingTaskQueue.tasks[]` task ids are compared against that authoritative visible key set. The validator now reports:

- spectator task ids not present in authoritative pending task queue tasks
- authoritative visible pending task ids missing from the spectator payload

This runs alongside pending task queue base payload checks, task payload shape/value validation, duplicate task-id validation, active-task diagnostics, metadata consistency checks and task-count mismatch diagnostics. Broad task-id/value parity still remains behind the count-equal gate.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueTaskKeySetWithTaskCountMismatch`.

The test builds a spectator replay frame from an authoritative state with two pending cleanup tasks, mutates one same-visible task id, and adds an extra forged task so spectator task count differs from authoritative task count. Validation now reports both forged task ids and the missing authoritative visible task id before broad parity would run.

## Validation

- Focused new test: `1/1`
- Focused PendingTaskQueue filter: `33/33`
- Focused recovery filter: `642/642`
- Adjacent recovery/opening/store-smoke filter: `1222/1222`
- Backend full: `6587/6587`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This narrows recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
