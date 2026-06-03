# Stage 4D-17YU Recovery Timing Trigger Queue Standard Last-Breath Source Controller Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

This slice tightens P1-004 recovery/replay determinism for the standard last-breath `triggerQueue[]` family. `MatchRecoveryValidator` now rejects readable standard last-breath trigger sources whose effective source object controller does not match the trigger controller in recovered snapshot, authoritative state and spectator replay-frame timing validation.

Runtime parity target:

- Standard last-breath resolvers derive the trigger controller from the destroyed source object's controller, owner or field-controller fallback.
- `CoreRuleEngine.BuildLastBreathTriggerQueueItem` queues `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{effectKind}` with that controller and `UNIT_DESTROYED`.
- Therefore retained readable standard last-breath trigger payloads must agree with the source object's effective controller when the applicable object registry exposes it.

## Code

- Threaded recovered snapshot, authoritative state and spectator replay-frame object controller registries into the shared standard last-breath source-object validation helper.
- Extended `ValidateTriggerQueueStandardLastBreathSourceObjectIdContext` to reject source controller drift after the source object id is proven to match the runtime trigger id suffix.
- Preserved legacy compatibility when the source object, trigger controller or object controller registry is hidden or absent.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourceControllerContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourceControllerContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceControllerContextDrift`

Validation passed:

- Focused new standard last-breath source-controller context tests: `3/3`
- Focused `TriggerQueue` filter: `293/293`
- Focused recovery filter: `978/978`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1558/1558`
- Backend full: `6923/6923`
- Touched-file scoped whitespace format, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed

## Status

Runtime changed: recovery frame and authoritative-state validation only.

Protocol shape, frontend, matrix JSON, official catalog, Chrome/browser/formal E2E, `fullOfficial` and final readiness were not changed.

Project remains **NOT READY**.
