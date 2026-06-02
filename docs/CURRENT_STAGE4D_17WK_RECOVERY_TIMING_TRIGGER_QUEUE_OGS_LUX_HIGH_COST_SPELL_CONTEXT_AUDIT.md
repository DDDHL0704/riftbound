# Stage 4D-17WK Recovery Timing Trigger Queue OGS Lux High-Cost Spell Context Audit

Date: 2026-06-03

Project status: **NOT READY**.

## Scope

Stage 4D-17WK tightened the OGS Lux high-cost spell trigger-queue recovery recognizer across recovered snapshots, authoritative state and spectator replay frames.

Runtime creates these trigger queue items as `TRIGGER-{stackItemId}-{sourceObjectId}-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`, with effect kind `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` and triggered event kind `CARD_PLAYED`.

## Runtime Basis

- `CoreRuleEngine.ResolveOgsLuxHighCostSpellPlayedTriggers` queues OGS Lux high-cost spell triggers with effect kind `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`.
- The retained trigger id ends with the current effect kind suffix and the triggered event kind is `CARD_PLAYED`.
- Source object membership stays covered by existing trigger-queue object membership validation because stack/source id parsing is ambiguous for hyphen-bearing ids.

## Coverage

New `MatchRecoveryTests`:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueOgsLuxHighCostSpellContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueOgsLuxHighCostSpellContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellContextDrift`

The tests prove recovered snapshot, authoritative state and spectator replay-frame timing payloads reject OGS Lux high-cost spell effect/event drift.

## Validation

- Focused new OGS Lux high-cost spell context tests: `3/3`
- Focused `TriggerQueue` filter: `107/107`
- Focused recovery filter: `792/792`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1372/1372`
- Backend full conformance: `6737/6737`
- Touched-file scoped whitespace format passed.
- `git diff --check` passed.
- Anchored conflict-marker scan over `src`, `tests` and `docs` passed.
- Matrix JSON parse passed.

## Remaining Risk

This narrows replay/recovery determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial`, P0/P1 closure and final status remain open.
