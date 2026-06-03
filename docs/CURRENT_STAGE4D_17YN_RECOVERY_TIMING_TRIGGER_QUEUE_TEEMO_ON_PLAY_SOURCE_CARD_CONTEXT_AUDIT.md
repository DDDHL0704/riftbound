# Stage 4D-17YN Recovery Timing Trigger Queue Teemo On-Play Source Card Context Audit

Date: 2026-06-03

Project status: **NOT READY**.

## Scope

Stage 4D-17YN tightens P1-004 recovery/replay determinism for Teemo on-play self-power timing `triggerQueue[]` entries. This slice covers recovered snapshot, authoritative state and spectator replay-frame validation of the readable Teemo trigger source object's card identity.

Runtime `CoreRuleEngine.BuildOnPlayTriggerQueueItem` creates these trigger queue items as `TRIGGER-{stackItemId}-{effectKind}`, with `sourceObjectId` set to the played unit and `triggeredByEventKind` set to `UNIT_PLAYED_TO_BASE`. `CardBehaviorRegistry` maps `TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3`, `TEEMO_ALT_A_PLAY_UNIT_SELF_POWER_PLUS_3`, `TEEMO_ALT_B_PLAY_UNIT_SELF_POWER_PLUS_3` and `FND_TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3` to Teemo source cards `OGN·197/298`, `OGN·197a/298`, `OGN·197b/298` and `FND-196/298` respectively. Earlier Teemo trigger-queue slices already covered source visibility, effect/event and stack-context parity.

## Implementation

- `MatchRecoveryValidator` now passes recovered snapshot and authoritative object card-number indexes into Teemo on-play self-power trigger-queue context validation.
- Teemo on-play self-power trigger-queue entries now emit an explicit diagnostic when a readable source object's available card number does not match the Teemo card required by the parsed trigger effect kind.
- The guard is legacy-compatible: it only fires when the source object id is readable and the applicable object card-number index exposes that source object.

## Tests

Added `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueTeemoOnPlaySelfPowerSourceCardContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueTeemoOnPlaySelfPowerSourceCardContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerSourceCardContextDrift`

Each test keeps the Teemo trigger id, effect kind, triggered event kind, source visibility and source membership valid, then reports the source object's card number as `WRONG-CARD`, proving the new source-card diagnostic across recovered, authoritative and spectator validation surfaces.

## Validation

- Focused new Teemo source-card context tests: `3/3`
- Focused `TriggerQueue` filter: `272/272`
- Focused recovery filter: `957/957`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1537/1537`
- Backend full: `6902/6902`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed
- Matrix JSON parse: passed

## Locks

A_MAIN touched only `src/Riftbound.Engine/MatchRecovery.cs`, `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`, current checkpoint/completion/P0-P1/next-dispatch docs, the shared coordination board and this audit file.

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.
