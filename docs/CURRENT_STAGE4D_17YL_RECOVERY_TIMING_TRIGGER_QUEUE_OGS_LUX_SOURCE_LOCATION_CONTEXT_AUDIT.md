# Stage 4D-17YL Recovery Timing Trigger Queue OGS Lux Source Location Context Audit

Date: 2026-06-03

Project status: **NOT READY**.

## Scope

Stage 4D-17YL tightens P1-004 recovery/replay determinism for OGS Lux high-cost spell timing `triggerQueue[]` entries. This slice covers recovered snapshot, authoritative state and spectator replay-frame validation of OGS Lux trigger source object-location context.

Runtime `CoreRuleEngine.ResolveOgsLuxHighCostSpellPlayedTriggers` queues OGS Lux high-cost spell triggers only from `GetControlledFieldUnitObjectIds(playerZones, cardObjects, playerId)`, which enumerates the trigger controller's `base` plus `battlefields` field zones before OGS Lux-specific card/unit/visibility filters run. Earlier OGS Lux trigger-queue slices already covered effect/event, source-object suffix, stack-context, source card/unit/visibility-state, source-controller, source field-zone and source equipment-tag parity.

## Implementation

- `MatchRecoveryValidator` now passes recovered snapshot and authoritative object-location indexes into OGS Lux high-cost spell trigger-queue context validation.
- OGS Lux high-cost spell trigger-queue entries now emit an explicit diagnostic when a readable source object has an available object-location zone outside `BASE` or `BATTLEFIELD`.
- The guard is legacy-compatible: it only fires when the applicable object-location index exposes the source object and a non-empty zone.

## Tests

Added `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueOgsLuxHighCostSpellSourceLocationContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueOgsLuxHighCostSpellSourceLocationContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceLocationContextDrift`

Each test keeps the OGS Lux source card/unit/visibility-state, controller, field-zone membership and equipment-tag context valid, then reports the source object's object-location zone as `GRAVEYARD`, proving the new object-location diagnostic across recovered, authoritative and spectator validation surfaces.

## Validation

- Focused new OGS Lux source location context tests: `3/3`
- Focused `TriggerQueue` filter: `266/266`
- Focused recovery filter: `951/951`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1531/1531`
- Backend full: `6896/6896`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- Matrix JSON parse: passed

## Locks

A_MAIN touched only `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/completion/P0-P1/next-dispatch docs, the shared coordination board and this audit file.

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
