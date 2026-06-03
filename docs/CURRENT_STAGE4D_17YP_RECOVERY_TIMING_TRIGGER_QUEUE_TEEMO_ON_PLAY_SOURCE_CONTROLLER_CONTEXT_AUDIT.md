# Stage 4D-17YP Recovery Timing Trigger Queue Teemo On-Play Source Controller Context Audit

Date: 2026-06-03 20:54 CST

Scope: server P1-004 recovery/replay determinism for Teemo on-play self-power `triggerQueue[]` source-controller context. This slice covers recovered snapshot, authoritative state and spectator replay-frame timing payloads only.

Runtime evidence: `CoreRuleEngine` resolves `PlaysSourceToBaseAsUnit` before queuing the Teemo on-play self-power trigger. `PlaySourceUnitToBase` places the source object in the stack controller's base zone, and `BuildOnPlayTriggerQueueItem` creates `TRIGGER-{stackItem.StackItemId}-{effectKind}` with `ControllerId = stackItem.ControllerId`, `SourceObjectId = stackItem.SourceObjectId` and `TriggeredByEventKind = UNIT_PLAYED_TO_BASE`.

Implementation: `MatchRecoveryValidator` now passes trigger controller id and object-controller indexes into `ValidateTriggerQueueTeemoOnPlaySelfPowerContext`. That helper rejects readable Teemo on-play self-power trigger entries when the applicable object registry reports a source object controller that differs from the trigger controller.

Coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueTeemoOnPlaySelfPowerSourceControllerContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueTeemoOnPlaySelfPowerSourceControllerContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerSourceControllerContextDrift`

Validation:

- Focused new source-controller tests: `3/3`
- Focused `TriggerQueue` filter: `278/278`
- Focused recovery filter: `963/963`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1543/1543`
- Backend full `Riftbound.slnx`: `6908/6908`
- Touched-file scoped whitespace format passed
- `git diff --check` passed
- Anchored conflict-marker scan over `docs`, `tests` and `src` passed with no matches
- Matrix JSON parse passed

Write locks: A_MAIN touched only `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/completion/P0-P1/next-dispatch docs, the shared coordination board and this audit. Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Result: accepted as Stage 4D-17YP. This narrows Teemo trigger-queue recovery parity only. Project remains **NOT READY**.
