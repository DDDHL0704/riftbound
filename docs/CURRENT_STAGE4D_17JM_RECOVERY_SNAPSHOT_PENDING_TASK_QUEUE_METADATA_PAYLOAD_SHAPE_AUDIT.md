# Stage 4D-17JM Recovery Snapshot Pending Task Queue Metadata Payload Shape Audit

Date: 2026-05-26 12:32 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present nested `Timing["pendingTaskQueue"]["metadata"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JL covered nested pending-task-queue task item shape. Stage 4D-17JM adds the corresponding metadata object-shape guard for present non-null metadata payloads. Missing or null metadata compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingPendingTaskQueueMetadataPayloadShape` between pending-task-queue task payload shape and value validation.
- Present non-null recovered snapshot `Timing["pendingTaskQueue"]["metadata"]` now fails with `snapshot for {playerId} timing pending task queue metadata payload is required` when it is not an object payload.
- Existing pending-task-queue queue, task item and metadata property/value validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingPendingTaskQueueMetadataPayloadShapeDrift` mutates recovered snapshot timing payloads and proves explicit diagnostics for a non-object nested pending-task-queue metadata payload while preserving valid queue and task item shape.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `292/292`
- Adjacent recovery/opening/store-smoke filter: `873/873`
- Backend full: `6238/6238`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot pending-task-queue nested metadata payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
