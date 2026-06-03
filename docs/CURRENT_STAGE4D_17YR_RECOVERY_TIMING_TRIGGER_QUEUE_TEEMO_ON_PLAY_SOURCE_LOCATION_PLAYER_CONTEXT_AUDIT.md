# Stage 4D-17YR Recovery Timing Trigger Queue Teemo On-Play Source Location Player Context Audit

Date: 2026-06-03 21:14 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed a server P1-004 recovery/replay determinism slice for Teemo on-play self-power `triggerQueue[]` source object-location player context. The slice covers recovered snapshot timing payloads, authoritative state trigger queue payloads and spectator replay-frame timing payloads.

## Runtime Evidence

`CoreRuleEngine.PlaySourceUnitToBase` places the stack source object into the stack controller's `Base` zone. `CoreRuleEngine.BuildOnPlayTriggerQueueItem` then queues `TRIGGER-{stackItem.StackItemId}-{effectKind}` with `controllerId = stackItem.ControllerId`, `sourceObjectId = stackItem.SourceObjectId` and `triggeredByEventKind = UNIT_PLAYED_TO_BASE`. A retained readable Teemo source object whose object-location player differs from the trigger controller is therefore recovery drift.

## Implementation

`MatchRecoveryValidator` now extends `ValidateTriggerQueueTeemoOnPlaySelfPowerContext` so readable Teemo on-play source objects reject available object-location `playerId` values that differ from the trigger controller id. The existing source-location zone `BASE` check remains unchanged.

## Coverage

New `MatchRecoveryTests` coverage proves explicit diagnostics for:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueTeemoOnPlaySelfPowerSourceLocationPlayerContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueTeemoOnPlaySelfPowerSourceLocationPlayerContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerSourceLocationPlayerContextDrift`

## Validation

- Focused new Teemo source-location-player context tests: `3/3`
- Focused `TriggerQueue` filter: `284/284`
- Focused recovery filter: `969/969`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1549/1549`
- Backend full `Riftbound.slnx`: `6914/6914`
- Touched-file scoped whitespace format, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Locks

A_MAIN touched only `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/completion/P0-P1/next-dispatch docs, the shared board and this audit. Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
