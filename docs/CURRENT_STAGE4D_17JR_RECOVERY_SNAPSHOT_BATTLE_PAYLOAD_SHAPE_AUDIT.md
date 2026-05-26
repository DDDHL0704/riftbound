# Stage 4D-17JR Recovery Snapshot Battle Payload Shape Audit

Date: 2026-05-26 13:29 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present top-level `Timing["battle"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JQ covered top-level spell-duel object shape. Stage 4D-17JR adds the same explicit object-shape guard for recovered snapshot battle payloads. Missing or null battle compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingObjectPayloadShape` for `Timing["battle"]` before battle property-name, battle value, nested participant-controller and damage-assignment validation.
- Present non-null recovered snapshot `Timing["battle"]` now fails with `snapshot for {playerId} timing battle payload is required` when it is not an object payload.
- Existing battle property/value validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingBattlePayloadShapeDrift` mutates recovered snapshot timing payloads and proves explicit diagnostics for a non-object top-level battle payload while preserving missing/null compatibility.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `297/297`
- Adjacent recovery/opening/store-smoke filter: `878/878`
- Backend full: `6243/6243`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot battle top-level payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
