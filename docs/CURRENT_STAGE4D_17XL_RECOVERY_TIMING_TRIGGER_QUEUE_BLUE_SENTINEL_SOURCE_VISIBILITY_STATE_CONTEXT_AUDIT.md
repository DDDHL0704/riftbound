# Stage 4D-17XL Recovery Timing Trigger Queue Blue Sentinel Source Visibility-State Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Blue Sentinel delayed-resource trigger queue source visibility-state context.

Runtime `CoreRuleEngine.BuildBlueSentinelHeldDelayedResourceTriggers` constructs delayed-resource trigger ids as `BLUE_SENTINEL_HELD_DELAYED_RESOURCE::{capturedTurnNumber}::{sourceObjectId}::{battlefieldObjectId}` only from non-face-down, non-standby Blue Sentinel defender units. Runtime payment resolution later repeats the same source-state checks before materializing the delayed resource.

Stage 4D-17XJ tightened source card-number and unit-tag context, and Stage 4D-17XK tightened source-controller context. This slice adds source face-down and standby validation without changing protocol shape.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Blue Sentinel delayed-resource source visibility-state context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard reuses `TryReadBlueSentinelDelayedTriggerContextForRecovery`, recovered snapshot object face-down indexes, authoritative object face-down indexes and existing object-tag indexes. When the parsed source object id is readable, the validator rejects face-down sources and sources tagged with `CardObjectTags.Standby`. Existing source object, source visibility, effect kind, triggered-event-kind, source-card/unit, source-controller, battlefield-state and captured-turn diagnostics remain unchanged.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueBlueSentinelDelayedResourceSourceVisibilityStateContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueBlueSentinelDelayedResourceSourceVisibilityStateContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueBlueSentinelDelayedResourceSourceVisibilityStateContextDrift`

Validation passed:

- focused new Blue Sentinel source visibility-state context tests: `3/3`
- focused `TriggerQueue` filter: `188/188`
- focused recovery filter: `873/873`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1453/1453`
- backend full conformance: `6818/6818`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Blue Sentinel trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
