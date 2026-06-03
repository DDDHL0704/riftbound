# Stage 4D-17WW Recovery Timing Trigger Queue Ghostly Centaur Friendly-Destroyed Power Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted for this runtime/server closure slice. Project remains **NOT READY**.

## Scope

This slice covers recovery validation for Ghostly Centaur friendly-destroyed power trigger queue context.

Runtime `CoreRuleEngine.BuildGhostlyCentaurFriendlyDestroyedTriggerQueueItems` constructs this trigger shape as `TRIGGER-{stackItemId}-{sourceObjectId}-{destroyedObjectId}-GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`, with controller set to the destroyed unit owner, effect kind `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2` and triggered event kind `UNIT_DESTROYED`.

The construction is used when a friendly unit is destroyed while Ghostly Centaur watches the destruction event and creates a power trigger for the destroyed unit's owner before the trigger is resolved through the trigger queue / stack flow.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now includes `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2` in the trigger queue context validator. The guard applies to:

- recovered snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The validator rejects:

- malformed Ghostly Centaur trigger ids when the effect kind identifies the trigger;
- snapshot/spectator source visibility that is not `VISIBLE`;
- readable effect kind drift away from `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`;
- readable triggered event kind drift away from `UNIT_DESTROYED`.

Source object membership remains covered by the existing trigger queue membership validation. The trigger id includes stack, source and destroyed object ids, so direct source/destroyed-id parsing remains intentionally avoided because hyphenated stack/source/destroyed ids are ambiguous.

## Tests

Added `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueGhostlyCentaurFriendlyDestroyedPowerContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueGhostlyCentaurFriendlyDestroyedPowerContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueGhostlyCentaurFriendlyDestroyedPowerContextDrift`

Validation passed:

- focused new Ghostly Centaur friendly-destroyed power context tests: `3/3`
- focused `TriggerQueue` filter: `143/143`
- focused recovery filter: `828/828`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1408/1408`
- backend full conformance: `6773/6773`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests`, `docs`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Ghostly Centaur trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, final readiness status and READY.
