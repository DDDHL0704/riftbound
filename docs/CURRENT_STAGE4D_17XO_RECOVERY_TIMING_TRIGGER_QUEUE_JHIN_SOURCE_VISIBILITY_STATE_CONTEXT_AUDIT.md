# Stage 4D-17XO Recovery Timing Trigger Queue Jhin Source Visibility-State Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Jhin movement-resource trigger queue source visibility-state context.

Runtime `CoreRuleEngine.BuildJhinMovementResourceTrigger` constructs movement-resource trigger ids as `JHIN_MOVE_RESOURCE::{tick}::{sourceObjectId}::{origin}::{destination}` only from Jhin unit sources that are not face down, not standby-tagged and controlled by the moving player. Stage 4D-17WH already tightened retained source/effect/event context for these ids, Stage 4D-17XM added source card-number and unit-tag validation, and Stage 4D-17XN added source-controller validation. This slice adds face-down and standby source-state validation without changing protocol shape.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Jhin movement-resource source visibility-state context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard reuses `TryReadJhinMovementTriggerContextForRecovery`, recovered snapshot object face-down / tag indexes, and authoritative object face-down / tag indexes. When the parsed source object id is readable and present in the applicable object registry, the validator rejects a source object that is face down or carries `CardObjectTags.Standby`. Existing source object, source-card/unit, source-controller, effect-kind and triggered-event-kind diagnostics remain unchanged.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueJhinMovementResourceSourceVisibilityStateContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueJhinMovementResourceSourceVisibilityStateContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceSourceVisibilityStateContextDrift`

Validation passed:

- focused new Jhin source visibility-state context tests: `3/3`
- focused `TriggerQueue` filter: `197/197`
- focused recovery filter: `882/882`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1462/1462`
- backend full conformance: `6827/6827`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Jhin trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
