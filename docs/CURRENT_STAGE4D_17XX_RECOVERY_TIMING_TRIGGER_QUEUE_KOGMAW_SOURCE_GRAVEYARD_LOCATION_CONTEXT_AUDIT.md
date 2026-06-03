# Stage 4D-17XX Recovery Timing Trigger Queue Kogmaw Source Graveyard Location Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17XX closes one server P1-004 recovery/replay determinism slice for retained Kogmaw last-breath trigger queue entries. Runtime only creates `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT` trigger queue items after a visible, non-face-down, non-standby Kogmaw unit is destroyed to graveyard. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads when an object-location index is available.

## Runtime Evidence

- `CoreRuleEngine.ResolveKogmawLastBreathAoePlayerId` returns a controller only when `removalResult.WasDestroyed`, `removalResult.WasUnit` and `removalResult.DestinationZone == "GRAVEYARD"` hold, with a battlefield object id and a readable Kogmaw unit source.
- `CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` writes the destroyed source object id into the queued trigger after the source has moved through the removal result to graveyard.
- Stage 4D-17WI, 17WJ, 17XC through 17XF and 17XU through 17XW already cover Kogmaw marker/effect/event, nested-prefix, source-object, battlefield-object, battlefield-card, battlefield-state, source-card/unit, source visibility-state and source-controller context. This slice adds source graveyard-location parity.
- Legacy recovered snapshots without source object-location entries remain compatible. Authoritative state and spectator replay-frame validation use authoritative object locations.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - `ValidateTriggerQueueKogmawLastBreathContext` now receives object-location indexes and labels.
  - Recovered snapshot timing validation passes recovered snapshot object locations.
  - Authoritative state trigger queue validation passes authoritative object locations.
  - Spectator replay-frame timing validation passes authoritative object locations.
  - The guard emits Kogmaw source-location diagnostics when the source object id is readable, the applicable object-location index contains the source, and that source location zone is not `GRAVEYARD`.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame source-location drift tests.

## Validation

- Focused new Kogmaw source-location context filter: `3/3`
- Focused `TriggerQueue` filter: `224/224`
- Focused recovery filter: `909/909`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1489/1489`
- Backend full: `6854/6854`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
