# Stage 4D-17JQ Recovery Snapshot Spell Duel Payload Shape Audit

Date: 2026-05-26 13:21 CST

Project status: **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation for present top-level `Timing["spellDuel"]` payload shape only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final readiness.

Stage 4D-17JP covered top-level turn-window object shape. Stage 4D-17JQ adds the same explicit object-shape guard for recovered snapshot spell-duel payloads. Missing or null spell-duel compatibility for recovered snapshots is unchanged.

## Runtime Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now calls `ValidateSnapshotTimingObjectPayloadShape` for `Timing["spellDuel"]` before spell-duel property-name and value validation.
- Present non-null recovered snapshot `Timing["spellDuel"]` now fails with `snapshot for {playerId} timing spell duel payload is required` when it is not an object payload.
- Existing spell-duel property/value validation behavior is unchanged for object payloads.

## Test Coverage

`RecoveryValidatorRejectsSnapshotTimingSpellDuelPayloadShapeDrift` mutates recovered snapshot timing payloads and proves explicit diagnostics for a non-object top-level spell-duel payload while preserving missing/null compatibility.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `296/296`
- Adjacent recovery/opening/store-smoke filter: `877/877`
- Backend full: `6242/6242`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered player-view snapshot spell-duel top-level payload shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
