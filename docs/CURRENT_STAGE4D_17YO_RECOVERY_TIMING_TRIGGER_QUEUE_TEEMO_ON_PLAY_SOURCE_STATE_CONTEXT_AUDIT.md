# Stage 4D-17YO Recovery Timing Trigger Queue Teemo On-Play Source State Context Audit

Date: 2026-06-03 20:45 CST

Scope: server P1-004 recovery/replay determinism for Teemo on-play self-power `triggerQueue[]` source object state. This slice covers recovered snapshot, authoritative state and spectator replay-frame timing payloads only.

Runtime evidence: `CoreRuleEngine` resolves `PlaysSourceToBaseAsUnit` before queuing the Teemo on-play self-power trigger and creates ids as `TRIGGER-{stackItem.StackItemId}-{effectKind}` through `BuildOnPlayTriggerQueueItem`, with `sourceObjectId` equal to the played source unit and `triggeredByEventKind` equal to `UNIT_PLAYED_TO_BASE`. Teemo card definitions keep `CardObjectTags.Standby`, so this slice intentionally validates only that the readable source is not face down and is tagged as a unit card.

Implementation: `MatchRecoveryValidator` now passes object face-down and tag indexes into `ValidateTriggerQueueTeemoOnPlaySelfPowerContext`. That helper rejects readable Teemo on-play self-power trigger entries when the applicable object registry reports the source object as face down or missing `CardObjectTags.UnitCard`.

Coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueTeemoOnPlaySelfPowerSourceStateContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueTeemoOnPlaySelfPowerSourceStateContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerSourceStateContextDrift`

Validation:

- Focused new source-state tests: `3/3`
- Focused `TriggerQueue` filter: `275/275`
- Focused recovery filter: `960/960`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1540/1540`
- Backend full `Riftbound.slnx`: `6905/6905`
- Touched-file scoped whitespace format passed
- `git diff --check` passed
- Anchored conflict-marker scan over `docs`, `tests` and `src` passed with no matches
- Matrix JSON parse passed

Write locks: A_MAIN touched only `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/completion/P0-P1/next-dispatch docs, the shared coordination board and this audit. Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Result: accepted as Stage 4D-17YO. This narrows Teemo trigger-queue recovery parity only. Project remains **NOT READY**.
