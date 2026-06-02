# Stage 4D-17SV Recovery Timing Battlefield Task Participant Object Unit Card Membership Audit

Status: accepted server recovery validation closure slice. Project remains **NOT READY**.

Date: 2026-06-02 08:08 CST

## Scope

This slice tightens recovery timing battlefield-task validation so pending recovered player-view snapshot and spectator replay-frame `battlefieldTasks[]` participant object ids must reference objects tagged as `CardObjectTags.UnitCard`.

The runtime builder only emits battlefield-task participants from battlefield occupant objects that carry `UnitCard`. Recovery validation already checked participant object registry membership, battlefield location membership and participant controller/object consistency in prior slices; this slice closes the remaining same-payload gap where a non-unit object located at the task battlefield with a matching controller could be forged into `participantObjectIds[]`.

## Runtime Change

- `MatchRecoveryValidator` now builds a snapshot object-tag index from recovered player object payload `tags[]`.
- Spectator replay validation now builds an authoritative object-tag index from `MatchState.CardObjects`.
- Recovered and spectator battlefield-task list validation calls a shared participant object unit-card membership check after battlefield membership and before controller/object consistency.
- The validator emits explicit diagnostics when a participant object is missing from readable object tags or does not carry `UnitCard`.

Protocol shape changed: no. Command resolution, frontend, matrix JSON, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final readiness status are unchanged.

## Coverage

New tests:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskParticipantObjectsWithoutUnitTag`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskParticipantObjectsWithoutUnitTag`

Validation passed:

- focused participant unit-card tests: `2/2`
- focused BattlefieldTask filter: `44/44`
- focused recovery: `618/618`
- adjacent recovery/opening/store-smoke filter: `1199/1199`
- backend full: `6564/6564`
- mechanical checks: `git diff --check`; anchored conflict-marker scan; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This narrows P1-004 replay/recovery determinism and timing battlefield-task participant-object unit-card membership enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
