# Stage 4D-17YK Recovery Timing Trigger Queue OGS Lux Source Equipment-Tag Context Audit

Date: 2026-06-03

Project status: **NOT READY**.

## Scope

Stage 4D-17YK tightens P1-004 recovery/replay determinism for OGS Lux high-cost spell timing `triggerQueue[]` entries. This slice covers recovered snapshot, authoritative state and spectator replay-frame validation of OGS Lux trigger source equipment-tag context.

Runtime `CoreRuleEngine.ResolveOgsLuxHighCostSpellPlayedTriggers` queues OGS Lux high-cost spell triggers only by iterating `GetControlledFieldUnitObjectIds(playerZones, cardObjects, playerId)`, which excludes `CardObjectTags.EquipmentCard` before OGS Lux-specific card/unit/visibility filters run. Earlier OGS Lux trigger-queue slices already covered effect/event, source-object suffix, stack-context, source card/unit/visibility-state, source-controller and source field-zone parity.

## Implementation

- `MatchRecoveryValidator` now rejects readable OGS Lux high-cost spell trigger sources that carry `CardObjectTags.EquipmentCard` in the applicable recovered snapshot or authoritative object-tag registry.
- The guard keeps the existing source-card requirements intact: valid retained entries still need OGS Lux `OGS·006/024`, `UnitCard`, non-face-down and non-standby source context.
- The check mirrors runtime source enumeration and leaves command resolution, protocol shape, frontend, matrix and official catalog files unchanged.

## Tests

Added `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueOgsLuxHighCostSpellSourceEquipmentTagContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueOgsLuxHighCostSpellSourceEquipmentTagContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceEquipmentTagContextDrift`

Each test keeps the OGS Lux source card/unit/visibility-state, controller and field-zone membership valid, then adds `EquipmentCard`, proving the new equipment-tag diagnostic across recovered, authoritative and spectator validation surfaces.

## Validation

- Focused new OGS Lux source equipment-tag context tests: `3/3`
- Focused `TriggerQueue` filter: `263/263`
- Focused recovery filter: `948/948`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1528/1528`
- Backend full: `6893/6893`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- Matrix JSON parse: passed

## Locks

A_MAIN touched only `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/completion/P0-P1/next-dispatch docs, the shared coordination board and this audit file.

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
