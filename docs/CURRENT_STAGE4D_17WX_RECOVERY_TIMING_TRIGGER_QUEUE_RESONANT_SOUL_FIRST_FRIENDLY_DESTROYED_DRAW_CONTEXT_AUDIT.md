# Stage 4D-17WX Recovery Timing Trigger Queue Resonant Soul First Friendly-Destroyed Draw Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted for this runtime/server closure slice. Project remains **NOT READY**.

## Scope

This slice covers recovery validation for Resonant Soul first friendly-destroyed draw trigger queue context.

Runtime `CoreRuleEngine.BuildResonantSoulFirstFriendlyDestroyedTriggerQueueItems` constructs this trigger shape as `TRIGGER-{stackItemId}-{sourceObjectId}-{destroyedObjectId}-RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`, with controller set to the destroyed unit owner, effect kind `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1` and triggered event kind `UNIT_DESTROYED`.

The construction is used when the first friendly unit is destroyed for that owner during the turn while a visible Resonant Soul watches the destruction event and creates a draw trigger for the destroyed unit's owner before the trigger is resolved through the trigger queue / stack flow.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now includes `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1` in the trigger queue context validator. The guard applies to:

- recovered snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The validator rejects:

- malformed Resonant Soul trigger ids when the effect kind identifies the trigger;
- snapshot/spectator source visibility that is not `VISIBLE`;
- readable effect kind drift away from `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`;
- readable triggered event kind drift away from `UNIT_DESTROYED`.

Source object membership remains covered by the existing trigger queue membership validation. The trigger id includes stack, source and destroyed object ids, so direct source/destroyed-id parsing remains intentionally avoided because hyphenated stack/source/destroyed ids are ambiguous.

## Tests

Added `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueResonantSoulFirstFriendlyDestroyedDrawContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueResonantSoulFirstFriendlyDestroyedDrawContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueResonantSoulFirstFriendlyDestroyedDrawContextDrift`

Validation passed:

- focused new Resonant Soul first friendly-destroyed draw context tests: `3/3`
- focused `TriggerQueue` filter: `146/146`
- focused recovery filter: `831/831`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1411/1411`
- backend full conformance: `6776/6776`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests`, `docs`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Resonant Soul trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, final readiness status and READY.
