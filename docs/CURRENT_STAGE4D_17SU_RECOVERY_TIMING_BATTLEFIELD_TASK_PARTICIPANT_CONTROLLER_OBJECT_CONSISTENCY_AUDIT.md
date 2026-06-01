# Stage 4D-17SU Recovery Timing Battlefield-Task Participant Controller/Object Consistency Audit

Date: 2026-06-02 07:56 CST

Status: accepted runtime validation slice. Project remains **NOT READY**.

## Scope

Stage 4D-17SU tightens P1-004 recovery/replay determinism for pending timing `battlefieldTasks[]` payloads. The slice only changes recovery validation and conformance tests. It does not change command resolution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status or solution files.

## Runtime Change

`MatchRecoveryValidator` now validates battlefield-task participant controller/object consistency for both recovered player-view snapshots and spectator replay frames:

- Recovered player-view snapshot validation builds an object-controller index from snapshot object payloads using `controllerId`, then `ownerId`, then location `playerId`.
- Spectator replay-frame validation builds the same effective controller index from authoritative `CardObjects` plus `ObjectLocations`.
- Each readable `participantControllerIds[]` entry must be a controller for at least one readable `participantObjectIds[]` object.
- Each readable participant object with a known effective controller requires that controller to appear in `participantControllerIds[]`.

Current `BuildBattlefieldTaskStates` derives `participantControllerIds[]` from the effective controllers of the sorted participant object set. The new diagnostics run after participant player/object reference validation and participant battlefield membership validation. This preserves explicit same-payload diagnostics for forged controller lists even when spectator `battlefieldTasks[]` count differs from authoritative state and parity is skipped.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskParticipantControllersInconsistentWithParticipantObjects`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskParticipantControllersInconsistentWithParticipantObjects`

The snapshot test proves extra participant controllers and missing object-derived controllers emit explicit diagnostics. The spectator test proves the same forged controller/object drift emits diagnostics under a battlefield-task count mismatch where authoritative parity is skipped.

## Validation

Passed:

- Focused participant controller/object consistency tests: `2/2`
- Focused BattlefieldTask filter: `42/42`
- Focused recovery filter: `616/616`
- Adjacent recovery/opening/store-smoke filter: `1216/1216`
- Backend full: `6562/6562`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows recovery/replay determinism for timing battlefield-task participant controller/object consistency. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
