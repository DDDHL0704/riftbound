# Stage 4D-17YI Recovery Timing Trigger Queue OGS Lux Source Controller Context Audit

Date: 2026-06-03

Project status: **NOT READY**.

## Scope

Stage 4D-17YI tightens P1-004 recovery/replay determinism for OGS Lux high-cost spell timing `triggerQueue[]` entries. This slice covers recovered snapshot, authoritative state and spectator replay-frame validation of the OGS Lux trigger source controller.

Runtime `CoreRuleEngine.ResolveOgsLuxHighCostSpellPlayedTriggers` queues OGS Lux high-cost spell triggers only by iterating `GetControlledFieldUnitObjectIds(playerZones, cardObjects, playerId)`, so the queued trigger controller must also control the readable source Lux object. Earlier OGS Lux trigger-queue slices already covered effect/event, source-object suffix, stack-context and source card/unit/visibility-state parity.

## Implementation

- `MatchRecoveryValidator` now passes object-controller indexes into `ValidateTriggerQueueOgsLuxHighCostSpellContext` for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads.
- OGS Lux high-cost spell trigger-queue entries now emit an explicit diagnostic when a readable source object has an object-controller id that differs from the trigger controller id.
- The guard is legacy-compatible: it only fires when the relevant object-controller index exposes the readable source object.

## Tests

Added `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueOgsLuxHighCostSpellSourceControllerContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueOgsLuxHighCostSpellSourceControllerContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceControllerContextDrift`

Each test keeps the OGS Lux source card/unit/visibility-state valid and mutates only the source controller away from the trigger controller, proving the new source-controller parity diagnostic across recovered, authoritative and spectator validation surfaces.

## Validation

- Focused new OGS Lux source-controller context tests: `3/3`
- Focused `TriggerQueue` filter: `257/257`
- Focused recovery filter: `942/942`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1522/1522`
- Backend full: `6887/6887`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- Matrix JSON parse: passed

## Locks

A_MAIN touched only `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/completion/P0-P1/next-dispatch docs, the shared coordination board and this audit file.

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
