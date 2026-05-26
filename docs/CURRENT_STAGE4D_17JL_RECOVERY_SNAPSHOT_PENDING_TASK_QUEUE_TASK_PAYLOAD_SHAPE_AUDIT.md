# Stage 4D-17JL Recovery Snapshot Pending Task Queue Task Payload Shape Audit

Date: 2026-05-26 12:18 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for nested `Timing["pendingTaskQueue"]["tasks"][]` item payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JK covered top-level recovered snapshot timing arrays. `pendingTaskQueue.tasks` is nested inside the queue payload, and its property-name and value validators still only operated on object entries. Stage 4D-17JL adds the missing nested task item object-shape guard.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingPendingTaskQueueTaskPayloadShapes` between pending-task-queue property-name and value validation.
- Present recovered snapshot `Timing["pendingTaskQueue"]["tasks"][]` entries now fail with `snapshot for {playerId} timing pending task queue task payload is required` when an item is not an object payload.
- Existing pending-task-queue queue, metadata and task item property/value validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingPendingTaskQueueTaskPayloadShapeDrift` mutates recovered snapshot timing payloads and proves explicit diagnostics for a non-object nested pending-task-queue task entry while preserving valid queue and metadata shape.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `291/291`
- Adjacent recovery/opening/store-smoke filter: `872/872`
- Backend full: `6237/6237`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot pending-task-queue nested task payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
