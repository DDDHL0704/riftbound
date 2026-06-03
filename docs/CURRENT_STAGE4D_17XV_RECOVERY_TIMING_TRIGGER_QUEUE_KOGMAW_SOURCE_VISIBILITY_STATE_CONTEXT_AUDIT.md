# Stage 4D-17XV Recovery Timing Trigger Queue Kogmaw Source Visibility-State Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XV closes one server P1-004 recovery/replay determinism slice for retained Kogmaw last-breath trigger queue entries. Runtime creates `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT` trigger queue items only from destroyed visible Kogmaw unit sources that are not face down and not standby-tagged. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads.

## Runtime Evidence

- `CoreRuleEngine.ResolveKogmawLastBreathAoePlayerId` returns a controller only when the destroyed object is card `OGN·190/298`, carries `CardObjectTags.UnitCard`, was destroyed to graveyard, is not face down and is not standby-tagged.
- `CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` writes effect kind `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`, triggered event kind `UNIT_DESTROYED`, the source object id and the destination battlefield marker into the queued trigger.
- Stage 4D-17WI, 17WJ, 17XC through 17XF and 17XU already cover Kogmaw marker/effect/event, nested-prefix, source-object, battlefield-object, battlefield-card, battlefield-state and source-card/unit context. This slice adds source visibility-state parity.
- The validator still does not parse the source id from the trigger id because the trigger prefix combines hyphenated stack item ids and hyphenated source object ids.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueKogmawLastBreathContext` now receives `sourceVisibility`, object face-down indexes and object tag labels in addition to the existing object card-number/tag indexes.
  - Recovered snapshot timing validation passes snapshot `sourceVisibility`, object face-down and tag indexes.
  - Authoritative state trigger queue validation passes authoritative object face-down and tag indexes.
  - Spectator replay-frame timing validation passes spectator `sourceVisibility` plus authoritative object face-down and tag indexes.
  - The guard emits Kogmaw source visibility, face-down and standby diagnostics only when the relevant payload or object indexes expose the source state.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame source visibility-state drift tests.

## Validation

- Focused new Kogmaw source visibility-state context filter: `3/3`
- Focused `TriggerQueue` filter: `218/218`
- Focused recovery filter: `903/903`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1483/1483`
- Backend full: `6848/6848`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
