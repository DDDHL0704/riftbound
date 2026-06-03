# Stage 4D-17YB Recovery Timing Trigger Queue Blue Sentinel Source Zone Membership Context Audit

Date: 2026-06-03

Owner: A_MAIN

## Scope

Stage 4D-17YB closes one server P1-004 recovery/replay determinism slice for retained Blue Sentinel delayed-resource trigger queue entries. Runtime can only offer the delayed resource while the encoded source Blue Sentinel object is still present in the paying player's battlefield zone list. This audit records the matching recovery validator guard for recovered snapshot timing payloads, authoritative state trigger queues and spectator replay-frame timing payloads when player battlefield-zone membership is available.

## Runtime Evidence

- `CoreRuleEngine.BlueSentinelDelayedTriggerCanPay` consumes retained Blue Sentinel delayed-resource triggers only when `BlueSentinelDelayedSourceStillHoldsBattlefield` remains true.
- `CoreRuleEngine.BlueSentinelDelayedSourceStillHoldsBattlefield` requires the source object to remain a valid Blue Sentinel unit at `BATTLEFIELD` with the trigger id encoded battlefield object id, then requires `state.PlayerZones[playerId].Battlefields` to contain that source object id.
- Stage 4D-17XH through 17XL, 17XZ and 17YA already cover Blue Sentinel trigger id/effect/event, source card/unit, source controller, source visibility-state, captured turn, battlefield-state, source battlefield-location and encoded battlefield controller context. This slice adds source battlefield-zone membership parity.
- Legacy recovered snapshots without readable player `zones.battlefields` payloads remain compatible. Authoritative state and spectator replay-frame validation use authoritative `MatchState.PlayerZones`.

## Implementation

- `src/Riftbound.Engine/MatchRecovery.cs`
  - Added a recovered snapshot player battlefield-zone membership index from `players[].zones.battlefields`.
  - Added an authoritative player battlefield-zone membership index from `MatchState.PlayerZones`.
  - `ValidateTriggerQueueBlueSentinelDelayedResourceContext` now rejects a Blue Sentinel delayed-resource trigger whose encoded source object is controlled by the trigger controller and located on the encoded battlefield, but is absent from the trigger controller's battlefield zone list.
  - The guard runs only when source controller, source location and battlefield-state context are sufficiently aligned, preserving existing diagnostics as primary for controller/location/battlefield-state drift.
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
  - Added recovered snapshot, authoritative state and spectator replay-frame source zone-membership drift tests.

## Validation

- Focused new Blue Sentinel source zone-membership context filter: `3/3`
- Focused `TriggerQueue` filter: `236/236`
- Focused recovery filter: `921/921`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1501/1501`
- Backend full: `6866/6866`
- Touched-file scoped whitespace format: passed
- `git diff --check`: passed
- Anchored conflict-marker scan: passed
- Matrix JSON parse: passed

## Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**.
