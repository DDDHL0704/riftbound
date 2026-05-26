# Stage 4D-17JN Recovery Snapshot Pending Task Queue Payload Shape Audit

Date: 2026-05-26 12:48 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present top-level `Timing["pendingTaskQueue"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JL covered nested pending-task-queue task item shape. Stage 4D-17JM covered nested metadata object shape. Stage 4D-17JN adds the corresponding top-level pending-task-queue object-shape guard. Missing or null pending-task-queue compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingPendingTaskQueuePayloadShape` before pending-task-queue property-name, nested task/metadata shape and value validation.
- Present non-null recovered snapshot `Timing["pendingTaskQueue"]` now fails with `snapshot for {playerId} timing pending task queue payload is required` when it is not an object payload.
- Existing pending-task-queue queue, task item and metadata property/value validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingPendingTaskQueuePayloadShapeDrift` mutates recovered snapshot timing payloads and proves explicit diagnostics for a non-object top-level pending-task-queue payload while preserving missing/null compatibility.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `293/293`
- Adjacent recovery/opening/store-smoke filter: `874/874`
- Backend full: `6239/6239`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot pending-task-queue top-level payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
