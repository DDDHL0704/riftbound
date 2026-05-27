# Stage 4D-17KR Recovery Spectator Snapshot Core Scalar Value Shape Audit

Date: 2026-05-27 17:20 CST

Project status: **NOT READY**.

## Scope

This slice covers spectator replay-frame snapshot top-level core scalar value shape only: `Tick`, `TurnNumber`, `ActivePlayerId` and `TurnState`. It does not change nested player/lane/stack/timing payload shape, protocol shape, frontend behavior, matrix JSON, official catalog data, Chrome/browser/formal E2E scripts, `fullOfficial`, or final status.

Stage 4D-17KQ covered spectator snapshot stack item value shape. Stage 4D-17KR adds explicit top-level spectator snapshot scalar shape validation before parity comparisons consume those values.

## Runtime Change

- Spectator replay-frame snapshot `Tick` now rejects negative values with an explicit diagnostic before frame tick comparison.
- Spectator replay-frame snapshot `TurnNumber` now rejects values below 1 with an explicit diagnostic before authoritative turn-number comparison.
- Spectator replay-frame snapshot `TurnState` now rejects missing/blank values, surrounding-whitespace values and unknown timing states before authoritative timing-state comparison.
- Spectator replay-frame snapshot `ActivePlayerId` now rejects missing/blank values and surrounding-whitespace values before authoritative active-player comparison.
- Existing parity diagnostics for valid-but-mismatched tick, turn number, turn state and active player remain in place.

## Test Coverage

`RecoveryValidatorRejectsSpectatorReplaySnapshotCoreScalarValueShapeDrift` mutates a spectator replay-frame snapshot with a negative tick, non-positive turn number, whitespace-mutated active player id and whitespace-mutated unknown turn state, then proves explicit diagnostics for each malformed top-level scalar.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `323/323`
- Adjacent recovery/opening/store-smoke filter: `904/904`
- Backend full: `6269/6269`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for spectator replay-frame snapshot top-level core scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
