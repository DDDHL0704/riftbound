# Stage 4D-17SY Recovery Timing Battlefield Task Occupant Membership Audit

Status: accepted server recovery validation closure slice. Project remains **NOT READY**.

Date: 2026-06-02 08:50 CST

## Scope

This slice tightens recovery timing battlefield-task validation so pending recovered player-view snapshot and spectator replay-frame `battlefieldTasks[]` `participantObjectIds[]` must match the current battlefield-state occupants for the task `battlefieldObjectId`.

Runtime battlefield tasks are derived from `MatchState.BattlefieldStates`: pending task participants come from the target battlefield state's non-standby unit occupants, and participant controllers are derived from those participants. Earlier Stage 4D slices checked participant object registry membership, task battlefield location, unit-card tags, controller/object consistency, battlefield-card membership and battlefield-state membership. This slice closes the remaining same-payload gap where a valid unit located at the task battlefield but absent from current battlefield-state occupants could be substituted for a true occupant.

## Runtime Change

- Recovered snapshot battlefield-task list validation now indexes `snapshot.Lanes["battlefields"]` by `battlefieldObjectId` and compares task `participantObjectIds[]` against readable `battlefields[].occupantObjectIds`.
- Spectator replay battlefield-task list validation now compares task `participantObjectIds[]` against authoritative `MatchState.BattlefieldStates` occupant object ids.
- The validator emits explicit same-payload diagnostics for extra participant object ids outside battlefield-state occupants and for omitted required occupants, including spectator count-mismatch paths where authoritative battlefield-task parity is skipped.

Protocol shape changed: no. Command resolution, frontend, matrix JSON, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final readiness status are unchanged.

## Coverage

New tests:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskParticipantObjectsInconsistentWithBattlefieldStateOccupants`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskParticipantObjectsInconsistentWithBattlefieldStateOccupants`

Validation passed:

- focused occupant membership tests: `2/2`
- focused BattlefieldTask filter: `50/50`
- focused recovery: `624/624`
- adjacent recovery/opening/store-smoke filter: `1205/1205`
- backend full: `6570/6570`
- mechanical checks: `git diff --check`; anchored conflict-marker scan; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This narrows P1-004 replay/recovery determinism and timing battlefield-task occupant membership enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
