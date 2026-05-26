# Stage 4D-17JS Recovery Snapshot Pending Hand Choice Payload Shape Audit

Date: 2026-05-26 13:40 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present top-level `Timing["pendingHandChoice"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JR covered top-level battle object shape. Stage 4D-17JS adds the same explicit object-shape guard for recovered snapshot pending-hand-choice payloads. Missing or null pending-hand-choice compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingObjectPayloadShape` for `Timing["pendingHandChoice"]` before pending-hand-choice property-name and value validation.
- Present non-null recovered snapshot `Timing["pendingHandChoice"]` now fails with `snapshot for {playerId} timing pending hand choice payload is required` when it is not an object payload.
- Existing pending-hand-choice property/value validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingPendingHandChoicePayloadShapeDrift` mutates recovered snapshot timing payloads and proves explicit diagnostics for a non-object top-level pending-hand-choice payload while preserving missing/null compatibility.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `298/298`
- Adjacent recovery/opening/store-smoke filter: `879/879`
- Backend full: `6244/6244`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot pending-hand-choice top-level payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
