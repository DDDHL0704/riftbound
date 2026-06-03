# Stage 4D-17YJ Recovery Timing Trigger Queue OGS Lux Source Field-Zone Context Audit

Date: 2026-06-03

Project status: **NOT READY**.

## Scope

Stage 4D-17YJ tightens P1-004 recovery/replay determinism for OGS Lux high-cost spell timing `triggerQueue[]` entries. This slice covers recovered snapshot, authoritative state and spectator replay-frame validation of the OGS Lux trigger source field-zone membership.

Runtime `CoreRuleEngine.ResolveOgsLuxHighCostSpellPlayedTriggers` queues OGS Lux high-cost spell triggers only by iterating `GetControlledFieldUnitObjectIds(playerZones, cardObjects, playerId)`, which considers the controller's `base` plus `battlefields` zones and then filters to controlled field unit objects. Earlier OGS Lux trigger-queue slices already covered effect/event, source-object suffix, stack-context, source card/unit/visibility-state and source-controller parity.

## Implementation

- `MatchRecoveryValidator` now builds recovered snapshot and authoritative player field-zone indexes from player `base` plus `battlefields` zone lists.
- OGS Lux high-cost spell trigger-queue entries now emit an explicit diagnostic when a readable source object is controlled by the trigger controller but absent from that controller's field zones.
- The guard is legacy-compatible: it only fires when the applicable player-zone index exposes the trigger controller and the object-controller index confirms the readable source belongs to that controller.

## Tests

Added `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueOgsLuxHighCostSpellSourceFieldZoneContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueOgsLuxHighCostSpellSourceFieldZoneContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceFieldZoneContextDrift`

Each test keeps the OGS Lux source card/unit/visibility-state and controller valid, then removes the source from the trigger controller's field zones, proving the new field-zone membership diagnostic across recovered, authoritative and spectator validation surfaces.

## Validation

- Focused new OGS Lux source field-zone context tests: `3/3`
- Focused `TriggerQueue` filter: `260/260`
- Focused recovery filter: `945/945`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1525/1525`
- Backend full: `6890/6890`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- Matrix JSON parse: passed

## Locks

A_MAIN touched only `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/completion/P0-P1/next-dispatch docs, the shared coordination board and this audit file.

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
