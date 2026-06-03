# Stage 4D-17XD Recovery Timing Trigger Queue Kogmaw Last-Breath Battlefield-Object Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Kogmaw last-breath trigger queue battlefield-object context.

Runtime `CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` constructs the trigger id as `TRIGGER-{stackItemId}-{sourceObjectId}-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::{battlefieldObjectId}`, with effect kind `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT` and triggered event kind `UNIT_DESTROYED`.

The recovery validator already recognized this trigger family, validated the source-object segment, and required a non-empty battlefield marker suffix. This slice adds battlefield-object registry consistency: the battlefield object id parsed from the trigger id must exist in the recovered snapshot, authoritative state or spectator authoritative object registry being validated.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Kogmaw last-breath battlefield-object context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard remains anchored on the stable runtime marker `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::`. It does not parse arbitrary stack/source id segments and does not broaden into battlefield-card tag or lane-state validation; this slice only proves the runtime battlefield object id is present in the applicable object registry.

Existing nested Kogmaw stack-prefix acceptance remains covered and is not treated as a Kogmaw last-breath trigger payload.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueKogmawLastBreathBattlefieldObjectContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueKogmawLastBreathBattlefieldObjectContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathBattlefieldObjectContextDrift`

Validation passed:

- focused new Kogmaw last-breath battlefield-object context tests: `3/3`
- focused `TriggerQueue` filter: `164/164`
- focused recovery filter: `849/849`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1429/1429`
- backend full conformance: `6794/6794`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Kogmaw trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
