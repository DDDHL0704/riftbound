# Stage 4D-17XG Recovery Timing Trigger Queue Teemo On-Play Stack Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Teemo on-play self-power trigger queue stack context.

Runtime `CoreRuleEngine.BuildOnPlayTriggerQueueItem` constructs Teemo on-play self-power trigger ids as `TRIGGER-{stackItemId}-{effectKind}`. The source object remains stored in the trigger queue item payload, while the trigger id must still retain the runtime stack-item id segment before the effect-kind suffix.

Stage 4D-17WM through Stage 4D-17XF tightened retained trigger-queue context for last-breath, friendly-destroyed, OGS Lux and Kogmaw trigger families. This slice adds the equivalent stack-context guard for the Teemo on-play self-power family without changing protocol shape.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Teemo on-play stack context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard stays anchored on the known Teemo self-power effect-kind suffixes:

- `TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3`
- `TEEMO_ALT_A_PLAY_UNIT_SELF_POWER_PLUS_3`
- `TEEMO_ALT_B_PLAY_UNIT_SELF_POWER_PLUS_3`
- `FND_TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3`

It rejects forged ids like `TRIGGER--TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3` where the stack-item context between `TRIGGER-` and the effect-kind suffix is empty. Existing source visibility, effect kind and triggered-event-kind diagnostics remain unchanged.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueTeemoOnPlaySelfPowerStackContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueTeemoOnPlaySelfPowerStackContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerStackContextDrift`

Validation passed:

- focused new Teemo stack-context tests: `3/3`
- focused `TriggerQueue` filter: `173/173`
- focused recovery filter: `858/858`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1438/1438`
- backend full conformance: `6803/6803`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Teemo trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
