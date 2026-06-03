# Stage 4D-17YM Recovery Timing Trigger Queue OGS Lux Source Location Player Context Audit

Date: 2026-06-03

Project status: **NOT READY**.

## Scope

Stage 4D-17YM tightens P1-004 recovery/replay determinism for OGS Lux high-cost spell timing `triggerQueue[]` entries. This slice covers recovered snapshot, authoritative state and spectator replay-frame validation of the readable OGS Lux trigger source object's object-location player context.

Runtime `CoreRuleEngine.ResolveOgsLuxHighCostSpellPlayedTriggers` queues OGS Lux high-cost spell triggers only from `GetControlledFieldUnitObjectIds(playerZones, cardObjects, playerId)` for the trigger controller. That helper enumerates the controller's `base` plus `battlefields` field zones and filters by controller before OGS Lux-specific card/unit/visibility filters run. Earlier OGS Lux trigger-queue slices already covered effect/event, source-object suffix, stack-context, source card/unit/visibility-state, source-controller, source field-zone, source equipment-tag and source object-location zone parity.

## Implementation

- OGS Lux high-cost spell trigger-queue entries now emit an explicit diagnostic when a readable source object's available object-location player id differs from the trigger controller id.
- The guard is legacy-compatible: it only fires when the source object id and trigger controller are readable, the applicable object-location index exposes the source object and that location has a non-empty player id.
- The existing source object-location zone guard remains unchanged and still requires readable source locations to be `BASE` or `BATTLEFIELD`.

## Tests

Added `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueOgsLuxHighCostSpellSourceLocationPlayerContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueOgsLuxHighCostSpellSourceLocationPlayerContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceLocationPlayerContextDrift`

Each test keeps the OGS Lux source card/unit/visibility-state, controller, field-zone membership, equipment-tag state and object-location zone valid, then reports the source object's object-location player id as `bob` while the trigger controller is `alice`, proving the new object-location player diagnostic across recovered, authoritative and spectator validation surfaces.

## Validation

- Focused new OGS Lux source location-player context tests: `3/3`
- Focused `TriggerQueue` filter: `269/269`
- Focused recovery filter: `954/954`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1534/1534`
- Backend full: `6899/6899`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- Matrix JSON parse: passed

## Locks

A_MAIN touched only `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/completion/P0-P1/next-dispatch docs, the shared coordination board and this audit file.

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
