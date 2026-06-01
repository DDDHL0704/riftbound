# Stage 4D-17ST Recovery Timing Battlefield-Task Participant-Object Battlefield Membership Audit

Date: 2026-06-02 07:43 CST

Status: accepted runtime validation slice. Project remains **NOT READY**.

## Scope

Stage 4D-17ST tightens P1-004 recovery/replay determinism for pending timing `battlefieldTasks[]` payloads. The slice only changes recovery validation and conformance tests. It does not change command resolution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status or solution files.

## Runtime Change

`MatchRecoveryValidator` now validates battlefield-task participant-object battlefield membership for both recovered player-view snapshots and spectator replay frames:

- Recovered player-view snapshot `battlefieldTasks[]` participants are checked against object locations recovered from snapshot player object payloads.
- Spectator replay-frame `battlefieldTasks[]` participants are checked against authoritative `ObjectLocations`.
- Each readable, unique `participantObjectIds[]` entry must have a location and must be in zone `BATTLEFIELD` at the task `battlefieldObjectId`.

Current `BuildBattlefieldTaskStates` output builds task participants from objects occupying the contested battlefield, so participants at another battlefield or in base cannot canonically belong to the task. The new diagnostics run after required participant object-id registry validation and before authoritative spectator battlefield-task parity comparison. This preserves explicit same-payload diagnostics for forged participant locations even when spectator `battlefieldTasks[]` count differs from authoritative state and parity is skipped.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskParticipantObjectsOutsideTaskBattlefield`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskParticipantObjectsOutsideTaskBattlefield`

The snapshot test proves participant objects located at another battlefield or base emit explicit diagnostics against the task battlefield id. The spectator test proves the same location drift emits diagnostics under a battlefield-task count mismatch where authoritative parity is skipped.

## Validation

Passed:

- Focused participant battlefield membership tests: `2/2`
- Focused BattlefieldTask filter: `40/40`
- Focused recovery filter: `614/614`
- Adjacent recovery/opening/store-smoke filter: `1214/1214`
- Backend full: `6560/6560`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows recovery/replay determinism for timing battlefield-task participant-object battlefield membership. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
