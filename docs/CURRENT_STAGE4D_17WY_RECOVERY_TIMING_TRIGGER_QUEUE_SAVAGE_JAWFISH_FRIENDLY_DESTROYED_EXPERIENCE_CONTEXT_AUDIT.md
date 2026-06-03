# Stage 4D-17WY Recovery Timing Trigger Queue Savage Jawfish Friendly-Destroyed Experience Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted for this runtime/server closure slice. Project remains **NOT READY**.

## Scope

This slice covers recovery validation for Savage Jawfish friendly-destroyed experience trigger queue context.

Runtime `CoreRuleEngine.BuildSavageJawfishFriendlyDestroyedTriggerQueueItems` constructs this trigger shape as `TRIGGER-{stackItemId}-{sourceObjectId}-{destroyedObjectId}-SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`, with controller set to the destroyed unit owner, effect kind `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1` and triggered event kind `UNIT_DESTROYED`.

The construction is used when a friendly unit is destroyed while a visible Savage Jawfish watches the destruction event and creates an experience trigger for the destroyed unit's owner before the trigger is resolved through the trigger queue / stack flow.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now includes `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1` in the trigger queue context validator. The guard applies to:

- recovered snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The validator rejects:

- malformed Savage Jawfish trigger ids when the effect kind identifies the trigger;
- snapshot/spectator source visibility that is not `VISIBLE`;
- readable effect kind drift away from `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`;
- readable triggered event kind drift away from `UNIT_DESTROYED`.

Source object membership remains covered by the existing trigger queue membership validation. The trigger id includes stack, source and destroyed object ids, so direct source/destroyed-id parsing remains intentionally avoided because hyphenated stack/source/destroyed ids are ambiguous.

## Tests

Added `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueSavageJawfishFriendlyDestroyedExperienceContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueSavageJawfishFriendlyDestroyedExperienceContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSavageJawfishFriendlyDestroyedExperienceContextDrift`

Validation passed:

- focused new Savage Jawfish friendly-destroyed experience context tests: `3/3`
- focused `TriggerQueue` filter: `149/149`
- focused recovery filter: `834/834`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1414/1414`
- backend full conformance: `6779/6779`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests`, `docs`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Savage Jawfish trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, final readiness status and READY.
