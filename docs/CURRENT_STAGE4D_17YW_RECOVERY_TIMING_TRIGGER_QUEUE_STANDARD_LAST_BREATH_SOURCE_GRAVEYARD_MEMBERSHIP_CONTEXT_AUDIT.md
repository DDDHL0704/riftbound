# Stage 4D-17YW Recovery Timing Trigger Queue Standard Last-Breath Source Graveyard-Membership Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

This slice tightens P1-004 recovery/replay determinism for the standard last-breath `triggerQueue[]` family. `MatchRecoveryValidator` now rejects readable standard last-breath trigger sources whose object-location zone is `GRAVEYARD` but whose source object is missing from that location player's `graveyard` player-zone list in recovered snapshot, authoritative state and spectator replay-frame timing validation.

Runtime parity target:

- Standard last-breath triggers are queued from destroyed source objects after field removal succeeds.
- `CoreRuleEngine.TryDestroyTarget` removes the destroyed source object from the field zone owner and appends it to that same player's `Graveyard` list when the object is not banished or recalled to base.
- `CoreRuleEngine.ReconcileObjectLocations` maps each object in `zones.Graveyard` to `ObjectLocationState(playerId, "GRAVEYARD")` for that same player.
- Therefore retained readable standard last-breath trigger payloads must keep the source object in the graveyard list for the player named by the source object's `GRAVEYARD` location when the applicable player-zone registry exposes that list.

## Code

- Added recovered snapshot and authoritative-state player graveyard object-id indexes, with compatibility skips for absent or malformed legacy zone lists.
- Threaded those indexes into recovered snapshot, authoritative state and spectator replay-frame standard last-breath source-object validation.
- Extended `ValidateTriggerQueueStandardLastBreathSourceObjectIdContext` to reject graveyard membership drift only after the source object id matches the runtime trigger id suffix and the source object's readable location zone is already `GRAVEYARD`.
- Preserved compatibility when the source object, object-location registry, player-zone registry or readable graveyard list is hidden or absent.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourceGraveyardMembershipContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourceGraveyardMembershipContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceGraveyardMembershipContextDrift`

Validation passed:

- Focused new standard last-breath source graveyard-membership context tests: `3/3`
- Focused `TriggerQueue` filter: `299/299`
- Focused recovery filter: `984/984`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1564/1564`
- Backend full: `6929/6929`
- Touched-file scoped whitespace format, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed

## Status

Runtime changed: recovery frame and authoritative-state validation only.

Protocol shape, frontend, matrix JSON, official catalog, Chrome/browser/formal E2E, `fullOfficial` and final readiness were not changed.

Project remains **NOT READY**.
