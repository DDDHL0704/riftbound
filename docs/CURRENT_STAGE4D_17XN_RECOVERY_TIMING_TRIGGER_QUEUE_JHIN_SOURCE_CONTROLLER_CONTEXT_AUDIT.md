# Stage 4D-17XN Recovery Timing Trigger Queue Jhin Source-Controller Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Jhin movement-resource trigger queue source-controller context.

Runtime `CoreRuleEngine.BuildJhinMovementResourceTrigger` constructs movement-resource trigger ids as `JHIN_MOVE_RESOURCE::{tick}::{sourceObjectId}::{origin}::{destination}` only from Jhin unit sources controlled by the moving player. Stage 4D-17WH already tightened retained source/effect/event context for these ids, and Stage 4D-17XM added source card-number and unit-tag validation. This slice adds source-controller validation without changing protocol shape.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Jhin movement-resource source-controller context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard reuses `TryReadJhinMovementTriggerContextForRecovery`, recovered snapshot object-controller indexes, and authoritative object-controller indexes. When the parsed source object id is readable and present in the applicable object registry, the validator rejects a source object whose effective controller does not match the trigger controller. Existing source object, source-card/unit, effect-kind and triggered-event-kind diagnostics remain unchanged.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueJhinMovementResourceSourceControllerContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueJhinMovementResourceSourceControllerContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceSourceControllerContextDrift`

Validation passed:

- focused new Jhin source-controller context tests: `3/3`
- focused `TriggerQueue` filter: `194/194`
- focused recovery filter: `879/879`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1459/1459`
- backend full conformance: `6824/6824`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Jhin trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
