# Stage 4D-17XE Recovery Timing Trigger Queue Kogmaw Last-Breath Battlefield-Card Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Kogmaw last-breath trigger queue battlefield-card context.

Runtime `CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` constructs the trigger id as `TRIGGER-{stackItemId}-{sourceObjectId}-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::{battlefieldObjectId}`, with effect kind `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT` and triggered event kind `UNIT_DESTROYED`.

Stage 4D-17XD already required the parsed battlefield object id to exist in the applicable object registry. This slice adds battlefield-card tag consistency: when object tags are readable, that parsed battlefield object id must carry `P6TokenFactoryCatalog.BattlefieldCardTag`.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Kogmaw last-breath battlefield-card context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard remains anchored on the stable runtime marker `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::`. It reuses recovered snapshot and authoritative object-tag indexes, does not parse arbitrary stack/source id segments, and does not broaden into battlefield lane-state membership validation.

Existing nested Kogmaw stack-prefix acceptance remains covered and is not treated as a Kogmaw last-breath trigger payload.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueKogmawLastBreathBattlefieldCardContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueKogmawLastBreathBattlefieldCardContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathBattlefieldCardContextDrift`

Validation passed:

- focused new Kogmaw last-breath battlefield-card context tests: `3/3`
- focused `TriggerQueue` filter: `167/167`
- focused recovery filter: `852/852`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1432/1432`
- backend full conformance: `6797/6797`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Kogmaw trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
