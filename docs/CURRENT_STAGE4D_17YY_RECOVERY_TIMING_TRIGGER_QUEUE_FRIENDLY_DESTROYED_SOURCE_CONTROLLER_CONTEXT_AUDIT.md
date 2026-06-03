# Stage 4D-17YY Recovery Timing Trigger Queue Friendly-Destroyed Source Controller Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

This slice tightens P1-004 recovery/replay determinism for friendly-destroyed and Viktor destroyed-non-minion `triggerQueue[]` families. `MatchRecoveryValidator` now rejects readable trigger sources whose effective source object controller differs from the trigger controller for Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion triggers.

Runtime parity target:

- Runtime builds these triggers as `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{destroyedObjectId}-{effectKind}`.
- Ghostly Centaur, Resonant Soul and Savage Jawfish runtime builders filter source field objects through `EffectiveFieldControllerId(...) == destroyedOwnerPlayerId` and queue the trigger with `ControllerId = destroyedOwnerPlayerId`.
- Viktor destroyed-non-minion runtime builders filter source field objects through `EffectiveFieldControllerId(...) == destroyedControllerId` and queue the trigger with `ControllerId = destroyedControllerId`.
- Retained readable recovery payloads must preserve source-controller parity when the applicable recovered or authoritative object registry exposes the source object controller.

## Code

- Added a shared friendly-destroyed/Viktor source-controller validation helper.
- Threaded the helper through recovered snapshot, authoritative state and spectator replay-frame trigger queue validation.
- Reused existing recovered snapshot and authoritative state object-controller indexes.
- Preserved compatibility when the trigger controller is hidden, the source object is hidden, or the applicable object-controller registry does not expose the source.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueFriendlyDestroyedSourceControllerContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueFriendlyDestroyedSourceControllerContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedSourceControllerContextDrift`

Validation passed:

- Focused new friendly-destroyed source-controller context tests: `3/3`
- Focused `TriggerQueue` filter: `305/305`
- Focused recovery filter: `990/990`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1570/1570`
- Backend full: `6935/6935`
- Touched-file scoped whitespace format passed
- `git diff --check` passed
- Anchored conflict-marker scan passed
- Matrix JSON parse passed with `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This only narrows recovery frame and authoritative-state validation for one trigger-family context. Broader command/recovery/random determinism, remaining nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, P0/P1 and final readiness remain open. Project remains **NOT READY**.
