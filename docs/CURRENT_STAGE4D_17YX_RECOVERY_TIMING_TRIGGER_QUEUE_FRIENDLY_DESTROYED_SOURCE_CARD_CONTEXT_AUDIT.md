# Stage 4D-17YX Recovery Timing Trigger Queue Friendly-Destroyed Source Card Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

This slice tightens P1-004 recovery/replay determinism for friendly-destroyed and Viktor destroyed-non-minion `triggerQueue[]` families. `MatchRecoveryValidator` now rejects readable trigger sources whose source object card number or unit tag does not match the runtime source-card requirements for Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion triggers.

Runtime parity target:

- Runtime builds these triggers as `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{destroyedObjectId}-{effectKind}`.
- Ghostly Centaur sources must be `UNL-068/219` unit cards.
- Resonant Soul sources must be `OGN·118/298` unit cards.
- Savage Jawfish sources must be `UNL-129/219` unit cards.
- Viktor destroyed-non-minion sources must be one of `ARC-006/006`, `OGN·246/298` or `OGN·246a/298` and must be unit cards.
- Each runtime builder filters source objects through the active field object registry before queueing the trigger, so retained readable recovery payloads must preserve source card/unit parity when the applicable object registry exposes that source.

## Code

- Added friendly-destroyed/Viktor source card-number constants and a shared source-card/unit validation helper.
- Threaded the helper through recovered snapshot, authoritative state and spectator replay-frame trigger queue validation.
- Reused existing recovered/authoritative object card-number and tag indexes.
- Preserved compatibility when the source object, card-number registry or tag registry is hidden or absent.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueFriendlyDestroyedSourceCardContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueFriendlyDestroyedSourceCardContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedSourceCardContextDrift`

Validation passed:

- Focused new friendly-destroyed source-card context tests: `3/3`
- Focused `TriggerQueue` filter: `302/302`
- Focused recovery filter: `987/987`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1567/1567`
- Backend full: `6932/6932`
- Touched-file scoped whitespace format, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed

## Status

Runtime changed: recovery frame and authoritative-state validation only.

Protocol shape, frontend, matrix JSON, official catalog, Chrome/browser/formal E2E, `fullOfficial` and final readiness were not changed.

Project remains **NOT READY**.
