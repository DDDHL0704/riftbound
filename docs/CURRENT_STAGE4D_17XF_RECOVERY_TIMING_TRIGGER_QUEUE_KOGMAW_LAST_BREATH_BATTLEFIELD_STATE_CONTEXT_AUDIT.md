# Stage 4D-17XF Recovery Timing Trigger Queue Kogmaw Last-Breath Battlefield-State Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Kogmaw last-breath trigger queue battlefield-state context.

Runtime `CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` constructs the trigger id as `TRIGGER-{stackItemId}-{sourceObjectId}-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::{battlefieldObjectId}`, with effect kind `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT` and triggered event kind `UNIT_DESTROYED`. The `battlefieldObjectId` comes from the destroyed Kogmaw's battlefield location and is later used by Kogmaw stack resolution to find units at that battlefield.

Stage 4D-17XD already required the parsed battlefield object id to exist in the applicable object registry. Stage 4D-17XE required the object to carry `P6TokenFactoryCatalog.BattlefieldCardTag` when object tags are readable. This slice adds battlefield-state consistency: when the battlefield-state set is available, that parsed battlefield object id must exist in recovered snapshot `lanes.battlefields[]` or authoritative `MatchState.BattlefieldStates`.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Kogmaw last-breath battlefield-state context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard remains anchored on the stable runtime marker `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::`. It reuses recovered snapshot lane-state and authoritative battlefield-state indexes, does not parse arbitrary stack/source id segments, and avoids redundant state diagnostics when the referenced object is absent from the object registry or is not a battlefield card.

Existing nested Kogmaw stack-prefix acceptance remains covered and is not treated as a Kogmaw last-breath trigger payload.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueKogmawLastBreathBattlefieldStateContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueKogmawLastBreathBattlefieldStateContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathBattlefieldStateContextDrift`

Validation passed:

- focused new Kogmaw last-breath battlefield-state context tests: `3/3`
- focused `TriggerQueue` filter: `170/170`
- focused recovery filter: `855/855`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1435/1435`
- backend full conformance: `6800/6800`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Kogmaw trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
