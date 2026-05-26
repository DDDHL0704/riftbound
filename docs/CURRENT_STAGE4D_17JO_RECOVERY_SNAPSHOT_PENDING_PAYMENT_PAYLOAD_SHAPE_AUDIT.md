# Stage 4D-17JO Recovery Snapshot Pending Payment Payload Shape Audit

Date: 2026-05-26 12:58 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present top-level `Timing["pendingPayment"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JN covered top-level pending-task-queue object shape. Stage 4D-17JO adds the same explicit object-shape guard for recovered snapshot pending-payment payloads. Missing or null pending-payment compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingPendingPaymentPayloadShape` before pending-payment property-name, power-trait, scalar and list value validation.
- Present non-null recovered snapshot `Timing["pendingPayment"]` now fails with `snapshot for {playerId} timing pending payment payload is required` when it is not an object payload.
- Existing pending-payment property/value validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingPendingPaymentPayloadShapeDrift` mutates recovered snapshot timing payloads and proves explicit diagnostics for a non-object top-level pending-payment payload while preserving missing/null compatibility.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `294/294`
- Adjacent recovery/opening/store-smoke filter: `875/875`
- Backend full: `6240/6240`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot pending-payment top-level payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
