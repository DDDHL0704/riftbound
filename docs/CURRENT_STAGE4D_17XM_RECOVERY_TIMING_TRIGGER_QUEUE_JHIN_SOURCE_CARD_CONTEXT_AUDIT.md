# Stage 4D-17XM Recovery Timing Trigger Queue Jhin Source-Card Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Jhin movement-resource trigger queue source-card context.

Runtime `CoreRuleEngine.BuildJhinMovementResourceTrigger` constructs movement-resource trigger ids as `JHIN_MOVE_RESOURCE::{tick}::{sourceObjectId}::{origin}::{destination}` only from Jhin unit sources that are not face down, not standby-tagged and are controlled by the moving player. Stage 4D-17WH already tightened retained source/effect/event context for these ids. This slice adds source card-number and unit-tag validation without changing protocol shape.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Jhin movement-resource source-card context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard reuses `TryReadJhinMovementTriggerContextForRecovery`, recovered snapshot object card-number / tag indexes, and authoritative object card-number / tag indexes. When the parsed source object id is readable, the validator rejects sources whose card number is not `P4ActivatedAbilityCatalog.JhinCardNo` and sources that do not carry `CardObjectTags.UnitCard`. Existing source object, effect kind and triggered-event-kind diagnostics remain unchanged.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueJhinMovementResourceSourceCardContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueJhinMovementResourceSourceCardContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceSourceCardContextDrift`

Validation passed:

- focused new Jhin source-card context tests: `3/3`
- focused `TriggerQueue` filter: `191/191`
- focused recovery filter: `876/876`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1456/1456`
- backend full conformance: `6821/6821`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Jhin trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
