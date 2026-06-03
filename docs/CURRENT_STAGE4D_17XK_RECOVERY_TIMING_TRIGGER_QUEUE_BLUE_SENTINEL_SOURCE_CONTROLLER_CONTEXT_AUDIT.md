# Stage 4D-17XK Recovery Timing Trigger Queue Blue Sentinel Source-Controller Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Blue Sentinel delayed-resource trigger queue source-controller context.

Runtime `CoreRuleEngine.BuildBlueSentinelHeldDelayedResourceTriggers` constructs delayed-resource trigger ids as `BLUE_SENTINEL_HELD_DELAYED_RESOURCE::{capturedTurnNumber}::{sourceObjectId}::{battlefieldObjectId}` only from Blue Sentinel defender objects controlled by the defending battlefield winner. Runtime payment resolution later requires the trigger controller / pending payment player to still control that same source object before materializing the delayed resource.

Stage 4D-17XJ tightened Blue Sentinel source card-number and unit-tag context. This slice adds source-controller validation without changing protocol shape.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Blue Sentinel delayed-resource source-controller context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard reuses `TryReadBlueSentinelDelayedTriggerContextForRecovery`, recovered snapshot object-controller indexes and authoritative object-controller indexes. When the parsed source object id has a readable controller, the validator rejects controller ids that differ from the trigger controller. Existing source object, source visibility, effect kind, triggered-event-kind, source-card/unit, battlefield-state and captured-turn diagnostics remain unchanged.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueBlueSentinelDelayedResourceSourceControllerContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueBlueSentinelDelayedResourceSourceControllerContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueBlueSentinelDelayedResourceSourceControllerContextDrift`

Validation passed:

- focused new Blue Sentinel source-controller context tests: `3/3`
- focused `TriggerQueue` filter: `185/185`
- focused recovery filter: `870/870`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1450/1450`
- backend full conformance: `6815/6815`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Blue Sentinel trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
