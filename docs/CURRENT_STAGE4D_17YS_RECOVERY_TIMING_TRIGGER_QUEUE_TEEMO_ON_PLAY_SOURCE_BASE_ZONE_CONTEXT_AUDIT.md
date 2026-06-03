# Stage 4D-17YS Recovery Timing Trigger Queue Teemo Source Base-Zone Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

This slice tightens P1-004 recovery/replay determinism for Teemo on-play self-power `triggerQueue[]` entries. `MatchRecoveryValidator` now rejects readable Teemo trigger sources that are controlled by the trigger controller but are absent from that controller's `base` player-zone list in recovered snapshot, authoritative state and spectator replay-frame timing validation.

Runtime parity target:

- `CoreRuleEngine.PlaySourceUnitToBase` writes the played source object into `playerZones[stackItem.ControllerId].Base`.
- `CoreRuleEngine.BuildOnPlayTriggerQueueItem` queues `TRIGGER-{stackItem.StackItemId}-{effectKind}` with the stack controller, played source object and `UNIT_PLAYED_TO_BASE`.
- Therefore retained readable Teemo trigger payloads must agree with the trigger controller's base-zone membership, complementing prior source-card, source-state, source-controller, object-location zone and object-location player checks.

## Code

- Added recovered snapshot and authoritative-state base-zone object indexes.
- Passed those indexes through recovered snapshot, authoritative state and spectator replay timing trigger-queue validation.
- Extended `ValidateTriggerQueueTeemoOnPlaySelfPowerContext` to require the readable source object to appear in the trigger controller's base zone when the source object's controller matches the trigger controller and a player-zone index is available.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueTeemoOnPlaySelfPowerSourceBaseZoneContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueTeemoOnPlaySelfPowerSourceBaseZoneContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerSourceBaseZoneContextDrift`

Validation passed:

- Focused new Teemo source-base-zone context tests: `3/3`
- Focused `TriggerQueue` filter: `287/287`
- Focused recovery filter: `972/972`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1552/1552`
- Backend full: `6917/6917`
- Touched-file scoped whitespace format, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed

## Status

Runtime changed: recovery frame and authoritative-state validation only.

Protocol shape, frontend, matrix JSON, official catalog, Chrome/browser/formal E2E, `fullOfficial` and final readiness were not changed.

Project remains **NOT READY**.
