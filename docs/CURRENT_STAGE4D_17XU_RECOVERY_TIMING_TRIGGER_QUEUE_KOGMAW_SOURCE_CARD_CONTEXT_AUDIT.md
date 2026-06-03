# Stage 4D-17XU Recovery Timing Trigger Queue Kogmaw Source Card Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XU closes one server P1-004 recovery/replay determinism slice for retained Kogmaw last-breath trigger queue entries. Runtime creates `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT` trigger queue items only from destroyed Kogmaw unit sources. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads when the retained payload exposes a readable source object id.

## Runtime Evidence

- `CoreRuleEngine.ResolveKogmawLastBreathAoePlayerId` returns a controller only when the destroyed object is card `OGN·190/298`, carries `CardObjectTags.UnitCard`, was destroyed to graveyard, is not face down and is not standby-tagged.
- `CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` writes effect kind `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`, triggered event kind `UNIT_DESTROYED`, the source object id and the destination battlefield marker into the queued trigger.
- Stage 4D-17WI, 17WJ and 17XC through 17XF already cover Kogmaw marker/effect/event, nested-prefix, source-object, battlefield-object, battlefield-card and battlefield-state context. This slice adds source-card/unit parity.
- The validator deliberately does not parse the source id from the trigger id because the trigger prefix combines hyphenated stack item ids and hyphenated source object ids.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - Added `KogmawCardNoForRecovery`.
  - `ValidateTriggerQueueKogmawLastBreathContext` now receives object card-number and tag indexes with labels.
  - Recovered snapshot timing validation passes snapshot object card-number/tag indexes.
  - Authoritative state and spectator replay-frame timing validation pass authoritative object card-number/tag indexes.
  - The guard emits source-card and source-unit diagnostics only when `sourceObjectId` is readable and present in the applicable indexes.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame source-card/unit drift tests.

## Validation

- Focused new Kogmaw source-card context filter: `3/3`
- Focused `TriggerQueue` filter: `215/215`
- Focused recovery filter: `900/900`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1480/1480`
- Backend full: `6845/6845`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
