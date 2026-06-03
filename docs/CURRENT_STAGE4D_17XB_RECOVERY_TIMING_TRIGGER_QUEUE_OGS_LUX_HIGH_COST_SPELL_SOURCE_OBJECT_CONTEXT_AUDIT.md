# Stage 4D-17XB Recovery Timing Trigger Queue OGS Lux High-Cost Spell Source-Object Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for OGS Lux high-cost spell trigger queue source-object context.

Runtime `CoreRuleEngine.ResolveOgsLuxHighCostSpellPlayedTriggers` constructs the trigger id as `TRIGGER-{stackItemId}-{sourceObjectId}-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`, with effect kind `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` and triggered event kind `CARD_PLAYED`.

The recovery validator already recognized this trigger family for source visibility, effect kind and triggered event kind. This slice adds a source-object consistency guard: when `sourceObjectId` is readable and not `HIDDEN`, it must appear immediately before the effect-kind suffix in the trigger id.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates OGS Lux high-cost spell source-object context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard does not parse arbitrary stack/source id segments. It uses the exact runtime suffix shape `-{sourceObjectId}-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`, so ids containing hyphens remain safe.

Existing trigger queue source-object membership validation continues to cover whether the readable source object exists in the recovered snapshot, authoritative state or spectator authoritative registry.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueOgsLuxHighCostSpellSourceObjectContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueOgsLuxHighCostSpellSourceObjectContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceObjectContextDrift`

Validation passed:

- focused new OGS Lux high-cost spell source-object context tests: `3/3`
- focused `TriggerQueue` filter: `158/158`
- focused recovery filter: `843/843`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1423/1423`
- backend full conformance: `6788/6788`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and OGS Lux trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
