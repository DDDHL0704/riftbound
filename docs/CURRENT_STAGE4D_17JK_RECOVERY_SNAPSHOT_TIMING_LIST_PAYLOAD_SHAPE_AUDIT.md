# Stage 4D-17JK Recovery Snapshot Timing List Payload Shape Audit

Date: 2026-05-26 12:01 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present timing object-list item payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JJ added explicit object-shape validation for recovered snapshot resolution-history arrays. Stage 4D-17JK extends the same recovered snapshot guard to the remaining top-level timing arrays whose property-name and value validators only operate on object entries.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingListItemPayloadShapes` before recovered snapshot timing list item property/value validation.
- Present recovered snapshot `Timing["temporaryPaymentResources"][]` entries now fail with `snapshot for {playerId} timing temporary payment resource payload is required` when an item is not an object payload.
- Present recovered snapshot `Timing["continuousEffects"][]` entries now fail with `snapshot for {playerId} timing continuous effect payload is required` when an item is not an object payload.
- Present recovered snapshot `Timing["triggerQueue"][]` entries now fail with `snapshot for {playerId} timing trigger queue item payload is required` when an item is not an object payload.
- Present recovered snapshot `Timing["battlefieldTasks"][]` entries now fail with `snapshot for {playerId} timing battlefield task payload is required` when an item is not an object payload.
- The previous resolution-history payload-shape validation now uses the same shared helper.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingListItemPayloadShapeDrift` mutates recovered snapshot timing payloads and proves explicit diagnostics for:

- non-object temporary-payment-resource entries
- non-object continuous-effect entries
- non-object trigger-queue entries
- non-object battlefield-task entries

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `290/290`
- Adjacent recovery/opening/store-smoke filter: `871/871`
- Backend full: `6236/6236`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot timing list item payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
