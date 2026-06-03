# Stage 4D-17YT Recovery Timing Trigger Queue Standard Last-Breath Source Card Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

This slice tightens P1-004 recovery/replay determinism for the standard last-breath `triggerQueue[]` family. `MatchRecoveryValidator` now rejects readable standard last-breath trigger sources whose available card number does not match the source card required by the trigger effect kind in recovered snapshot, authoritative state and spectator replay-frame timing validation.

Runtime parity target:

- `CoreRuleEngine` resolves each standard last-breath trigger from the destroyed source object's card number before queuing the trigger.
- `CoreRuleEngine.BuildLastBreathTriggerQueueItem` queues `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{effectKind}` with `UNIT_DESTROYED`.
- The validator's source-card map covers Watchful Sentinel, Unsung Hero, Scouting Warhawk, Sad Poro, Loyal Poro, Honest Broker, Mechanical Trickster, Undercover Agent, Ironclad Vanguard and Muddy Dredger. Sad Poro accepts both its original and Unleashed card numbers.

## Code

- Added recovery-side source-card constants for the standard last-breath family.
- Extended `ValidateTriggerQueueStandardLastBreathSourceObjectIdContext` to also validate readable source object card numbers when the applicable object registry exposes them.
- Threaded recovered snapshot, authoritative state and spectator replay-frame object card-number registries into the shared standard last-breath source-object validation helper.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourceCardContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourceCardContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceCardContextDrift`

Validation passed:

- Focused new standard last-breath source-card context tests: `3/3`
- Focused `TriggerQueue` filter: `290/290`
- Focused recovery filter: `975/975`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1555/1555`
- Backend full: `6920/6920`
- Touched-file scoped whitespace format, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed

## Status

Runtime changed: recovery frame and authoritative-state validation only.

Protocol shape, frontend, matrix JSON, official catalog, Chrome/browser/formal E2E, `fullOfficial` and final readiness were not changed.

Project remains **NOT READY**.
