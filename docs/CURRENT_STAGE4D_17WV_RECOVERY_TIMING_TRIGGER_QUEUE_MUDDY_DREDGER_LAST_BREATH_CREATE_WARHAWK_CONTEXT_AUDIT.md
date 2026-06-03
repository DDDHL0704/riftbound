# Stage 4D-17WV Recovery Timing Trigger Queue Muddy Dredger Last-Breath Create-Warhawk Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted for this runtime/server closure slice. Project remains **NOT READY**.

## Scope

This slice covers recovery validation for Muddy Dredger last-breath create-warhawk trigger queue context.

Runtime `CoreRuleEngine.BuildLastBreathTriggerQueueItem` constructs this trigger shape as `TRIGGER-{stackItemId}-{sourceObjectId}-MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK`, with effect kind `MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK` and triggered event kind `UNIT_DESTROYED`.

The same construction is used from the true destroy path and the state-based cleanup path before the trigger is resolved through the trigger queue / stack flow.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now includes `MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK` in the standard last-breath trigger queue context validator. The guard applies to:

- recovered snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The validator rejects:

- malformed Muddy Dredger trigger ids when the effect kind identifies the trigger;
- snapshot/spectator source visibility that is not `VISIBLE`;
- readable effect kind drift away from `MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK`;
- readable triggered event kind drift away from `UNIT_DESTROYED`.

Source object membership remains covered by the existing trigger queue membership validation. The standard trigger id includes stack and source ids, so direct source-id parsing remains intentionally avoided because hyphenated stack/source ids are ambiguous.

## Tests

Added `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueMuddyDredgerLastBreathCreateWarhawkContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueMuddyDredgerLastBreathCreateWarhawkContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMuddyDredgerLastBreathCreateWarhawkContextDrift`

Validation passed:

- focused new Muddy Dredger last-breath create-warhawk context tests: `3/3`
- focused `TriggerQueue` filter: `140/140`
- focused recovery filter: `825/825`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1405/1405`
- backend full conformance: `6770/6770`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests`, `docs`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Muddy Dredger trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, final readiness status and READY.
