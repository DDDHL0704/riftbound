# Stage 4D-17YQ Recovery Timing Trigger Queue Teemo On-Play Source Location Context Audit

Date: 2026-06-03 21:05 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed a server P1-004 recovery/replay determinism slice for Teemo on-play self-power `triggerQueue[]` source location context. The slice covers recovered snapshot timing payloads, authoritative state trigger queue payloads and spectator replay-frame timing payloads.

## Runtime Evidence

`CoreRuleEngine.PlaySourceUnitToBase` places the stack source object into the stack controller's `Base` zone before `CoreRuleEngine.BuildOnPlayTriggerQueueItem` queues `TRIGGER-{stackItem.StackItemId}-{effectKind}` with `sourceObjectId = stackItem.SourceObjectId`, `controllerId = stackItem.ControllerId` and `triggeredByEventKind = UNIT_PLAYED_TO_BASE`. A retained readable Teemo on-play source object in any non-`BASE` object-location zone is therefore recovery drift.

## Implementation

`MatchRecoveryValidator` now passes recovered and authoritative object-location indexes into `ValidateTriggerQueueTeemoOnPlaySelfPowerContext`. For readable Teemo on-play source objects, the helper rejects any available object-location zone other than `BASE`, using the applicable recovered snapshot or authoritative-state object-location label in diagnostics.

## Coverage

New `MatchRecoveryTests` coverage proves explicit diagnostics for:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueTeemoOnPlaySelfPowerSourceLocationContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueTeemoOnPlaySelfPowerSourceLocationContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerSourceLocationContextDrift`

## Validation

- Focused new Teemo source-location context tests: `3/3`
- Focused `TriggerQueue` filter: `281/281`
- Focused recovery filter: `966/966`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1546/1546`
- Backend full `Riftbound.slnx`: `6911/6911`
- Touched-file scoped whitespace format, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Locks

A_MAIN touched only `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/completion/P0-P1/next-dispatch docs, the shared board and this audit. Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
