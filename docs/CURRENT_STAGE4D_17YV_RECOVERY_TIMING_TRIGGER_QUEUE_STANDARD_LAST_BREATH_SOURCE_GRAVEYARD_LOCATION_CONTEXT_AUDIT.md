# Stage 4D-17YV Recovery Timing Trigger Queue Standard Last-Breath Source Graveyard-Location Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

This slice tightens P1-004 recovery/replay determinism for the standard last-breath `triggerQueue[]` family. `MatchRecoveryValidator` now rejects readable standard last-breath trigger sources whose object-location zone is not `GRAVEYARD` in recovered snapshot, authoritative state and spectator replay-frame timing validation.

Runtime parity target:

- Standard last-breath triggers are queued from destroyed source objects after field removal succeeds.
- Standard last-breath resolvers require `FieldRemovalResult.DestinationZone == "GRAVEYARD"` or queue only after `TryDestroyTarget` reports a destroyed unit.
- `CoreRuleEngine.ReconcileObjectLocations` maps the destroyed source object through the owner graveyard zone before recovered/authoritative state is retained.
- Therefore retained readable standard last-breath trigger payloads must keep the source object in `GRAVEYARD` when the applicable object-location registry exposes that source.

## Code

- Threaded recovered snapshot, authoritative state and spectator replay-frame object-location registries into the shared standard last-breath source-object validation helper.
- Extended `ValidateTriggerQueueStandardLastBreathSourceObjectIdContext` to reject source location drift after the source object id is proven to match the runtime trigger id suffix.
- Preserved legacy compatibility when the source object or object-location registry is hidden, absent or lacks a readable zone.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourceLocationContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourceLocationContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceLocationContextDrift`

Validation passed:

- Focused new standard last-breath source-location context tests: `3/3`
- Focused `TriggerQueue` filter: `296/296`
- Focused recovery filter: `981/981`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1561/1561`
- Backend full: `6926/6926`
- Touched-file scoped whitespace format, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed

## Status

Runtime changed: recovery frame and authoritative-state validation only.

Protocol shape, frontend, matrix JSON, official catalog, Chrome/browser/formal E2E, `fullOfficial` and final readiness were not changed.

Project remains **NOT READY**.
