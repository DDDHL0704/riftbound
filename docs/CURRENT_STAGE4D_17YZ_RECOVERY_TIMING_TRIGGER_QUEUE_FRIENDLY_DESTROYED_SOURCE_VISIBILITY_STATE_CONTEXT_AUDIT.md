# Stage 4D-17YZ Recovery Timing Trigger Queue Friendly-Destroyed Source Visibility-State Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

This slice tightens P1-004 recovery/replay determinism for friendly-destroyed and Viktor destroyed-non-minion `triggerQueue[]` families. `MatchRecoveryValidator` now rejects readable trigger sources that are face down or standby-tagged for Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion triggers.

Runtime parity target:

- Runtime builds these triggers as `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{destroyedObjectId}-{effectKind}`.
- Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion runtime builders all require the source object to be a unit-card source that is not face down and does not carry `CardObjectTags.Standby`.
- Retained readable recovery payloads must preserve source visibility-state parity when the applicable recovered or authoritative object registry exposes the source object's face-down flag or tags.

## Code

- Added a shared friendly-destroyed/Viktor source visibility-state validation helper.
- Threaded the helper through recovered snapshot, authoritative state and spectator replay-frame trigger queue validation.
- Reused existing recovered snapshot and authoritative state object face-down and tag indexes.
- Preserved compatibility when the source object is hidden or the applicable object registry does not expose face-down/tag state.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueFriendlyDestroyedSourceVisibilityStateContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueFriendlyDestroyedSourceVisibilityStateContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedSourceVisibilityStateContextDrift`

Validation passed:

- Focused new friendly-destroyed source visibility-state context tests: `3/3`
- Focused `TriggerQueue` filter: `308/308`
- Focused recovery filter: `993/993`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1573/1573`
- Backend full: `6938/6938`
- Touched-file scoped whitespace format passed
- `git diff --check` passed
- Anchored conflict-marker scan passed
- Matrix JSON parse passed with `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This only narrows recovery frame and authoritative-state validation for one trigger-family context. Broader command/recovery/random determinism, remaining nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, P0/P1 and final readiness remain open. Project remains **NOT READY**.
