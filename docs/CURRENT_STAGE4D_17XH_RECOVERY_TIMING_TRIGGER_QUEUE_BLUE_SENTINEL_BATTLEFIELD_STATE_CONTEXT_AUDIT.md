# Stage 4D-17XH Recovery Timing Trigger Queue Blue Sentinel Battlefield-State Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Blue Sentinel delayed-resource trigger queue battlefield-state context.

Runtime `CoreRuleEngine.BuildBlueSentinelHeldDelayedResourceTriggers` constructs delayed-resource trigger ids as `BLUE_SENTINEL_HELD_DELAYED_RESOURCE::{capturedTurnNumber}::{sourceObjectId}::{battlefieldObjectId}`. Later delayed-resource payment validation reads the same battlefield object id and requires the Blue Sentinel source to still hold that battlefield.

Stage 4D-17XG tightened Teemo on-play stack context. This slice applies the same trigger-id context discipline to Blue Sentinel's encoded battlefield id without changing protocol shape.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Blue Sentinel delayed-resource battlefield-state context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard reuses recovered snapshot `lanes.battlefields[]` and authoritative `MatchState.BattlefieldStates` indexes. It rejects forged trigger ids whose encoded battlefield object id is not present in the current battlefield-state set while keeping existing source object, source visibility, effect kind and triggered-event-kind diagnostics unchanged.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueBlueSentinelDelayedResourceBattlefieldStateContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueBlueSentinelDelayedResourceBattlefieldStateContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueBlueSentinelDelayedResourceBattlefieldStateContextDrift`

Validation passed:

- focused new Blue Sentinel battlefield-state context tests: `3/3`
- focused `TriggerQueue` filter: `176/176`
- focused recovery filter: `861/861`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1441/1441`
- backend full conformance: `6806/6806`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Blue Sentinel trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
