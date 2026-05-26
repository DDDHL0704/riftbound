# Stage 4D-17JP Recovery Snapshot Turn Window Payload Shape Audit

Date: 2026-05-26 13:13 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present top-level `Timing["turnWindow"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JO covered top-level pending-payment object shape. Stage 4D-17JP adds the same explicit object-shape guard for recovered snapshot turn-window payloads. Missing or null turn-window compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingObjectPayloadShape` for `Timing["turnWindow"]` before turn-window property-name and value validation.
- Present non-null recovered snapshot `Timing["turnWindow"]` now fails with `snapshot for {playerId} timing turn window payload is required` when it is not an object payload.
- Existing turn-window property/value validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingTurnWindowPayloadShapeDrift` mutates recovered snapshot timing payloads and proves explicit diagnostics for a non-object top-level turn-window payload while preserving missing/null compatibility.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `295/295`
- Adjacent recovery/opening/store-smoke filter: `876/876`
- Backend full: `6241/6241`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot turn-window top-level payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
