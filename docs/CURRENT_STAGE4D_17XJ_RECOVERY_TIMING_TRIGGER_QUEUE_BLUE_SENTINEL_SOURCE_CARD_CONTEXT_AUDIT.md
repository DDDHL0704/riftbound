# Stage 4D-17XJ Recovery Timing Trigger Queue Blue Sentinel Source-Card Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Blue Sentinel delayed-resource trigger queue source-card context.

Runtime `CoreRuleEngine.BuildBlueSentinelHeldDelayedResourceTriggers` constructs delayed-resource trigger ids as `BLUE_SENTINEL_HELD_DELAYED_RESOURCE::{capturedTurnNumber}::{sourceObjectId}::{battlefieldObjectId}` from the Blue Sentinel source that held a battlefield. A recovered or spectator trigger id whose source object id resolves to another card, or to an object without the unit tag, cannot represent a valid runtime Blue Sentinel delayed-resource trigger.

Stage 4D-17XH tightened Blue Sentinel battlefield-state context, and Stage 4D-17XI tightened future captured-turn context. This slice adds source card-number and unit-tag validation without changing protocol shape.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Blue Sentinel delayed-resource source-card context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard reuses `TryReadBlueSentinelDelayedTriggerContextForRecovery`, adds recovered snapshot and authoritative object card-number indexes, and reuses recovered snapshot / authoritative object-tag indexes. When the parsed source object id is readable in the applicable object registry, the validator rejects card numbers other than `P4ActivatedAbilityCatalog.BlueSentinelCardNo` and rejects objects missing `CardObjectTags.UnitCard`. Existing source object, source visibility, effect kind, triggered-event-kind, battlefield-state and captured-turn diagnostics remain unchanged.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueBlueSentinelDelayedResourceSourceCardContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueBlueSentinelDelayedResourceSourceCardContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueBlueSentinelDelayedResourceSourceCardContextDrift`

Validation passed:

- focused new Blue Sentinel source-card context tests: `3/3`
- focused `TriggerQueue` filter: `182/182`
- focused recovery filter: `867/867`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1447/1447`
- backend full conformance: `6812/6812`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Blue Sentinel trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
