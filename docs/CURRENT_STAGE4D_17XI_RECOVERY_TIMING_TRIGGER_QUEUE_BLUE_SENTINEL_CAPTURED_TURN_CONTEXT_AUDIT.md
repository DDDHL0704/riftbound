# Stage 4D-17XI Recovery Timing Trigger Queue Blue Sentinel Captured-Turn Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Blue Sentinel delayed-resource trigger queue captured-turn context.

Runtime `CoreRuleEngine.BuildBlueSentinelHeldDelayedResourceTriggers` constructs delayed-resource trigger ids as `BLUE_SENTINEL_HELD_DELAYED_RESOURCE::{capturedTurnNumber}::{sourceObjectId}::{battlefieldObjectId}` using the turn number at battlefield-held resolution. A recovered or spectator trigger id whose captured turn is greater than the current turn cannot be produced by that runtime path.

Stage 4D-17XH tightened Blue Sentinel battlefield-state context. This slice adds future captured-turn rejection without changing protocol shape or converting the delayed-resource payment window into a queue-lifetime invariant.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Blue Sentinel delayed-resource future captured-turn context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard reuses `TryReadBlueSentinelDelayedTriggerContextForRecovery` and the current recovered snapshot or authoritative turn number. It rejects encoded captured turn numbers greater than the current turn while preserving same-turn and next-turn queue lifetime. Existing source object, source visibility, effect kind, triggered-event-kind and battlefield-state diagnostics remain unchanged.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueBlueSentinelDelayedResourceFutureCapturedTurnContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueBlueSentinelDelayedResourceFutureCapturedTurnContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueBlueSentinelDelayedResourceFutureCapturedTurnContextDrift`

Validation passed:

- focused new Blue Sentinel future captured-turn context tests: `3/3`
- focused `TriggerQueue` filter: `179/179`
- focused recovery filter: `864/864`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1444/1444`
- backend full conformance: `6809/6809`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Blue Sentinel trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
